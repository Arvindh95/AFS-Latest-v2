using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using FinancialReport.Helper;
using PX.Data;

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
                var localDataService = new FinancialDataService(_authService, tenantName);

                // 2. Get Template File
                var (templateFileContent, originalFileName) = _fileService.GetFileContentAndName(_currentRecord.Noteid, _currentRecord);
                if (templateFileContent == null || templateFileContent.Length == 0)
                    throw new PXException(Messages.TemplateFileIsEmpty);

                string extension = Path.GetExtension(originalFileName) ?? ".docx";
                templatePath = Path.Combine(Path.GetTempPath(), $"{_currentRecord.ReportCD}_Template{extension}");
                File.WriteAllBytes(templatePath, templateFileContent);

                // 3. Extract Placeholders
                List<string> extractedKeys = _wordTemplateService.ExtractPlaceholderKeys(templatePath);

                // 4. Set up Parameters
                string currYear = _currentRecord.CurrYear ?? DateTime.Now.ToString("yyyy");
                string selectedMonth = _currentRecord.FinancialMonth ?? "12";
                int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
                string prevYear = (currYearInt - 1).ToString();
                string selectedPeriod = $"{selectedMonth}{currYear}";
                string prevYearPeriod = $"{selectedMonth}{prevYear}";

                // 5. Fetch all required data from the API in parallel
                var dataFetchTasks = new[]
                {
                    Task.Run(() => localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, selectedPeriod)),
                    Task.Run(() => localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPeriod)),
                    Task.Run(() => localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYear)),
                    Task.Run(() => localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, currYear)),
                    Task.Run(() => localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, $"01{currYear}", selectedPeriod)),
                    Task.Run(() => localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, $"01{prevYear}", $"12{prevYear}"))
                };

                // Wait for all tasks to complete
                var results = Task.WhenAll(dataFetchTasks).Result;

                // Assign results
                var currYearData = results[0];
                var prevYearData = results[1];
                var januaryBeginningDataPY = results[2];
                var januaryBeginningDataCY = results[3];
                var cumulativeCYData = results[4];
                var cumulativePYData = results[5];

                // 6. Map Data to Placeholders
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Split placeholders into standard and prefix-based
                var standardPlaceholders = extractedKeys.Where(key => !localDataService.HasPrefixPattern(key)).ToList();
                var prefixPlaceholders = extractedKeys.Where(key => localDataService.HasPrefixPattern(key)).ToList();

                PXTrace.WriteInformation($"Found {standardPlaceholders.Count} standard placeholders and {prefixPlaceholders.Count} prefix placeholders");

                // Process standard placeholders with existing logic
                Dictionary<string, string> standardResults = localDataService.BuildSmartPlaceholderMapFromKeys(
                    standardPlaceholders, currYearData, prevYearData, januaryBeginningDataCY, januaryBeginningDataPY, cumulativeCYData, cumulativePYData
                );

                // Process prefix placeholders with new logic
                var userSettings = new UserSettings
                {
                    Branch = _currentRecord.Branch,
                    Organization = _currentRecord.Organization,
                    Ledger = _currentRecord.Ledger
                };

                Dictionary<string, string> prefixResults = localDataService.ProcessPrefixPlaceholders(
                    prefixPlaceholders, selectedPeriod, prevYearPeriod, userSettings
                );

                // Merge results
                Dictionary<string, string> finalPlaceholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in standardResults)
                    finalPlaceholders[kvp.Key] = kvp.Value;
                foreach (var kvp in prefixResults)
                    finalPlaceholders[kvp.Key] = kvp.Value;

                sw.Stop();
                PXTrace.WriteInformation($"Placeholder mapping completed in {sw.ElapsedMilliseconds} ms");

                finalPlaceholders[Constants.CurrentYearSuffix] = currYear;
                finalPlaceholders[Constants.PreviousYearSuffix] = prevYear;

                // 7. Populate Word Template
                string outputFileName = $"{_currentRecord.ReportCD}_Generated_{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
                outputPath = Path.Combine(Path.GetTempPath(), outputFileName);
                _wordTemplateService.PopulateTemplate(templatePath, outputPath, finalPlaceholders);

                // 8. Save Generated File and return its ID
                byte[] generatedFileContent = File.ReadAllBytes(outputPath);
                var fileId = _fileService.SaveGeneratedDocument(outputFileName, generatedFileContent, _currentRecord);

                totalStopwatch.Stop();
                PXTrace.WriteInformation($"Total report generation completed in {totalStopwatch.ElapsedMilliseconds} ms");

                return fileId;
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                PXTrace.WriteError($"Report generation failed after {totalStopwatch.ElapsedMilliseconds} ms: {ex.Message}");
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
                        PXTrace.WriteWarning($"Failed to cleanup template file: {ex.Message}");
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
                        PXTrace.WriteWarning($"Failed to cleanup output file: {ex.Message}");
                    }
                }
            }
        }

    }
}
