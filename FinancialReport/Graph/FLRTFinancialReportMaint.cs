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
                // Example of numeric formatting:
                // return number.ToString("#,##0;(#,##0)"); // negative in parentheses
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
                throw new PXException(Messages.FileGenerationInProgress);

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
            selectedRecord.Selected = true;
            FinancialReport.Update(selectedRecord);
            Actions.PressSave();

            int? reportID = selectedRecord.ReportID;
            if (reportID == null)
                throw new PXException(Messages.PleaseSelectTemplate);

            // Start the background operation
            PXLongOperation.StartOperation(this, () =>
            {
                FLRTFinancialReportMaint reportGraph = PXGraph.CreateInstance<FLRTFinancialReportMaint>();
                // Retrieve the record from the database using ReportID
                FLRTFinancialReport dbRecord = PXSelect<FLRTFinancialReport,
                    Where<FLRTFinancialReport.reportID, Equal<Required<FLRTFinancialReport.reportID>>>>
                    .Select(reportGraph, reportID);

                if (dbRecord == null)
                    throw new PXException(Messages.FailedToRetrieveFile);
                if (dbRecord.Noteid == null)
                    throw new PXException(Messages.TemplateHasNoFiles);

                // Set the record explicitly for the background graph
                reportGraph.FinancialReport.Current = dbRecord;

                try
                {
                    // Generate the report
                    reportGraph.GenerateFinancialReport(token);
                    PXTrace.WriteInformation("Report has been generated and is ready for download.");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Report generation failed: {ex.Message}");
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

                // Fetch data sets
                PXTrace.WriteInformation($"Fetching data for Period: {selectedPeriod}, Branch: {branch}, Ledger: {currentRecord.Ledger}");
                var currYearData = localDataService.FetchAllApiData(branch, currentRecord.Ledger, selectedPeriod) ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching data for Prev Year Period: {prevYearPeriod}, Branch: {branch}, Ledger: {currentRecord.Ledger}");
                var prevYearData = localDataService.FetchAllApiData(branch, currentRecord.Ledger, prevYearPeriod) ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching January {prevYear} Beginning Balance");
                var januaryBeginningDataPY = localDataService.FetchJanuaryBeginningBalance(branch, currentRecord.Ledger, prevYear) ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching January {currYear} Beginning Balance");
                var januaryBeginningDataCY = localDataService.FetchJanuaryBeginningBalance(branch, currentRecord.Ledger, currYear) ?? new FinancialApiData();

                string fromPeriodCY = "01" + currYear;
                string toPeriodCY = selectedMonth + currYear;
                PXTrace.WriteInformation($"Fetching CY cumulative data from {fromPeriodCY} to {toPeriodCY}");
                var cumulativeCYData = localDataService.FetchRangeApiData(branch, currentRecord.Ledger, fromPeriodCY, toPeriodCY);

                string fromPeriodPY = "01" + prevYear;
                string toPeriodPY = "12" + prevYear;
                PXTrace.WriteInformation($"Fetching PY cumulative data from {fromPeriodPY} to {toPeriodPY}");
                var cumulativePYData = localDataService.FetchRangeApiData(branch, currentRecord.Ledger, fromPeriodPY, toPeriodPY);

                // Build placeholder data
                var placeholderData = GetPlaceholderData(
                    currYearData,
                    prevYearData,
                    januaryBeginningDataPY,
                    januaryBeginningDataCY,
                    cumulativeCYData,
                    cumulativePYData
                );

                // Merge placeholders into Word template
                _wordTemplateService.PopulateTemplate(templatePath, outputPath, placeholderData);

                // Save generated file
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

        // ----------------------------------------------------------
        // UPDATED GetPlaceholderData to handle prefix lengths 1..5
        // ----------------------------------------------------------
        private Dictionary<string, string> GetPlaceholderData(
            FinancialApiData currYearData,
            FinancialApiData prevYearData,
            FinancialApiData januaryBeginningDataPY,
            FinancialApiData januaryBeginningDataCY,
            FinancialApiData cumulativeCYData,
            FinancialApiData cumulativePYData
        )
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

            // Some special totals for accounts starting with B or H
            decimal sumB_CY = 0m, sumB_PY = 0m;
            decimal sumH_CY = 0m, sumH_PY = 0m;

            // Helper to ensure dictionary-of-dictionaries is initialized
            Dictionary<string, decimal> EnsureDict(Dictionary<int, Dictionary<string, decimal>> outer, int prefixLen)
            {
                if (!outer.ContainsKey(prefixLen))
                    outer[prefixLen] = new Dictionary<string, decimal>();
                return outer[prefixLen];
            }

            // Data structures for prefix-based sums:
            //   (prefixLen -> (prefix -> decimal))
            var prefixEndingBalanceCY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixEndingBalancePY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixDebitCY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixCreditCY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixDebitPY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixCreditPY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixBeginningCY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixBeginningPY = new Dictionary<int, Dictionary<string, decimal>>();

            var placeholderData = new Dictionary<string, string>
            {
                { "{{financialMonth}}", monthName },
                { "{{testValue}}", "Success" },
                { "{{CY}}", currYear },
                { "{{currmonth}}", DateTime.Now.ToString("MMMM") },
                { "{{PY}}", prevYear }
            };

            // -------------------------------------------------
            // 1) Single-period Data for Current Year (CY)
            // -------------------------------------------------
            foreach (var kvp in currYearData.AccountData)
            {
                string accountId = kvp.Key;
                var data = kvp.Value;

                // Store direct placeholders: {{A111_CY}}, {{description_A111_CY}}
                placeholderData[$"{{{{{accountId}_CY}}}}"] = FormatNumber(data.EndingBalance.ToString());
                placeholderData[$"{{{{description_{accountId}_CY}}}}"] = data.Description;

                // If account starts with B or H, accumulate for special placeholders
                if (accountId.StartsWith("B", StringComparison.OrdinalIgnoreCase))
                    sumB_CY += data.EndingBalance;
                if (accountId.StartsWith("H", StringComparison.OrdinalIgnoreCase))
                    sumH_CY += data.EndingBalance;

                // Also accumulate prefix-based sums for ending balance
                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (accountId.Length < prefixLen) break;
                    string prefix = accountId.Substring(0, prefixLen);

                    var dictEB = EnsureDict(prefixEndingBalanceCY, prefixLen);
                    if (!dictEB.ContainsKey(prefix))
                        dictEB[prefix] = 0m;

                    dictEB[prefix] += data.EndingBalance;
                }
            }

            // -------------------------------------------------
            // 2) Single-period Data for Previous Year (PY)
            // -------------------------------------------------
            foreach (var kvp in prevYearData.AccountData)
            {
                string accountId = kvp.Key;
                var data = kvp.Value;

                // Store direct placeholders: {{A111_PY}}
                placeholderData[$"{{{{{accountId}_PY}}}}"] = FormatNumber(data.EndingBalance.ToString());

                // If account starts with B or H
                if (accountId.StartsWith("B", StringComparison.OrdinalIgnoreCase))
                    sumB_PY += data.EndingBalance;
                if (accountId.StartsWith("H", StringComparison.OrdinalIgnoreCase))
                    sumH_PY += data.EndingBalance;

                // Accumulate prefix-based sums for ending balance (PY)
                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (accountId.Length < prefixLen) break;
                    string prefix = accountId.Substring(0, prefixLen);

                    var dictEB = EnsureDict(prefixEndingBalancePY, prefixLen);
                    if (!dictEB.ContainsKey(prefix))
                        dictEB[prefix] = 0m;

                    dictEB[prefix] += data.EndingBalance;
                }
            }

            // -------------------------------------------------
            // 3) January 1st balances for CY / PY
            // -------------------------------------------------
            //   {{A111_Jan1_CY}},  {{A111_Jan1_PY}}
            //   plus prefix-based sums
            foreach (var kvp in januaryBeginningDataPY.AccountData)
            {
                string accountId = kvp.Key;
                var data = kvp.Value;

                placeholderData[$"{{{{{accountId}_Jan1_PY}}}}"] = FormatNumber(data.EndingBalance.ToString());

                // Accumulate prefix-based for beginning balance (PY)
                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (accountId.Length < prefixLen) break;
                    string prefix = accountId.Substring(0, prefixLen);

                    var dictBB = EnsureDict(prefixBeginningPY, prefixLen);
                    if (!dictBB.ContainsKey(prefix))
                        dictBB[prefix] = 0m;

                    dictBB[prefix] += data.EndingBalance;
                }
            }
            foreach (var kvp in januaryBeginningDataCY.AccountData)
            {
                string accountId = kvp.Key;
                var data = kvp.Value;

                placeholderData[$"{{{{{accountId}_Jan1_CY}}}}"] = FormatNumber(data.EndingBalance.ToString());

                // Accumulate prefix-based for beginning balance (CY)
                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (accountId.Length < prefixLen) break;
                    string prefix = accountId.Substring(0, prefixLen);

                    var dictBB = EnsureDict(prefixBeginningCY, prefixLen);
                    if (!dictBB.ContainsKey(prefix))
                        dictBB[prefix] = 0m;

                    dictBB[prefix] += data.EndingBalance;
                }
            }

            // -------------------------------------------------
            // 4) Cumulative Debit & Credit (CY)
            //    {{A111_debit_CY}}, {{A111_credit_CY}}
            // -------------------------------------------------
            foreach (var kvp in cumulativeCYData.AccountData)
            {
                string accountId = kvp.Key;
                var data = kvp.Value;

                placeholderData[$"{{{{{accountId}_debit_CY}}}}"] = FormatNumber(data.Debit.ToString());
                placeholderData[$"{{{{{accountId}_credit_CY}}}}"] = FormatNumber(data.Credit.ToString());

                // Accumulate prefix-based for debit/credit (CY)
                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (accountId.Length < prefixLen) break;
                    string prefix = accountId.Substring(0, prefixLen);

                    var dictDebit = EnsureDict(prefixDebitCY, prefixLen);
                    if (!dictDebit.ContainsKey(prefix))
                        dictDebit[prefix] = 0m;
                    dictDebit[prefix] += data.Debit;

                    var dictCredit = EnsureDict(prefixCreditCY, prefixLen);
                    if (!dictCredit.ContainsKey(prefix))
                        dictCredit[prefix] = 0m;
                    dictCredit[prefix] += data.Credit;
                }
            }

            // -------------------------------------------------
            // 5) Cumulative Debit & Credit (PY)
            //    {{A111_debit_PY}}, {{A111_credit_PY}}
            // -------------------------------------------------
            foreach (var kvp in cumulativePYData.AccountData)
            {
                string accountId = kvp.Key;
                var data = kvp.Value;

                placeholderData[$"{{{{{accountId}_debit_PY}}}}"] = FormatNumber(data.Debit.ToString());
                placeholderData[$"{{{{{accountId}_credit_PY}}}}"] = FormatNumber(data.Credit.ToString());

                // Accumulate prefix-based for debit/credit (PY)
                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (accountId.Length < prefixLen) break;
                    string prefix = accountId.Substring(0, prefixLen);

                    var dictDebit = EnsureDict(prefixDebitPY, prefixLen);
                    if (!dictDebit.ContainsKey(prefix))
                        dictDebit[prefix] = 0m;
                    dictDebit[prefix] += data.Debit;

                    var dictCredit = EnsureDict(prefixCreditPY, prefixLen);
                    if (!dictCredit.ContainsKey(prefix))
                        dictCredit[prefix] = 0m;
                    dictCredit[prefix] += data.Credit;
                }
            }

            // -------------------------------------------------
            // 6) Build placeholders from prefix-based dictionaries
            //    For each prefixLen + prefix, produce keys like:
            //      {{Sum1_A_CY}}, {{Sum2_A1_CY}}, etc.
            //      {{DebitSum3_A10_CY}}, {{CreditSum3_A10_CY}}, ...
            //      {{BegSum4_A111_CY}}, ...
            // -------------------------------------------------
            for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
            {
                // EndingBalance (CY + PY) => Sum{prefixLen}_{prefix}_{CY or PY}
                if (prefixEndingBalanceCY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixEndingBalanceCY[prefixLen])
                    {
                        string prefix = entry.Key;
                        decimal total = entry.Value;
                        string placeholder = $"{{{{Sum{prefixLen}_{prefix}_CY}}}}";
                        placeholderData[placeholder] = FormatNumber(total.ToString());
                    }
                }
                if (prefixEndingBalancePY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixEndingBalancePY[prefixLen])
                    {
                        string prefix = entry.Key;
                        decimal total = entry.Value;
                        string placeholder = $"{{{{Sum{prefixLen}_{prefix}_PY}}}}";
                        placeholderData[placeholder] = FormatNumber(total.ToString());
                    }
                }

                // BeginningBalance => BegSum{prefixLen}_{prefix}_CY or _PY
                if (prefixBeginningCY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixBeginningCY[prefixLen])
                    {
                        string prefix = entry.Key;
                        decimal total = entry.Value;
                        string placeholder = $"{{{{BegSum{prefixLen}_{prefix}_CY}}}}";
                        placeholderData[placeholder] = FormatNumber(total.ToString());
                    }
                }
                if (prefixBeginningPY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixBeginningPY[prefixLen])
                    {
                        string prefix = entry.Key;
                        decimal total = entry.Value;
                        string placeholder = $"{{{{BegSum{prefixLen}_{prefix}_PY}}}}";
                        placeholderData[placeholder] = FormatNumber(total.ToString());
                    }
                }

                // Debit / Credit => DebitSum{prefixLen}_{prefix}_CY, etc.
                if (prefixDebitCY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixDebitCY[prefixLen])
                    {
                        string prefix = entry.Key;
                        decimal total = entry.Value;
                        string placeholder = $"{{{{DebitSum{prefixLen}_{prefix}_CY}}}}";
                        placeholderData[placeholder] = FormatNumber(total.ToString());
                    }
                }
                if (prefixCreditCY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixCreditCY[prefixLen])
                    {
                        string prefix = entry.Key;
                        decimal total = entry.Value;
                        string placeholder = $"{{{{CreditSum{prefixLen}_{prefix}_CY}}}}";
                        placeholderData[placeholder] = FormatNumber(total.ToString());
                    }
                }
                if (prefixDebitPY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixDebitPY[prefixLen])
                    {
                        string prefix = entry.Key;
                        decimal total = entry.Value;
                        string placeholder = $"{{{{DebitSum{prefixLen}_{prefix}_PY}}}}";
                        placeholderData[placeholder] = FormatNumber(total.ToString());
                    }
                }
                if (prefixCreditPY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixCreditPY[prefixLen])
                    {
                        string prefix = entry.Key;
                        decimal total = entry.Value;
                        string placeholder = $"{{{{CreditSum{prefixLen}_{prefix}_PY}}}}";
                        placeholderData[placeholder] = FormatNumber(total.ToString());
                    }
                }
            }

            // Finally, store the special "B_" and "H_" totals
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
            // Get the current record from the grid
            FLRTFinancialReport currentRecord = FinancialReport.Current;
            if (currentRecord == null)
                throw new PXException(Messages.NoRecordIsSelected);

            if (currentRecord.GeneratedFileID == null)
                throw new PXException(Messages.NoGeneratedFile);

            // Trigger the file download using PXRedirectToFileException
            throw new PXRedirectToFileException(currentRecord.GeneratedFileID.Value, 1, false);
        }

        #endregion

        #region Helper Methods for Company/Tenant Mapping

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

        #endregion
    }
}
