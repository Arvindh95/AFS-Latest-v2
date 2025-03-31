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
using PX.Data.Update;

namespace FinancialReport
{
    public class FLRTFinancialReportMaint : PXGraph<FLRTFinancialReportMaint>
    {
        #region Services
        private readonly FileService _fileService;
        private readonly WordTemplateService _wordTemplateService;
        #endregion

        #region Configuration & Utility
        private string GetConfigValue(string key) =>
            ConfigurationManager.AppSettings[key] ?? throw new PXException(Messages.MissingConfig);

        private string _baseUrl => GetConfigValue("Acumatica.BaseUrl");

        public FLRTFinancialReportMaint()
        {
            _fileService = new FileService(this);
            _wordTemplateService = new WordTemplateService();
        }

        private string FormatNumber(string value)
        {
            return decimal.TryParse(value, out decimal number)
                ? number.ToString("#,##0")
                : value;
        }
        #endregion

        public SelectFrom<FLRTFinancialReport>.View FinancialReport;

        #region Events / Actions

        protected void FLRTFinancialReport_Selected_FieldUpdated(PXCache cache, PXFieldUpdatedEventArgs e)
        {
            var current = (FLRTFinancialReport)e.Row;
            if (current?.Selected != true) return;

            // Unselect all other rows
            foreach (FLRTFinancialReport item in FinancialReport.Cache.Cached)
            {
                if (item.ReportID != current.ReportID && item.Selected == true)
                {
                    item.Selected = false;
                }
            }
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

        public PXSave<FLRTFinancialReport> Save;
        public PXCancel<FLRTFinancialReport> Cancel;

        /// <summary>
        /// This method is called by the Generate Report button on the Acumatica screen.
        /// It checks that a valid financial report template has been selected and then
        /// starts a background (long) operation to generate the report.
        /// </summary>
        public PXAction<FLRTFinancialReport> GenerateReport;
        [PXButton(CommitChanges = false)]
        [PXUIField(DisplayName = "Generate Report")]
        protected virtual IEnumerable generateReport(PXAdapter adapter)
        {
            // 1) Retrieve the currently selected report from the cache
            var selectedRecord = FinancialReport.Cache.Cached
                .Cast<FLRTFinancialReport>()
                .FirstOrDefault(item => item.Selected == true);

            // 2) Validate that we actually have a selected record
            if (selectedRecord == null)
                throw new PXException(Messages.PleaseSelectTemplate);

            // 3) Prevent generating if currently in progress
            if (selectedRecord.Status == ReportStatus.InProgress)
                throw new PXException(Messages.FileGenerationInProgress);

            // 4) Validate that the selected template has an attached file (the .docx, for instance)
            if (selectedRecord.Noteid == null)
                throw new PXException(Messages.TemplateHasNoFiles);

            // 5) Get the Company ID from the database (mapped to a tenant name later on)
            int? companyID = GetCompanyIDFromDB(selectedRecord.ReportID);
            PXTrace.WriteInformation($"CompanyID retrieved: {companyID}");

            // 6) Map the database's CompanyID to the Acumatica Tenant Name via your custom logic
            string tenantName = MapCompanyIDToTenantName(companyID);
            PXTrace.WriteInformation($"Mapped Tenant Name: {tenantName}");

            // 7) Retrieve the credentials for this tenant from the FLRTTenantCredentials table
            AcumaticaCredentials tenantCredentials = CredentialProvider.GetCredentials(tenantName);
            PXTrace.WriteInformation(
                $"API Credentials: ClientId={tenantCredentials.ClientId}, Username={tenantCredentials.Username}"
            );

            // 8) Create a shared AuthService instance for login/logout and token refresh
            var authService = new AuthService(
                _baseUrl,
                tenantCredentials.ClientId,
                tenantCredentials.ClientSecret,
                tenantCredentials.Username,
                tenantCredentials.Password
            );

            // 9) Optionally authenticate here to ensure we can get a token
            string token = authService.AuthenticateAndGetToken();
            PXTrace.WriteInformation($"Successfully authenticated for {tenantName}. Token: {token}");

            // 10) Mark the record as "In Progress" so the user sees status is changing
            selectedRecord.Status = ReportStatus.InProgress;
            selectedRecord.Selected = true;
            FinancialReport.Update(selectedRecord);
            Actions.PressSave();

            // 11) Prepare for background processing
            int? reportID = selectedRecord.ReportID;
            if (reportID == null)
                throw new PXException(Messages.PleaseSelectTemplate);

            // 12) StartOperation runs the code in a background thread so the UI doesn't block
            PXLongOperation.StartOperation(this, () =>
            {
                // Create a fresh instance of the same graph to avoid concurrency issues
                var reportGraph = PXGraph.CreateInstance<FLRTFinancialReportMaint>();

                // 12a) Reload the FLRTFinancialReport record from DB
                FLRTFinancialReport dbRecord = PXSelect<FLRTFinancialReport,
                    Where<FLRTFinancialReport.reportID, Equal<Required<FLRTFinancialReport.reportID>>>>
                    .SelectSingleBound(reportGraph, null, reportID);

                if (dbRecord == null)
                    throw new PXException(Messages.FailedToRetrieveFile);

                if (dbRecord.Noteid == null)
                    throw new PXException(Messages.TemplateHasNoFiles);

                // 12b) Set the current record in the newly instantiated graph
                reportGraph.FinancialReport.Current = dbRecord;

                // 12c) Attempt to generate the financial report
                try
                {
                    // We pass the same AuthService instance (holding token info) for continuity
                    reportGraph.GenerateFinancialReport(authService);
                    PXTrace.WriteInformation("Report has been generated and is ready for download.");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Report generation failed: {ex.Message}");
                    dbRecord.Status = ReportStatus.Failed;
                    reportGraph.FinancialReport.Update(dbRecord);
                    reportGraph.Actions.PressSave();
                }
            });

            return adapter.Get();
        }

        /// <summary>
        /// Generates the financial report by:
        ///  - Validating that we have a valid .docx template
        ///  - Fetching data from the external FinancialDataService using a provided AuthService
        ///  - Merging placeholders and computations into the Word template
        ///  - Saving the finished file in Acumatica
        /// This method is intended to run within a PXLongOperation.
        /// </summary>
        /// <param name="authService">An AuthService instance, already authenticated.</param>
        private void GenerateFinancialReport(AuthService authService)
        {
            try
            {
                // 1) Retrieve the current record
                var currentRecord = FinancialReport.Current;
                if (currentRecord == null)
                    throw new PXException(Messages.PleaseSelectTemplate);

                // 2) Make sure there's a note/file attached
                if (currentRecord.Noteid == null)
                    throw new PXException(Messages.TemplateHasNoFiles);

                // 3) Determine tenant name from the CompanyID in the FLRTFinancialReport record
                int? companyID = GetCompanyIDFromDB(currentRecord.ReportID);
                string tenantName = MapCompanyIDToTenantName(companyID);
                PXTrace.WriteInformation($"Mapped Tenant Name: {tenantName}");

                // 4) Create a FinancialDataService that will use the provided authService
                var localDataService = new FinancialDataService(_baseUrl, authService, tenantName);

                // 5) Capture Branch & Organization separately
                string selectedBranch = currentRecord.Branch;
                string selectedOrganization = currentRecord.Organization;
                string selectedLedger = currentRecord.Ledger;

                // Ensure that at least one dimension is provided
                //if (string.IsNullOrEmpty(selectedBranch) && string.IsNullOrEmpty(selectedOrganization))
                //{
                //    throw new PXException(Messages.FailedToSelectBranchorOrg);
                //}

                if (string.IsNullOrEmpty(selectedLedger))
                    throw new PXException(Messages.PleaseSelectALedger);

                // 6) Retrieve the Word template file content + original filename
                var (templateFileContent, originalFileName) = GetFileContent(currentRecord.Noteid);
                if (templateFileContent == null || templateFileContent.Length == 0)
                    throw new PXException(Messages.TemplateFileIsEmpty);

                // 7) Determine extension from the original file name (default to ".docx" if none)
                string extension = Path.GetExtension(originalFileName);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".docx";
                }

                // 8) Build a local temp file for the template
                string templatePath = Path.Combine(
                    Path.GetTempPath(),
                    $"{currentRecord.ReportCD}_Template{extension}"
                );
                File.WriteAllBytes(templatePath, templateFileContent);

                // 9) Prepare the output file name and path
                string outputFileName = $"{currentRecord.ReportCD}_Generated_{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
                string outputPath = Path.Combine(Path.GetTempPath(), outputFileName);

                // 10) Determine the year and month for the queries
                string currYear = currentRecord.CurrYear ?? DateTime.Now.ToString("yyyy");
                string selectedMonth = currentRecord.FinancialMonth ?? "12";
                int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
                string prevYear = (currYearInt - 1).ToString();

                string selectedPeriod = $"{selectedMonth}{currYear}";
                string prevYearPeriod = $"{selectedMonth}{prevYear}";

                // 11) Fetch single-period data for Current Year (CY) and Previous Year (PY)
                PXTrace.WriteInformation($"Fetching data for Period: {selectedPeriod}, Branch: {selectedBranch}, Org: {selectedOrganization}, Ledger: {selectedLedger}");
                var currYearData = localDataService.FetchAllApiData(selectedBranch, selectedOrganization, selectedLedger, selectedPeriod)
                                    ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching data for Prev Year Period: {prevYearPeriod}, Branch: {selectedBranch}, Org: {selectedOrganization}, Ledger: {selectedLedger}");
                var prevYearData = localDataService.FetchAllApiData(selectedBranch, selectedOrganization, selectedLedger, prevYearPeriod)
                                    ?? new FinancialApiData();

                // 12) Fetch January beginning balances for both CY and PY
                PXTrace.WriteInformation($"Fetching January {prevYear} Beginning Balance");
                var januaryBeginningDataPY = localDataService.FetchJanuaryBeginningBalance(selectedBranch, selectedOrganization, selectedLedger, prevYear)
                                            ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching January {currYear} Beginning Balance");
                var januaryBeginningDataCY = localDataService.FetchJanuaryBeginningBalance(selectedBranch, selectedOrganization, selectedLedger, currYear)
                                            ?? new FinancialApiData();

                // 13) Fetch cumulative data from Jan to the selected month for CY, and full year for PY
                string fromPeriodCY = "01" + currYear;
                string toPeriodCY = selectedMonth + currYear;
                PXTrace.WriteInformation($"Fetching CY cumulative data from {fromPeriodCY} to {toPeriodCY}");
                var cumulativeCYData = localDataService.FetchRangeApiData(selectedBranch, selectedOrganization, selectedLedger, fromPeriodCY, toPeriodCY);

                string fromPeriodPY = "01" + prevYear;
                string toPeriodPY = "12" + prevYear;
                PXTrace.WriteInformation($"Fetching PY cumulative data from {fromPeriodPY} to {toPeriodPY}");
                var cumulativePYData = localDataService.FetchRangeApiData(selectedBranch, selectedOrganization, selectedLedger, fromPeriodPY, toPeriodPY);

                // 14) Aggregate these data sets into a base dictionary of placeholders
                Dictionary<string, string> basePlaceholders = GetPlaceholderData(
                    currYearData,
                    prevYearData,
                    januaryBeginningDataPY,
                    januaryBeginningDataCY,
                    cumulativeCYData,
                    cumulativePYData
                );

                // 15) Use a specialized placeholder calculator, based on the tenant
                PlaceholderCalculationService.IPlaceholderCalculator calculator;
                if (tenantName.Equals("IYRES", StringComparison.OrdinalIgnoreCase))
                {
                    calculator = new PlaceholderCalculationService.IYRESPlaceholderCalculator();
                }
                else if (tenantName.Equals("LPK", StringComparison.OrdinalIgnoreCase))
                {
                    calculator = new PlaceholderCalculationService.LPKPlaceholderCalculator();
                }
                else if (tenantName.Equals("IKMA", StringComparison.OrdinalIgnoreCase))
                {
                    calculator = new PlaceholderCalculationService.IKMAPlaceholderCalculator();
                }
                else if (tenantName.Equals("TEST", StringComparison.OrdinalIgnoreCase))
                {
                    calculator = new PlaceholderCalculationService.IKMAPlaceholderCalculator();
                }
                else
                {
                    throw new PXException(Messages.NoCalculation);
                }

                // 16) Perform final calculations on placeholders
                Dictionary<string, string> finalPlaceholders =
                    calculator.CalculatePlaceholders(currYearData, prevYearData, basePlaceholders);

                // 17) Merge placeholders into the Word template
                _wordTemplateService.PopulateTemplate(templatePath, outputPath, finalPlaceholders);

                // 18) Read the merged file from disk, then attach it to the current record as the generated file
                byte[] generatedFileContent = File.ReadAllBytes(outputPath);
                Guid fileID = SaveGeneratedDocument(outputFileName, generatedFileContent, currentRecord);

                // 19) Update the record's status and the reference to the newly generated file
                PXTrace.WriteInformation("Report generated successfully.");
                currentRecord.GeneratedFileID = fileID;
                currentRecord.Status = ReportStatus.Completed;
                FinancialReport.Update(currentRecord);
                Actions.PressSave();
            }
            catch (Exception ex)
            {
                // If anything fails, mark status as "Failed" and log the error
                PXTrace.WriteError($"Report generation failed: {ex.Message}");
                var currentRecord = FinancialReport.Current;
                if (currentRecord != null)
                {
                    currentRecord.Status = ReportStatus.Failed;
                    FinancialReport.Update(currentRecord);
                    Actions.PressSave();
                }
                throw new PXException(Messages.FailedToRetrieveFile);
            }
            finally
            {
                // Regardless of success/failure, attempt to log out of Acumatica
                authService?.Logout();
            }
        }



