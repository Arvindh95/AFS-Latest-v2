using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using FinancialReport.Helper;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;

namespace FinancialReport.Services
{
    /// <summary>
    /// This service encapsulates the entire business process of generating a single financial report.
    /// It is responsible for orchestrating data fetching, placeholder mapping, and file creation.
    /// </summary>
    public class ReportGenerationService
    {
        private readonly FLRTFinancialReportMaint _graph;
        private readonly FLRTFinancialReport _currentRecord;
        private readonly AuthService _authService;
        private readonly FileService _fileService;
        private readonly WordTemplateService _wordTemplateService;

        public ReportGenerationService(FLRTFinancialReportMaint graph, FLRTFinancialReport record, AuthService authService)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _currentRecord = record ?? throw new ArgumentNullException(nameof(record));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            // Instantiate dependent services
            _fileService = new FileService(_graph);
            _wordTemplateService = new WordTemplateService();
        }

        /// <summary>
        /// Executes the end-to-end report generation process with performance optimizations.
        /// </summary>
        /// <returns>The GUID of the newly generated and saved file.</returns>
        public Guid Execute()
        {
            var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();

            string templatePath = null;
            string outputPath = null;

            try
            {
                // 1. Get Tenant Name for the data service
                int? companyID = _graph.GetCompanyIDFromDB(_currentRecord.ReportID);
                string tenantName = _graph.MapCompanyIDToTenantName(companyID);

                // 1b. Load Report Definitions — multi-definition (new) or single legacy fallback
                GIColumnMapping columnMapping = null;
                var definitionLinks = new List<ReportCalculationEngine.DefinitionLink>();

                // Check for linked definitions first (multi-def path)
                var linkedDefs = SelectFrom<FLRTReportDefinitionLink>
                    .InnerJoin<FLRTReportDefinition>
                        .On<FLRTReportDefinition.definitionID.IsEqual<FLRTReportDefinitionLink.definitionID>>
                    .Where<FLRTReportDefinitionLink.reportID.IsEqual<@P.AsInt>>
                    .OrderBy<FLRTReportDefinitionLink.displayOrder.Asc>
                    .View.Select(_graph, _currentRecord.ReportID)
                    .Cast<PXResult<FLRTReportDefinitionLink, FLRTReportDefinition>>()
                    .ToList();

                if (linkedDefs.Any())
                {
                    // Use the first definition's GI mapping for GL data fetching
                    var firstDef = linkedDefs.First().GetItem<FLRTReportDefinition>();
                    columnMapping = GIColumnMapping.FromDefinition(firstDef);

                    foreach (var result in linkedDefs)
                    {
                        var def = result.GetItem<FLRTReportDefinition>();
                        definitionLinks.Add(new ReportCalculationEngine.DefinitionLink
                        {
                            DefinitionID = def.DefinitionID.Value,
                            Prefix       = def.DefinitionPrefix,
                            Rounding     = RoundingSettings.FromDefinition(def)
                        });
                    }

                    PXTrace.WriteInformation($"Multi-definition report: {definitionLinks.Count} definition(s) linked — prefixes: [{string.Join(", ", definitionLinks.Select(d => d.Prefix))}]");
                }
                else if (_currentRecord.DefinitionID != null)
                {
                    // Legacy single-definition fallback
                    var reportDef = SelectFrom<FLRTReportDefinition>
                        .Where<FLRTReportDefinition.definitionID.IsEqual<@P.AsInt>>
                        .View.Select(_graph, _currentRecord.DefinitionID)
                        .TopFirst;

                    if (reportDef != null)
                    {
                        columnMapping = GIColumnMapping.FromDefinition(reportDef);
                        var rounding  = RoundingSettings.FromDefinition(reportDef);
                        definitionLinks.Add(new ReportCalculationEngine.DefinitionLink
                        {
                            DefinitionID = reportDef.DefinitionID.Value,
                            Prefix       = reportDef.DefinitionPrefix,
                            Rounding     = rounding
                        });
                        PXTrace.WriteInformation($"Legacy single definition '{reportDef.DefinitionCD}' (prefix: {reportDef.DefinitionPrefix}): GI={columnMapping.GIName}, Rounding={rounding.RoundingLevel}/{rounding.DecimalPlaces}dp");
                    }
                }

                var localDataService = new FinancialDataService(_authService, tenantName, columnMapping);

                // 2. Get Template File
                var (templateFileContent, originalFileName) = _fileService.GetFileContentAndName(_currentRecord.Noteid, _currentRecord);
                if (templateFileContent == null || templateFileContent.Length == 0)
                    throw new PXException(Messages.TemplateFileIsEmpty);

                // Validate originalFileName is not null before using it
                if (string.IsNullOrWhiteSpace(originalFileName))
                {
                    PXTrace.WriteError("Original file name is null or empty");
                    throw new PXException(Messages.TemplateFileIsEmpty);
                }

                string extension = Path.GetExtension(originalFileName) ?? ".docx";
                templatePath = Path.Combine(Path.GetTempPath(), $"{_currentRecord.ReportCD}_Template{extension}");
                File.WriteAllBytes(templatePath, templateFileContent);

                // 3. Extract Placeholders
                List<string> extractedKeys = _wordTemplateService.ExtractPlaceholderKeys(templatePath);

                // 4. ✅ NEW: Separate ALL placeholder types (wildcard, exact range, regular)
                var (wildcardRangePlaceholders, exactRangePlaceholders, regularPlaceholders) =
                    localDataService.SeparateAllPlaceholderTypes(extractedKeys);

                PXTrace.WriteInformation($"📊 Extracted {extractedKeys.Count} total placeholders:");
                PXTrace.WriteInformation($"   🌟 {wildcardRangePlaceholders.Count} wildcard range (A????:B????_e_CY)");
                PXTrace.WriteInformation($"   🎯 {exactRangePlaceholders.Count} exact range (A74101:A75101_e_CY)");
                PXTrace.WriteInformation($"   📝 {regularPlaceholders.Count} regular (A74101_CY)");

                // 5. Set up Parameters
                string currYear = _currentRecord.CurrYear ?? DateTime.Now.ToString("yyyy");
                string selectedMonth = _currentRecord.FinancialMonth ?? "12";
                int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
                int selectedMonthInt = int.TryParse(selectedMonth, out int parsedMonth) ? parsedMonth : 12;
                string prevYear = (currYearInt - 1).ToString();
                string prevYearPrior = (currYearInt - 2).ToString();
                string selectedPeriod = $"{selectedMonth}{currYear}";
                string prevYearPeriod = $"{selectedMonth}{prevYear}";
                string prevYearPriorPeriod = $"{selectedMonth}{prevYearPrior}";

                // Compute the fiscal year start period for cumulative (Debit/Credit/Movement) fetches.
                // The fiscal year starts on the month immediately after the financial year-end month.
                //   e.g. year-end = April (04) → fiscal start = May (05)
                //   e.g. year-end = December (12) → fiscal start = January (01) of same calendar year
                int fiscalStartMonthInt = (selectedMonthInt % 12) + 1;
                string fiscalStartMonth = fiscalStartMonthInt.ToString("D2");
                // When the fiscal start month is AFTER the year-end month numerically, the fiscal year
                // crosses a calendar year boundary and the start falls in the PRIOR calendar year.
                int cyCumulativeStartYearInt = fiscalStartMonthInt > selectedMonthInt ? currYearInt - 1 : currYearInt;
                string cyCumulativeStart = $"{fiscalStartMonth}{cyCumulativeStartYearInt}";
                string pyCumulativeStart = $"{fiscalStartMonth}{cyCumulativeStartYearInt - 1}";
                string pyCumulativeEnd   = prevYearPeriod; // $"{selectedMonth}{prevYear}"

                PXTrace.WriteInformation($"Fiscal year: {cyCumulativeStart} → {selectedPeriod} (CY), {pyCumulativeStart} → {pyCumulativeEnd} (PY)");

                // 6. Fetch all required data from the API in parallel
                var dataFetchTasks = new[]
                {
            Task.Run(() => localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, selectedPeriod)),
            Task.Run(() => localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPeriod)),
            Task.Run(() => localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYear)),
            Task.Run(() => localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, currYear)),
            Task.Run(() => localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, cyCumulativeStart, selectedPeriod)),
            Task.Run(() => localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, pyCumulativeStart, pyCumulativeEnd)),
            // [6] Opening balance data for PY: year-end of (currYear-2) → EndingBalance = PY fiscal-year opening
            Task.Run(() => localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPriorPeriod))
        };

                var results = Task.WhenAll(dataFetchTasks).Result;
                var currYearData = results[0];
                var prevYearData = results[1];
                var januaryBeginningDataPY = results[2];
                var januaryBeginningDataCY = results[3];
                var cumulativeCYData = results[4];
                var cumulativePYData = results[5];
                // prevYearData.EndingBalance     = CY fiscal-year opening balance
                // prevYearPriorData.EndingBalance = PY fiscal-year opening balance
                var prevYearPriorData = results[6];

                PXTrace.WriteInformation("📊 7 API calls completed - starting optimized placeholder processing");

                // 7. Set up user settings for analysis
                var userSettings = new UserSettings
                {
                    Branch = _currentRecord.Branch,
                    Organization = _currentRecord.Organization,
                    Ledger = _currentRecord.Ledger
                };

                // 8. Process regular placeholders using existing logic
                var regularPlaceholderRequests = localDataService.AnalyzePlaceholders(regularPlaceholders, selectedPeriod, prevYearPeriod, userSettings);
                var regularPlaceholderValues = localDataService.ProcessPlaceholdersFromFetchedData(
                    regularPlaceholderRequests, currYearData, prevYearData, januaryBeginningDataCY,
                    januaryBeginningDataPY, cumulativeCYData, cumulativePYData);

                // 9. Process exact range placeholders
                var exactRangePlaceholderValues = localDataService.ProcessAccountRangePlaceholders(
                    exactRangePlaceholders, currYearData, prevYearData, januaryBeginningDataCY,
                    januaryBeginningDataPY, cumulativeCYData, cumulativePYData);

                // 10. ✅ NEW: Process wildcard range placeholders
                var wildcardRangePlaceholderValues = localDataService.ProcessWildcardRangePlaceholders(
                    wildcardRangePlaceholders, currYearData, prevYearData, januaryBeginningDataCY,
                    januaryBeginningDataPY, cumulativeCYData, cumulativePYData);

                // 11. Combine all placeholder values
                var finalPlaceholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // ── Run ReportCalculationEngine for all linked definitions.
                // Produces PREFIX_LINECODE_CY / PREFIX_LINECODE_PY placeholders.
                // Cross-definition formulas are resolved via topological sort.
                // Engine values take priority over raw account placeholders.
                if (definitionLinks.Any())
                {
                    PXTrace.WriteInformation($"Running ReportCalculationEngine for {definitionLinks.Count} definition(s).");
                    var engine = new ReportCalculationEngine(_graph);
                    var enginePlaceholders = engine.CalculateAll(
                        definitionLinks,
                        currYearData,
                        prevYearData,
                        cyOpeningData:    prevYearData,           // BalanceType=Beginning: EndingBalance of prior year-end → CY fiscal opening
                        pyOpeningData:    prevYearPriorData,      // BalanceType=Beginning: EndingBalance of 2-years-ago year-end → PY fiscal opening
                        cyJanOpeningData: januaryBeginningDataCY, // BalanceType=JanuaryBeginning: BeginningBalance of 01-{currYear}
                        pyJanOpeningData: januaryBeginningDataPY, // BalanceType=JanuaryBeginning: BeginningBalance of 01-{prevYear}
                        cyCumulativeData: cumulativeCYData,       // BalanceType=Debit/Credit/Movement: full-year Jan–Dec CY totals
                        pyCumulativeData: cumulativePYData);      // BalanceType=Debit/Credit/Movement: full-year Jan–Dec PY totals

                    foreach (var kvp in enginePlaceholders)
                        finalPlaceholders[kvp.Key] = kvp.Value;

                    PXTrace.WriteInformation($"ReportCalculationEngine produced {enginePlaceholders.Count} placeholders.");
                }

                // ── LEGACY: Raw account-code placeholders for anything not covered by the definition.
                // Preserves backward compatibility with existing templates that use {{A10100_CY}} syntax.
                foreach (var kvp in regularPlaceholderValues)
                {
                    if (!finalPlaceholders.ContainsKey(kvp.Key))
                        finalPlaceholders[kvp.Key] = kvp.Value;
                }

                foreach (var kvp in exactRangePlaceholderValues)
                {
                    if (!finalPlaceholders.ContainsKey(kvp.Key))
                        finalPlaceholders[kvp.Key] = kvp.Value;
                }

                foreach (var kvp in wildcardRangePlaceholderValues)
                {
                    if (!finalPlaceholders.ContainsKey(kvp.Key))
                        finalPlaceholders[kvp.Key] = kvp.Value;
                }

                // Add year constants
                finalPlaceholders[Constants.CurrentYearSuffix] = currYear;
                finalPlaceholders[Constants.PreviousYearSuffix] = prevYear;

                PXTrace.WriteInformation($"📋 Final placeholder count: {finalPlaceholders.Count}");
                PXTrace.WriteInformation($"   📝 Regular: {regularPlaceholderValues.Count}");
                PXTrace.WriteInformation($"   🎯 Exact Range: {exactRangePlaceholderValues.Count}");
                PXTrace.WriteInformation($"   🌟 Wildcard Range: {wildcardRangePlaceholderValues.Count}");

                // 12. Populate Word Template
                string outputFileName = $"{_currentRecord.ReportCD}_Generated_{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
                outputPath = Path.Combine(Path.GetTempPath(), outputFileName);
                _wordTemplateService.PopulateTemplate(templatePath, outputPath, finalPlaceholders);

                // 12. Save Generated File and return its ID
                byte[] generatedFileContent = File.ReadAllBytes(outputPath);
                var fileId = _fileService.SaveGeneratedDocument(outputFileName, generatedFileContent, _currentRecord);

                totalStopwatch.Stop();
                PXTrace.WriteInformation($"Total report generation completed in {totalStopwatch.ElapsedMilliseconds} ms");

                return fileId;
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                PXTrace.WriteError($"Report generation failed after {totalStopwatch.ElapsedMilliseconds} ms for Report '{_currentRecord.ReportCD}' (ID: {_currentRecord.ReportID}): {ex.ToString()}");
                throw;
            }
            finally
            {
                // Cleanup temporary files
                if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                {
                    try
                    {
                        File.Delete(templatePath);
                        PXTrace.WriteInformation($"Cleaned up template file: {Path.GetFileName(templatePath)}");
                    }
                    catch (Exception ex)
                    {
                        PXTrace.WriteWarning($"Failed to cleanup template file '{Path.GetFileName(templatePath)}': {ex.ToString()}");
                    }
                }

                if (!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath))
                {
                    try
                    {
                        File.Delete(outputPath);
                        PXTrace.WriteInformation($"Cleaned up output file: {Path.GetFileName(outputPath)}");
                    }
                    catch (Exception ex)
                    {
                        PXTrace.WriteWarning($"Failed to cleanup output file '{Path.GetFileName(outputPath)}': {ex.ToString()}");
                    }
                }

                // Clear credential cache after report generation completes
                CredentialProvider.ClearCache();
            }
        }

    }
}
