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
                //return number.ToString("N0"); // Formats as ###,###.00                                             
                //return number.ToString("#,##0;(#,##0)");// Custom format: positive numbers as usual, negative numbers in parentheses
                //number = Math.Abs(number); // Convert to absolute value (removes negative sign)
                //if (number == 0)
                //{
                //    return "(0)"; // Replace zero values with (0)
                //}
                return number.ToString("#,##0");
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
                int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
                string prevYear = (currYearInt - 1).ToString();

                string selectedPeriod = $"{selectedMonth}{currYear}";
                string prevYearPeriod = $"{selectedMonth}{prevYear}";

                // ✅ Fetch CY Data
                PXTrace.WriteInformation($"Fetching data for Period: {selectedPeriod}, Branch: {branch}, Ledger: {currentRecord.Ledger}");
                var currYearData = localDataService.FetchAllApiData(branch, currentRecord.Ledger, selectedPeriod) ?? new FinancialApiData();

                // ✅ Fetch PY Data
                PXTrace.WriteInformation($"Fetching data for Prev Year Period: {prevYearPeriod}, Branch: {branch}, Ledger: {currentRecord.Ledger}");
                var prevYearData = localDataService.FetchAllApiData(branch, currentRecord.Ledger, prevYearPeriod) ?? new FinancialApiData();

                // ✅ Fetch January Beginning Balance
                PXTrace.WriteInformation($"Fetching January {prevYear} Beginning Balance, Branch: {branch}, Ledger: {currentRecord.Ledger}");
                var januaryBeginningDataPY = localDataService.FetchJanuaryBeginningBalance(branch, currentRecord.Ledger, prevYear) ?? new FinancialApiData();
                var januaryBeginningDataCY = localDataService.FetchJanuaryBeginningBalance(branch, currentRecord.Ledger, currYear) ?? new FinancialApiData();

                // ✅ Fetch Cumulative Debit/Credit Data
                string fromPeriodCY = "01" + currYear;
                string toPeriodCY = selectedMonth + currYear;
                PXTrace.WriteInformation($"Fetching CY cumulative data from {fromPeriodCY} to {toPeriodCY}");
                var cumulativeCYData = localDataService.FetchRangeApiData(branch, currentRecord.Ledger, fromPeriodCY, toPeriodCY);

                string fromPeriodPY = "01" + prevYear;
                string toPeriodPY = "12" + prevYear;
                PXTrace.WriteInformation($"Fetching PY cumulative data from {fromPeriodPY} to {toPeriodPY}");
                var cumulativePYData = localDataService.FetchRangeApiData(branch, currentRecord.Ledger, fromPeriodPY, toPeriodPY);

                // ✅ Store all data in placeholders
                var placeholderData = GetPlaceholderData(currYearData, prevYearData, januaryBeginningDataPY, januaryBeginningDataCY, cumulativeCYData, cumulativePYData);

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

        private Dictionary<string, string> GetPlaceholderData(FinancialApiData currYearData, FinancialApiData prevYearData,
                                                      FinancialApiData januaryBeginningDataPY, FinancialApiData januaryBeginningDataCY,
                                                      FinancialApiData cumulativeCYData, FinancialApiData cumulativePYData)
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

            // ✅ Initialize the summation variables
            decimal sumB_CY = 0m, sumB_PY = 0m;
            decimal sumH_CY = 0m, sumH_PY = 0m;

            // ✅ Aggregate Cumulative Data by Prefix
            var cumulativeCYByPrefix = new Dictionary<string, decimal>();
            var cumulativePYByPrefix = new Dictionary<string, decimal>();
            var beginningCYByPrefix = new Dictionary<string, decimal>(); // For Beginning Balance CY
            var beginningPYByPrefix = new Dictionary<string, decimal>(); // For Beginning Balance PY


            // ✅ Aggregate Cumulative Data by First 4 Prefix
            var cumulativeCYBy4Prefix = new Dictionary<string, decimal>();
            var cumulativePYBy4Prefix = new Dictionary<string, decimal>();
            var beginningCYBy4Prefix = new Dictionary<string, decimal>(); // For Beginning Balance CY
            var beginningPYBy4Prefix = new Dictionary<string, decimal>(); // For Beginning Balance PY

            // ✅ Aggregate Cumulative Data by Prefix (3-character)
            var debitCYByPrefix = new Dictionary<string, decimal>();  // Debit for CY
            var creditCYByPrefix = new Dictionary<string, decimal>(); // Credit for CY
            var debitPYByPrefix = new Dictionary<string, decimal>();  // Debit for PY
            var creditPYByPrefix = new Dictionary<string, decimal>(); // Credit for PY

            // ✅ Aggregate Cumulative Data by First 4 Prefix (4-character)
            var debitCYBy4Prefix = new Dictionary<string, decimal>();
            var creditCYBy4Prefix = new Dictionary<string, decimal>();
            var debitPYBy4Prefix = new Dictionary<string, decimal>();
            var creditPYBy4Prefix = new Dictionary<string, decimal>();

            var placeholderData = new Dictionary<string, string>
            {
                { "{{financialMonth}}", monthName },
                { "{{testValue}}", "Success" },
                { "{{CY}}", currYear },
                { "{{currmonth}}", DateTime.Now.ToString("MMMM") },
                { "{{PY}}", prevYear }
            };

            // ✅ Store Current Year (CY) Ending Balance & Description
            foreach (var account in currYearData.AccountData)
            {
                string accountId = account.Key;
                var data = account.Value;
                placeholderData[$"{{{{{accountId}_CY}}}}"] = FormatNumber(data.EndingBalance.ToString());
                placeholderData[$"{{{{description_{accountId}_CY}}}}"] = data.Description;

                if (accountId.StartsWith("B"))
                {
                    sumB_CY += data.EndingBalance;
                }
                if (accountId.StartsWith("H"))
                {
                    sumH_CY += data.EndingBalance;
                }
            }

            // ✅ Store Previous Year (PY) Ending Balance
            foreach (var account in prevYearData.AccountData)
            {
                string accountId = account.Key;
                var data = account.Value;
                placeholderData[$"{{{{{accountId}_PY}}}}"] = FormatNumber(data.EndingBalance.ToString());

                // ✅ Sum up PY balances for accounts starting with 'B' and 'H'
                if (accountId.StartsWith("B"))
                {
                    sumB_PY += data.EndingBalance;
                }
                if (accountId.StartsWith("H"))
                {
                    sumH_PY += data.EndingBalance;
                }
            }

            // ✅ Store January 1st Balance for Previous Year (Jan1_PY)
            foreach (var account in januaryBeginningDataPY.AccountData)
            {
                string accountId = account.Key;
                var data = account.Value;
                placeholderData[$"{{{{{accountId}_Jan1_PY}}}}"] = FormatNumber(data.EndingBalance.ToString());
            }

            // ✅ Store January 1st Balance for Current Year (Jan1_CY)
            foreach (var account in januaryBeginningDataCY.AccountData)
            {
                string accountId = account.Key;
                var data = account.Value;
                placeholderData[$"{{{{{accountId}_Jan1_CY}}}}"] = FormatNumber(data.EndingBalance.ToString());
            }

            // ✅ Store Cumulative Debit & Credit for Current Year
            foreach (var account in cumulativeCYData.AccountData)
            {
                string accountId = account.Key;
                var data = account.Value;
                placeholderData[$"{{{{{accountId}_debit_CY}}}}"] = FormatNumber(data.Debit.ToString());
                placeholderData[$"{{{{{accountId}_credit_CY}}}}"] = FormatNumber(data.Credit.ToString());
            }

            // ✅ Store Cumulative Debit & Credit for Previous Year
            foreach (var account in cumulativePYData.AccountData)
            {
                string accountId = account.Key;
                var data = account.Value;
                placeholderData[$"{{{{{accountId}_debit_PY}}}}"] = FormatNumber(data.Debit.ToString());
                placeholderData[$"{{{{{accountId}_credit_PY}}}}"] = FormatNumber(data.Credit.ToString());
            }

            // Process CY Data (3 strings)
            foreach (var account in cumulativeCYData.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 3); // Extract first three characters (e.g., "A11")

                if (!cumulativeCYByPrefix.ContainsKey(accountPrefix))
                    cumulativeCYByPrefix[accountPrefix] = 0m;
                cumulativeCYByPrefix[accountPrefix] += account.Value.EndingBalance;

                
            }

            // Process CY Data (January 1st Beginning Balances)
            foreach (var account in januaryBeginningDataCY.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 3); // Extract first four characters (e.g., "A110")

                // Sum Beginning Balance
                if (!beginningCYByPrefix.ContainsKey(accountPrefix))
                    beginningCYByPrefix[accountPrefix] = 0m;
                beginningCYByPrefix[accountPrefix] += account.Value.BeginningBalance;
            }

            // Process PY Data (3 strings)
            foreach (var account in cumulativePYData.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 3); // Extract first three characters (e.g., "A11")

                if (!cumulativePYByPrefix.ContainsKey(accountPrefix))
                    cumulativePYByPrefix[accountPrefix] = 0m;
                cumulativePYByPrefix[accountPrefix] += account.Value.EndingBalance;

            }

            // Process PY Data (January 1st Beginning Balances)
            foreach (var account in januaryBeginningDataPY.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 3); // Extract first four characters (e.g., "A110")

                if (!beginningPYByPrefix.ContainsKey(accountPrefix))
                    beginningPYByPrefix[accountPrefix] = 0m;
                beginningPYByPrefix[accountPrefix] += account.Value.BeginningBalance;
            }

            // ✅ Store cumulative sums in placeholders
            foreach (var prefix in cumulativeCYByPrefix.Keys)
            {
                placeholderData[$"{{{{Sum_{prefix}_CY}}}}"] = FormatNumber(cumulativeCYByPrefix[prefix].ToString());
            }
            foreach (var prefix in cumulativePYByPrefix.Keys)
            {
                placeholderData[$"{{{{Sum_{prefix}_PY}}}}"] = FormatNumber(cumulativePYByPrefix[prefix].ToString());
            }

            // ✅ Store Beginning Balance sums in placeholders
            foreach (var prefix in beginningCYByPrefix.Keys)
            {
                placeholderData[$"{{{{BegSum_{prefix}_CY}}}}"] = FormatNumber(beginningCYByPrefix[prefix].ToString());
            }
            foreach (var prefix in beginningPYByPrefix.Keys)
            {
                placeholderData[$"{{{{BegSum_{prefix}_PY}}}}"] = FormatNumber(beginningPYByPrefix[prefix].ToString());
            }

            // Process CY Data (4 strings)
            foreach (var account in cumulativeCYData.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 4); // Extract first four characters (e.g., "A110")

                if (!cumulativeCYBy4Prefix.ContainsKey(accountPrefix))
                    cumulativeCYBy4Prefix[accountPrefix] = 0m;
                cumulativeCYBy4Prefix[accountPrefix] += account.Value.EndingBalance;
               
            }

            // Process CY Data (January 1st Beginning Balances)
            foreach (var account in januaryBeginningDataCY.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 4); // Extract first four characters (e.g., "A110")

                // Sum Beginning Balance
                if (!beginningCYBy4Prefix.ContainsKey(accountPrefix))
                    beginningCYBy4Prefix[accountPrefix] = 0m;
                beginningCYBy4Prefix[accountPrefix] += account.Value.BeginningBalance;
            }

            // Process PY Data (4 strings)
            foreach (var account in cumulativePYData.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 4); // Extract first four characters (e.g., "A110")

                if (!cumulativePYBy4Prefix.ContainsKey(accountPrefix))
                    cumulativePYBy4Prefix[accountPrefix] = 0m;
                cumulativePYBy4Prefix[accountPrefix] += account.Value.EndingBalance;

            }

            // Process PY Data (January 1st Beginning Balances)
            foreach (var account in januaryBeginningDataPY.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 4); // Extract first four characters (e.g., "A110")

                if (!beginningPYBy4Prefix.ContainsKey(accountPrefix))
                    beginningPYBy4Prefix[accountPrefix] = 0m;
                beginningPYBy4Prefix[accountPrefix] += account.Value.BeginningBalance;
            }

            // ✅ Store cumulative sums in placeholders
            foreach (var prefix in cumulativeCYBy4Prefix.Keys)
            {
                placeholderData[$"{{{{Sum4_{prefix}_CY}}}}"] = FormatNumber(cumulativeCYBy4Prefix[prefix].ToString());
            }
            foreach (var prefix in cumulativePYBy4Prefix.Keys)
            {
                placeholderData[$"{{{{Sum4_{prefix}_PY}}}}"] = FormatNumber(cumulativePYBy4Prefix[prefix].ToString());
            }

            foreach (var prefix in beginningCYBy4Prefix.Keys)
            {
                placeholderData[$"{{{{BegSum4_{prefix}_CY}}}}"] = FormatNumber(beginningCYBy4Prefix[prefix].ToString());
            }
            foreach (var prefix in beginningPYBy4Prefix.Keys)
            {
                placeholderData[$"{{{{BegSum4_{prefix}_PY}}}}"] = FormatNumber(beginningPYBy4Prefix[prefix].ToString());
            }

            // Process CY Data (3-character prefix) Debit/Credit
            foreach (var account in cumulativeCYData.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 3); // Extract first 3 characters (e.g., "A11")

                // Sum Debit
                if (!debitCYByPrefix.ContainsKey(accountPrefix))
                    debitCYByPrefix[accountPrefix] = 0m;
                debitCYByPrefix[accountPrefix] += account.Value.Debit;

                // Sum Credit
                if (!creditCYByPrefix.ContainsKey(accountPrefix))
                    creditCYByPrefix[accountPrefix] = 0m;
                creditCYByPrefix[accountPrefix] += account.Value.Credit;
            }

            // Process CY Data (4-character prefix) Debit/Credit
            foreach (var account in cumulativeCYData.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 4); // Extract first 4 characters (e.g., "A110")

                // Sum Debit
                if (!debitCYBy4Prefix.ContainsKey(accountPrefix))
                    debitCYBy4Prefix[accountPrefix] = 0m;
                debitCYBy4Prefix[accountPrefix] += account.Value.Debit;

                // Sum Credit
                if (!creditCYBy4Prefix.ContainsKey(accountPrefix))
                    creditCYBy4Prefix[accountPrefix] = 0m;
                creditCYBy4Prefix[accountPrefix] += account.Value.Credit;
            }

            // Process PY Data (3-character prefix) Debit/Credit
            foreach (var account in cumulativePYData.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 3); // Extract first 3 characters (e.g., "A11")

                // Sum Debit
                if (!debitPYByPrefix.ContainsKey(accountPrefix))
                    debitPYByPrefix[accountPrefix] = 0m;
                debitPYByPrefix[accountPrefix] += account.Value.Debit;

                // Sum Credit
                if (!creditPYByPrefix.ContainsKey(accountPrefix))
                    creditPYByPrefix[accountPrefix] = 0m;
                creditPYByPrefix[accountPrefix] += account.Value.Credit;
            }

            // Process PY Data (4-character prefix) Debit/Credit
            foreach (var account in cumulativePYData.AccountData)
            {
                string accountId = account.Key;
                string accountPrefix = accountId.Substring(0, 4); // Extract first 4 characters (e.g., "A110")

                // Sum Debit
                if (!debitPYBy4Prefix.ContainsKey(accountPrefix))
                    debitPYBy4Prefix[accountPrefix] = 0m;
                debitPYBy4Prefix[accountPrefix] += account.Value.Debit;

                // Sum Credit
                if (!creditPYBy4Prefix.ContainsKey(accountPrefix))
                    creditPYBy4Prefix[accountPrefix] = 0m;
                creditPYBy4Prefix[accountPrefix] += account.Value.Credit;
            }

            // ✅ Store summed debit & credit for 3-character prefixes
            foreach (var prefix in debitCYByPrefix.Keys)
            {
                placeholderData[$"{{{{DebitSum_{prefix}_CY}}}}"] = FormatNumber(debitCYByPrefix[prefix].ToString());
                placeholderData[$"{{{{CreditSum_{prefix}_CY}}}}"] = FormatNumber(creditCYByPrefix[prefix].ToString());
            }
            foreach (var prefix in debitPYByPrefix.Keys)
            {
                placeholderData[$"{{{{DebitSum_{prefix}_PY}}}}"] = FormatNumber(debitPYByPrefix[prefix].ToString());
                placeholderData[$"{{{{CreditSum_{prefix}_PY}}}}"] = FormatNumber(creditPYByPrefix[prefix].ToString());
            }

            // ✅ Store summed debit & credit for 4-character prefixes
            foreach (var prefix in debitCYBy4Prefix.Keys)
            {
                placeholderData[$"{{{{DebitSum4_{prefix}_CY}}}}"] = FormatNumber(debitCYBy4Prefix[prefix].ToString());
                placeholderData[$"{{{{CreditSum4_{prefix}_CY}}}}"] = FormatNumber(creditCYBy4Prefix[prefix].ToString());
            }
            foreach (var prefix in debitPYBy4Prefix.Keys)
            {
                placeholderData[$"{{{{DebitSum4_{prefix}_PY}}}}"] = FormatNumber(debitPYBy4Prefix[prefix].ToString());
                placeholderData[$"{{{{CreditSum4_{prefix}_PY}}}}"] = FormatNumber(creditPYBy4Prefix[prefix].ToString());
            }




            // ✅ Store summed values in placeholders
            placeholderData["{{B_Total_CY}}"] = FormatNumber(sumB_CY.ToString());
            placeholderData["{{B_Total_PY}}"] = FormatNumber(sumB_PY.ToString());
            placeholderData["{{H_Total_CY}}"] = FormatNumber(sumH_CY.ToString());
            placeholderData["{{H_Total_PY}}"] = FormatNumber(sumH_PY.ToString());

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