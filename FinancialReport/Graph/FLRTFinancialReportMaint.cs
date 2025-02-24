using System;
using PX.Data;
using PX.Data.BQL.Fluent;
using System.Collections.Generic;
using System.IO;
using PX.SM; // For UploadFileMaintenance and NoteDoc
using System.Linq;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json; // For JsonConvert
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Configuration;
using System.Collections;
using PX.Objects.GL;
using static PX.Objects.GL.AccountEntityType;
using FinancialReport.Services;
using static FinancialReport.FLRTFinancialReport;

namespace FinancialReport
{
    public class FLRTFinancialReportMaint : PXGraph<FLRTFinancialReportMaint>
    {

        #region Services     

        // Handles Authentication with Acumatica API
        //private readonly AuthService _authService;

        // Fetches financial data from Acumatica API
        //private readonly FinancialDataService _dataService;

        // Manages file operations such as fetching templates & saving reports
        private readonly FileService _fileService;

        // Handles Word template population and formatting
        private readonly WordTemplateService _wordTemplateService;

        #endregion

        #region Configuration & Utility Methods

        private string GetConfigValue(string key)
        {
            return ConfigurationManager.AppSettings[key] ?? throw new PXException(Messages.MissingConfig);
        }
        private string _baseUrl => GetConfigValue("Acumatica.BaseUrl");

        public FLRTFinancialReportMaint()
        {
            //_authService = new AuthService(_baseUrl, GetConfigValue("Acumatica.ClientId"), GetConfigValue("Acumatica.ClientSecret"), GetConfigValue("Acumatica.Username"), GetConfigValue("Acumatica.Password"));
            //_dataService = new FinancialDataService(_baseUrl, _authService);
            _fileService = new FileService(this);
            _wordTemplateService = new WordTemplateService();
        }

        #endregion

        #region Utility Methods

        private List<string> GetAccountNumbers()
        {
            var accountNumbers = PXSelect<Account>
            .Select(this)
                                 .RowCast<Account>()
                                 .Select(a => a.AccountCD.Trim())
                                 .ToList();

            // Log the fetched account numbers
            PXTrace.WriteInformation("Fetched Account Numbers:");
            foreach (var account in accountNumbers)
            {
                PXTrace.WriteInformation($"AccountCD: {account}");
            }

            return accountNumbers;
        }

        private string FormatNumber(string value)
        {
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N0"); // Formats as ###,###.00
            }
            return value; // Return original if parsing fails
        }

        #endregion


        public SelectFrom<FLRTFinancialReport>.View FinancialReport;
               
    
        #region Events and Actions

        protected void FLRTFinancialReport_Selected_FieldUpdated(PXCache cache, PXFieldUpdatedEventArgs e)
        {
            var current = (FLRTFinancialReport)e.Row;
            if (current?.Selected != true) return;

            // Unselect all other records
            foreach (FLRTFinancialReport item in FinancialReport.Cache.Cached)
            {
                if (item.ReportID != current.ReportID && item.Selected == true)
                {
                    item.Selected = false;
                }
            }

        }

        protected void FLRTFinancialReport_Branch_FieldUpdated(PXCache cache, PXFieldUpdatedEventArgs e)
        {
            //No need to implement this method
        }

        protected void FLRTFinancialReport_Ledger_FieldUpdated(PXCache cache, PXFieldUpdatedEventArgs e)
        {
            var row = (FLRTFinancialReport)e.Row;
            if (row != null && string.IsNullOrEmpty(row.Ledger))
            {
                PXTrace.WriteError("Ledger cannot be empty.");
                throw new PXException(Messages.PleaseSelectALedger);
            }
        }

        protected void FLRTFinancialReport_CurrYear_FieldUpdated(PXCache cache, PXFieldUpdatedEventArgs e)
        {
            var row = (FLRTFinancialReport)e.Row;
            if (row != null)
            {
                PXTrace.WriteInformation($"CurrYear Updated to: {row.CurrYear}");
            }
        }

        #endregion

        public PXSave<FLRTFinancialReport> Save;
        public PXCancel<FLRTFinancialReport> Cancel;



        #region Business Logic