        #endregion

        #region Placeholder Data Helper

        private Dictionary<string, string> GetPlaceholderData(
            FinancialApiData currYearData,
            FinancialApiData prevYearData,
            FinancialApiData januaryBeginningDataPY,
            FinancialApiData januaryBeginningDataCY,
            FinancialApiData cumulativeCYData,
            FinancialApiData cumulativePYData
        )
        {
            var record = FinancialReport.Current;
            string selectedMonth = record?.FinancialMonth ?? "12";
            string currYear = record?.CurrYear ?? DateTime.Now.ToString("yyyy");

            if (string.IsNullOrEmpty(currYear))
                throw new PXException(Messages.CurrentYearNotSpecified);

            int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
            string prevYear = (currYearInt - 1).ToString();

            int monthNumber = int.Parse(selectedMonth);
            string monthName = new DateTime(1, monthNumber, 1).ToString("MMMM");

            // Helper for prefix-based dictionaries
            Dictionary<string, decimal> EnsureDict(Dictionary<int, Dictionary<string, decimal>> outer, int prefixLen)
            {
                if (!outer.ContainsKey(prefixLen))
                    outer[prefixLen] = new Dictionary<string, decimal>();
                return outer[prefixLen];
            }

            // Prefix dictionaries
            var prefixEndingBalanceCY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixEndingBalancePY = new Dictionary<int, Dictionary<string, decimal>>();

            var prefixDebitCY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixCreditCY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixDebitPY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixCreditPY = new Dictionary<int, Dictionary<string, decimal>>();

            var prefixBeginningCY = new Dictionary<int, Dictionary<string, decimal>>();
            var prefixBeginningPY = new Dictionary<int, Dictionary<string, decimal>>();

            // Base placeholders
            var placeholders = new Dictionary<string, string>
            {
                { "{{financialMonth}}", monthName },
                { "{{testValue}}", "Success" },
                { "{{CY}}", currYear },
                { "{{currmonth}}", DateTime.Now.ToString("MMMM") },
                { "{{PY}}", prevYear }
            };

            #region 1) Single-Period: Current Year
            foreach (var kvp in currYearData.AccountData)
            {
                string acctId = kvp.Key;
                var data = kvp.Value;

                placeholders[$"{{{{{acctId}_CY}}}}"] = FormatNumber(data.EndingBalance.ToString());
                placeholders[$"{{{{description_{acctId}_CY}}}}"] = data.Description;

                // Accumulate prefix-based for EndingBalance (CY)
                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (acctId.Length < prefixLen) break;
                    string prefix = acctId.Substring(0, prefixLen);

                    var dictEB = EnsureDict(prefixEndingBalanceCY, prefixLen);
                    if (!dictEB.ContainsKey(prefix))
                        dictEB[prefix] = 0m;

                    dictEB[prefix] += data.EndingBalance;
                }
            }
            #endregion

