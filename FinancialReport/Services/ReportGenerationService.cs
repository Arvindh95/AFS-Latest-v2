using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// Executes the end-to-end report generation process.
        /// </summary>
        /// <returns>The GUID of the newly generated and saved file.</returns>
        public Guid Execute()
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
            string templatePath = Path.Combine(Path.GetTempPath(), $"{_currentRecord.ReportCD}_Template{extension}");
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

            // 5. Fetch all required data from the API
            var currYearData = localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, selectedPeriod);
            var prevYearData = localDataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPeriod);
            var januaryBeginningDataPY = localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYear);
            var januaryBeginningDataCY = localDataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, currYear);
            var cumulativeCYData = localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, $"01{currYear}", selectedPeriod);
            var cumulativePYData = localDataService.FetchRangeApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, $"01{prevYear}", $"12{prevYear}");

            // 6. Map Data to Placeholders
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Dictionary<string, string> finalPlaceholders = localDataService.BuildSmartPlaceholderMapFromKeys(
                extractedKeys, currYearData, prevYearData, januaryBeginningDataCY, januaryBeginningDataPY, cumulativeCYData, cumulativePYData
            );
            sw.Stop();
            PXTrace.WriteInformation($"Placeholder mapping completed in {sw.ElapsedMilliseconds} ms");

            finalPlaceholders[Constants.CurrentYearSuffix] = currYear;
            finalPlaceholders[Constants.PreviousYearSuffix] = prevYear;

            // 7. Populate Word Template
            string outputFileName = $"{_currentRecord.ReportCD}_Generated_{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
            string outputPath = Path.Combine(Path.GetTempPath(), outputFileName);
            _wordTemplateService.PopulateTemplate(templatePath, outputPath, finalPlaceholders);

            // 8. Save Generated File and return its ID
            byte[] generatedFileContent = File.ReadAllBytes(outputPath);
            return _fileService.SaveGeneratedDocument(outputFileName, generatedFileContent, _currentRecord);
        }
    }
}