        public PXAction<FLRTFinancialReport> GenerateReport;
        [PXButton(CommitChanges = false)]
        [PXUIField(DisplayName = "Generate Report")]
        protected virtual IEnumerable generateReport(PXAdapter adapter)
        {

            // Get the current selected record
            FLRTFinancialReport selectedRecord = FinancialReport.Cache.Cached
                                .Cast<FLRTFinancialReport>()
                                .FirstOrDefault(item => item.Selected == true);



            if (selectedRecord == null)
                throw new PXException(Messages.PleaseSelectTemplate);

            if (selectedRecord.Status == ReportStatus.InProgress)
            {
                throw new PXException(Messages.FileGenerationInProgress);
            }

            if (selectedRecord.Noteid == null)
                throw new PXException(Messages.TemplateHasNoFiles);

            // Retrieve CompanyID from the database
            int? companyID = GetCompanyIDFromDB(selectedRecord.ReportID);
            PXTrace.WriteInformation($"CompanyID retrieved: {companyID}");

            // Map CompanyID to a tenant name
            string tenantName = MapCompanyIDToTenantName(companyID);
            PXTrace.WriteInformation($"Mapped Tenant Name: {tenantName}");

            // Fetch credentials based on the tenant
            AcumaticaCredentials tenantCredentials = CredentialProvider.GetCredentials(tenantName);
            PXTrace.WriteInformation($"API Credentials: ClientId={tenantCredentials.ClientId}, Username={tenantCredentials.Username}");

            // Initialize AuthService with these credentials
            AuthService authService = new AuthService(
                                        _baseUrl,
                                        tenantCredentials.ClientId,
                                        tenantCredentials.ClientSecret,
                                        tenantCredentials.Username,
                                        tenantCredentials.Password
            );

            string token;
            try
            {
                token = authService.AuthenticateAndGetToken();
                PXTrace.WriteInformation($"Successfully authenticated for {tenantName}. Token: {token}");
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Authentication failed for {tenantName}: {ex.Message}");
                throw new PXException(Messages.InvalidCredentials);
            }

            PXTrace.WriteInformation($"Using API Credentials for {tenantName}");

            selectedRecord.Status = ReportStatus.InProgress;
            // Persist the record and ensure its state is stored in the database.
            selectedRecord.Selected = true;
            
            FinancialReport.Update(selectedRecord);
            Actions.PressSave();

            int? reportID = selectedRecord.ReportID;
            if (reportID == null)
                throw new PXException(Messages.PleaseSelectTemplate);

            // Start the background operation.
            PXLongOperation.StartOperation(this, () =>
            {
                FLRTFinancialReportMaint reportGraph = PXGraph.CreateInstance<FLRTFinancialReportMaint>();
                // Retrieve the record from the database using ReportID.
                FLRTFinancialReport dbRecord = PXSelect<FLRTFinancialReport,
                    Where<FLRTFinancialReport.reportID, Equal<Required<FLRTFinancialReport.reportID>>>>
                    .Select(reportGraph, reportID);

                if (dbRecord == null)
                    throw new PXException(Messages.FailedToRetrieveFile);
                if (dbRecord.Noteid == null)
                    throw new PXException(Messages.TemplateHasNoFiles);

                // Set the record explicitly for the background graph.
                reportGraph.FinancialReport.Current = dbRecord;

                try
                {
                    // Generate the report.
                    reportGraph.GenerateFinancialReport(token);
                    // Log or store the file ID so the UI can later display a download link.
                    PXTrace.WriteInformation("Report has been generated and is ready for download.");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Report generation failed: {ex.Message}");
                    // Set status to "Failed" if an error occurs
                    dbRecord.Status = ReportStatus.Failed;
                }
            });

            return adapter.Get();
        }