            #region 2) Single-Period: Previous Year
            foreach (var kvp in prevYearData.AccountData)
            {
                string acctId = kvp.Key;
                var data = kvp.Value;

                placeholders[$"{{{{{acctId}_PY}}}}"] = FormatNumber(data.EndingBalance.ToString());

                // Accumulate prefix-based for EndingBalance (PY)
                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (acctId.Length < prefixLen) break;
                    string prefix = acctId.Substring(0, prefixLen);

                    var dictEB = EnsureDict(prefixEndingBalancePY, prefixLen);
                    if (!dictEB.ContainsKey(prefix))
                        dictEB[prefix] = 0m;

                    dictEB[prefix] += data.EndingBalance;
                }
            }
            #endregion

            #region 3) January 1st Balances (PY / CY)
            foreach (var kvp in januaryBeginningDataPY.AccountData)
            {
                string acctId = kvp.Key;
                placeholders[$"{{{{{acctId}_Jan1_PY}}}}"] = FormatNumber(kvp.Value.EndingBalance.ToString());

                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (acctId.Length < prefixLen) break;
                    string prefix = acctId.Substring(0, prefixLen);

                    var dictBB = EnsureDict(prefixBeginningPY, prefixLen);
                    if (!dictBB.ContainsKey(prefix)) dictBB[prefix] = 0m;
                    dictBB[prefix] += kvp.Value.EndingBalance;
                }
            }

            foreach (var kvp in januaryBeginningDataCY.AccountData)
            {
                string acctId = kvp.Key;
                placeholders[$"{{{{{acctId}_Jan1_CY}}}}"] = FormatNumber(kvp.Value.EndingBalance.ToString());

                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (acctId.Length < prefixLen) break;
                    string prefix = acctId.Substring(0, prefixLen);

                    var dictBB = EnsureDict(prefixBeginningCY, prefixLen);
                    if (!dictBB.ContainsKey(prefix)) dictBB[prefix] = 0m;
                    dictBB[prefix] += kvp.Value.EndingBalance;
                }
            }
            #endregion

            #region 4) Cumulative Debit & Credit (CY)
            foreach (var kvp in cumulativeCYData.AccountData)
            {
                string acctId = kvp.Key;
                var data = kvp.Value;

                placeholders[$"{{{{{acctId}_debit_CY}}}}"] = FormatNumber(data.Debit.ToString());
                placeholders[$"{{{{{acctId}_credit_CY}}}}"] = FormatNumber(data.Credit.ToString());

                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (acctId.Length < prefixLen) break;
                    string prefix = acctId.Substring(0, prefixLen);

                    var dictDebit = EnsureDict(prefixDebitCY, prefixLen);
                    if (!dictDebit.ContainsKey(prefix)) dictDebit[prefix] = 0m;
                    dictDebit[prefix] += data.Debit;

                    var dictCredit = EnsureDict(prefixCreditCY, prefixLen);
                    if (!dictCredit.ContainsKey(prefix)) dictCredit[prefix] = 0m;
                    dictCredit[prefix] += data.Credit;
                }
            }
            #endregion

            #region 5) Cumulative Debit & Credit (PY)
            foreach (var kvp in cumulativePYData.AccountData)
            {
                string acctId = kvp.Key;
                var data = kvp.Value;

                placeholders[$"{{{{{acctId}_debit_PY}}}}"] = FormatNumber(data.Debit.ToString());
                placeholders[$"{{{{{acctId}_credit_PY}}}}"] = FormatNumber(data.Credit.ToString());

                for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
                {
                    if (acctId.Length < prefixLen) break;
                    string prefix = acctId.Substring(0, prefixLen);

                    var dictDebit = EnsureDict(prefixDebitPY, prefixLen);
                    if (!dictDebit.ContainsKey(prefix)) dictDebit[prefix] = 0m;
                    dictDebit[prefix] += data.Debit;

                    var dictCredit = EnsureDict(prefixCreditPY, prefixLen);
                    if (!dictCredit.ContainsKey(prefix)) dictCredit[prefix] = 0m;
                    dictCredit[prefix] += data.Credit;
                }
            }
            #endregion

            #region Generate Prefix-Based Placeholders
            // Summaries: Sum{prefixLen}_{prefix}_CY / PY, BegSum{prefixLen}_{prefix}_CY / PY, DebitSum, CreditSum, etc.
            for (int prefixLen = 1; prefixLen <= 5; prefixLen++)
            {
                // EndingBalance
                if (prefixEndingBalanceCY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixEndingBalanceCY[prefixLen])
                    {
                        placeholders[$"{{{{Sum{prefixLen}_{entry.Key}_CY}}}}"] = FormatNumber(entry.Value.ToString());
                    }
                }
                if (prefixEndingBalancePY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixEndingBalancePY[prefixLen])
                    {
                        placeholders[$"{{{{Sum{prefixLen}_{entry.Key}_PY}}}}"] = FormatNumber(entry.Value.ToString());
                    }
                }

                // Beginning Balances
                if (prefixBeginningCY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixBeginningCY[prefixLen])
                    {
                        placeholders[$"{{{{BegSum{prefixLen}_{entry.Key}_CY}}}}"] = FormatNumber(entry.Value.ToString());
                    }
                }
                if (prefixBeginningPY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixBeginningPY[prefixLen])
                    {
                        placeholders[$"{{{{BegSum{prefixLen}_{entry.Key}_PY}}}}"] = FormatNumber(entry.Value.ToString());
                    }
                }

                // Debit / Credit CY
                if (prefixDebitCY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixDebitCY[prefixLen])
                    {
                        placeholders[$"{{{{DebitSum{prefixLen}_{entry.Key}_CY}}}}"] = FormatNumber(entry.Value.ToString());
                    }
                }
                if (prefixCreditCY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixCreditCY[prefixLen])
                    {
                        placeholders[$"{{{{CreditSum{prefixLen}_{entry.Key}_CY}}}}"] = FormatNumber(entry.Value.ToString());
                    }
                }

                // Debit / Credit PY
                if (prefixDebitPY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixDebitPY[prefixLen])
                    {
                        placeholders[$"{{{{DebitSum{prefixLen}_{entry.Key}_PY}}}}"] = FormatNumber(entry.Value.ToString());
                    }
                }
                if (prefixCreditPY.ContainsKey(prefixLen))
                {
                    foreach (var entry in prefixCreditPY[prefixLen])
                    {
                        placeholders[$"{{{{CreditSum{prefixLen}_{entry.Key}_PY}}}}"] = FormatNumber(entry.Value.ToString());
                    }
                }
            }
            #endregion

            return placeholders;
        }

        #endregion

        #region File Retrieval / Save Helpers / Make Account Values Positive

        private (byte[] fileBytes, string originalFileName) GetFileContent(Guid? noteID)
        {
            return _fileService.GetFileContentAndName(noteID);
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
            var currentRecord = FinancialReport.Current;
            if (currentRecord == null)
                throw new PXException(Messages.NoRecordIsSelected);

            if (currentRecord.GeneratedFileID == null)
                throw new PXException(Messages.NoGeneratedFile);

            throw new PXRedirectToFileException(currentRecord.GeneratedFileID.Value, 1, false);
        }

        #endregion

        #region Company / Tenant Mapping

        private int? GetCompanyIDFromDB(int? reportID)
        {
            if (reportID == null) return null;

            using (PXTransactionScope ts = new PXTransactionScope())
            {
                var result = PXDatabase.SelectSingle<FLRTFinancialReport>(
                    new PXDataField("CompanyID"),
                    new PXDataFieldValue("ReportID", reportID)
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
            {
                throw new PXException(Messages.NoCompanyID);
            }

            // Query the FLRTTenantCredentials table for the matching CompanyNum.
            FLRTTenantCredentials tenantCreds = PXSelect<FLRTTenantCredentials,
                Where<FLRTTenantCredentials.companyNum, Equal<Required<FLRTTenantCredentials.companyNum>>>>
                .Select(this, companyID);

            if (tenantCreds == null || string.IsNullOrEmpty(tenantCreds.TenantName))
            {
                PXTrace.WriteError($"No tenant found in FLRTTenantCredentials for CompanyID: {companyID}");
                throw new PXException(Messages.NoTenantMapping);
            }

            return tenantCreds.TenantName;
        }


        #endregion
    }
}