        private void GenerateFinancialReport(string authToken)
        {
            AuthService localAuthService = null;
            try
            {
                FLRTFinancialReport currentRecord = FinancialReport.Current;
                if (currentRecord == null)
                    throw new PXException(Messages.PleaseSelectTemplate);

                if (currentRecord.Noteid == null)
                    throw new PXException(Messages.TemplateHasNoFiles);

                int? companyID = GetCompanyIDFromDB(currentRecord.ReportID);
                string tenantName = MapCompanyIDToTenantName(companyID);
                PXTrace.WriteInformation($"Mapped Tenant Name: {tenantName}");

                AcumaticaCredentials creds = CredentialProvider.GetCredentials(tenantName);
                localAuthService = new AuthService(_baseUrl, creds.ClientId, creds.ClientSecret, creds.Username, creds.Password);
                localAuthService.SetToken(authToken);

                var localDataService = new FinancialDataService(_baseUrl, localAuthService, tenantName);

                string branch = string.IsNullOrEmpty(currentRecord.Branch) ? currentRecord.Organization : currentRecord.Branch;
                if (string.IsNullOrEmpty(branch))
                    throw new PXException(Messages.PleaseSelectABranch);
                if (string.IsNullOrEmpty(currentRecord.Ledger))
                    throw new PXException(Messages.PleaseSelectALedger);

                byte[] templateFileContent = GetFileContent(currentRecord.Noteid);
                if (templateFileContent == null || templateFileContent.Length == 0)
                    throw new PXException(Messages.TemplateFileIsEmpty);

                string templatePath = Path.Combine(Path.GetTempPath(), $"{currentRecord.ReportCD}_Template.docx");
                File.WriteAllBytes(templatePath, templateFileContent);

                string uniqueFileName = $"{currentRecord.ReportCD}_Generated_{DateTime.Now:yyyyMMdd_HHmmssfff}.docx";
                string outputPath = Path.Combine(Path.GetTempPath(), uniqueFileName);

                string currYear = currentRecord.CurrYear ?? DateTime.Now.ToString("yyyy");
                string selectedMonth = currentRecord.FinancialMonth ?? "12";
                string selectedPeriod = $"{selectedMonth}{currYear}";
                int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
                string prevYear = (currYearInt - 1).ToString();
                string prevYearPeriod = selectedMonth + prevYear;

                PXTrace.WriteInformation($"Fetching data for Period: {selectedPeriod}, Branch: {branch}, Ledger: {currentRecord.Ledger}");
                var currYearData = localDataService.FetchAllApiData(branch, currentRecord.Ledger, selectedPeriod) ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching data for Prev Year Period: {prevYearPeriod}, Branch: {branch}, Ledger: {currentRecord.Ledger}");
                var prevYearData = localDataService.FetchAllApiData(branch, currentRecord.Ledger, prevYearPeriod) ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching January {prevYear} Beginning Balance, Branch: {branch}, Ledger: {currentRecord.Ledger}");
                var januaryBeginningData = localDataService.FetchJanuaryBeginningBalance(branch, currentRecord.Ledger, prevYear) ?? new FinancialApiData();

                var placeholderData = GetPlaceholderData(currYearData, prevYearData, januaryBeginningData); // Updated to include January data
                _wordTemplateService.PopulateTemplate(templatePath, outputPath, placeholderData);

                byte[] generatedFileContent = File.ReadAllBytes(outputPath);
                Guid fileID = SaveGeneratedDocument(uniqueFileName, generatedFileContent, currentRecord);

                PXTrace.WriteInformation("Report generated successfully.");
                currentRecord.GeneratedFileID = fileID;
                currentRecord.Status = FLRTFinancialReport.ReportStatus.Completed;
                FinancialReport.Update(currentRecord);
                Actions.PressSave();
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Report generation failed: {ex.Message}");
                var currentRecord = FinancialReport.Current;
                if (currentRecord != null)
                {
                    currentRecord.Status = FLRTFinancialReport.ReportStatus.Failed;
                    FinancialReport.Update(currentRecord);
                    Actions.PressSave();
                }
                throw new PXException(Messages.FailedToRetrieveFile);
            }
            finally
            {
                localAuthService?.Logout();
            }
        }

        #endregion


        #region Supporting Methods

        private Dictionary<string, string> GetPlaceholderData(FinancialApiData currYearData, FinancialApiData prevYearData, FinancialApiData januaryBeginningData)
        {
            var selectedRecord = FinancialReport.Current;
            string selectedMonth = selectedRecord?.FinancialMonth ?? "12";
            string currYear = selectedRecord?.CurrYear ?? DateTime.Now.ToString("yyyy");

            if (string.IsNullOrEmpty(currYear))
                throw new PXException(Messages.CurrentYearNotSpecified);

            int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
            string prevYear = (currYearInt - 1).ToString();

            int monthNumber = int.Parse(selectedMonth);
            string monthName = new DateTime(1, monthNumber, 1).ToString("MMMM");

            var placeholderData = new Dictionary<string, string>
            {
                { "{{financialMonth}}", monthName },
                { "{{branchName}}", "Censof-Test" },
                { "{{agencyname}}", "Suruhanjaya Tenaga" },
                { "{{name1}}", "Dato' Khir bin Osman" },
                { "{{name2}}", "Dato' Shaik Hussein bin Anggota" },
                { "{{agencyName}}", "Suruhanjaya Tenaga" },
                { "{{CY}}", currYear },
                { "{{currmonth}}", DateTime.Now.ToString("MMMM") },
                { "{{PY}}", prevYear }
            };

            foreach (var account in currYearData.AccountData)
            {
                placeholderData[$"{{{{{account.Key}_CY}}}}"] = FormatNumber(account.Value.EndingBalance);
                placeholderData[$"{{{{description_{account.Key}_CY}}}}"] = account.Value.Description;
            }

            foreach (var account in prevYearData.AccountData)
            {
                placeholderData[$"{{{{{account.Key}_PY}}}}"] = FormatNumber(account.Value.EndingBalance);
            }

            foreach (var account in januaryBeginningData.AccountData)
            {
                placeholderData[$"{{{{{account.Key}_Jan1_PY}}}}"] = FormatNumber(account.Value.EndingBalance); // e.g., {{A10999_Jan1_2023}}
            }

            return placeholderData;
        }

        private byte[] GetFileContent(Guid? noteID)
        {
            return _fileService.GetFileContent(noteID);
        }


        private Guid SaveGeneratedDocument(string fileName, byte[] fileContent, FLRTFinancialReport currentRecord)
        {
            return _fileService.SaveGeneratedDocument(fileName, fileContent, currentRecord);
        }

        #endregion


        #region Download Method

        public PXAction<FLRTFinancialReport> DownloadReport;
        [PXButton]
        [PXUIField(DisplayName = "Download Report", MapEnableRights = PXCacheRights.Select, Visible = true)]
        protected virtual IEnumerable downloadReport(PXAdapter adapter)
        {
            // Get the current record from the grid.
            FLRTFinancialReport currentRecord = FinancialReport.Current;
            if (currentRecord == null)
            {
                throw new PXException(Messages.NoRecordIsSelected);
            }

            if (currentRecord.GeneratedFileID == null)
            {
                throw new PXException(Messages.NoGeneratedFile);
            }

            // Trigger the file download using PXRedirectToFileException.
            throw new PXRedirectToFileException(currentRecord.GeneratedFileID.Value, 1, false);
        }


        #endregion

        private int? GetCompanyIDFromDB(int? reportID)
        {
            if (reportID == null) return null;

            using (PXTransactionScope ts = new PXTransactionScope())
            {
                var result = PXDatabase.SelectSingle<FLRTFinancialReport>(
                    new PXDataField("CompanyID"), // Column name in DB
                    new PXDataFieldValue("ReportID", reportID) // Filtering by ReportID
                );

                if (result != null)
                {
                    return (int?)result.GetInt32(0);
                }
            }

            return null;
        }

        private string MapCompanyIDToTenantName(int? companyID)
        {
            if (companyID == null)
                return string.Empty;

            string mappingConfig = ConfigurationManager.AppSettings["TenantMapping"];

            if (string.IsNullOrEmpty(mappingConfig))
            {
                PXTrace.WriteError("TenantMapping is missing from Web.config.");
                throw new PXException(Messages.TenantMissingFromConfig);
            }

            var tenantMap = mappingConfig.Split(',')
                .Select(entry => entry.Split(':'))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => int.Parse(parts[0]), parts => parts[1]);

            if (!tenantMap.TryGetValue(companyID.Value, out string tenant))
            {
                PXTrace.WriteError($"No tenant found for CompanyID: {companyID}");
                throw new PXException(Messages.TenantMissingFromConfig);
            }

            return tenant;
        }



    }
}