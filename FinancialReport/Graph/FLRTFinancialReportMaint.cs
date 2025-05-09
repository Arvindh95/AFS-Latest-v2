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
using System.Collections.Concurrent;
using System.Threading.Tasks;
using FinancialReport.Helper;
using System.Text.RegularExpressions;

namespace FinancialReport
{
    public class FLRTFinancialReportMaint : PXGraph<FLRTFinancialReportMaint>
    {
        #region Services
        private readonly FileService _fileService;
        private readonly WordTemplateService _wordTemplateService;

        public FLRTFinancialReportMaint()
        {
            _fileService = new FileService(this);
            _wordTemplateService = new WordTemplateService();
            FinancialReport.Current = null;
        }
        #endregion

        #region Configuration & Utility
        private string GetConfigValue(string key) => ConfigurationManager.AppSettings[key] ?? throw new PXException(Messages.MissingConfig);

        private string _baseUrl => GetConfigValue("Acumatica.BaseUrl");

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
                TraceLogger.Error("Ledger cannot be empty.");
                throw new PXException(Messages.PleaseSelectALedger);
            }
        }

        //protected void FLRTFinancialReport_RowSelected(PXCache cache, PXRowSelectedEventArgs e)
        //{
        //    var row = (FLRTFinancialReport)e.Row;
        //    if (row == null || row.Noteid == null) return;

        //    var file = PXSelectJoin<UploadFile,
        //        InnerJoin<NoteDoc, On<UploadFile.fileID, Equal<NoteDoc.fileID>>>,
        //        Where<NoteDoc.noteID, Equal<Required<NoteDoc.noteID>>>>
        //        .Select(this, row.Noteid)
        //        .RowCast<UploadFile>()
        //        .OrderByDescending(f => f.CreatedDateTime)
        //        .FirstOrDefault(f => f.Name != null && f.Name.Contains("FRTemplate")); // ✅ Add this filter

        //    //if (file != null && row.UploadedFileIDDisplay != file.FileID.ToString())
        //    //{
        //    //    cache.SetValue<FLRTFinancialReport.uploadedFileIDDisplay>(row, file.FileID.ToString());
        //    //    cache.MarkUpdated(row);
        //    //    cache.IsDirty = true;
        //    //}
        //}

        protected void FLRTFinancialReport_RowSelected(PXCache cache, PXRowSelectedEventArgs e)
        {
            var row = (FLRTFinancialReport)e.Row;
            if (row == null) return;

            // ✅ Determine if any report is in progress
            bool anyInProgress = FinancialReport.Cache.Cached
                .Cast<FLRTFinancialReport>()
                .Any(r => r.Status == ReportStatus.InProgress);

            // ✅ Disable this row's fields if any report is in progress
            PXUIFieldAttribute.SetEnabled(cache, row, !anyInProgress);

            // ✅ Optional: keep NoteID/file logic here
            if (row.Noteid != null)
            {
                var file = PXSelectJoin<UploadFile,
                    InnerJoin<NoteDoc, On<UploadFile.fileID, Equal<NoteDoc.fileID>>>,
                    Where<NoteDoc.noteID, Equal<Required<NoteDoc.noteID>>>>
                    .Select(this, row.Noteid)
                    .RowCast<UploadFile>()
                    .OrderByDescending(f => f.CreatedDateTime)
                    .FirstOrDefault(f => f.Name != null && f.Name.Contains("FRTemplate"));
            }

            // ✅ Disable actions if any report is running
            GenerateReport.SetEnabled(!anyInProgress);
            DownloadReport.SetEnabled(!anyInProgress);
        }




        //protected void FLRTFinancialReport_RowUpdated(PXCache cache, PXRowUpdatedEventArgs e)
        //{
        //    var row = (FLRTFinancialReport)e.Row;

        //    // If display field is set and valid, sync it into the DB field
        //    if (!string.IsNullOrEmpty(row?.UploadedFileIDDisplay)
        //        && Guid.TryParse(row.UploadedFileIDDisplay, out var parsedGuid)
        //        && row.UploadedFileID != parsedGuid)
        //    {
        //        cache.SetValue<FLRTFinancialReport.uploadedFileID>(row, parsedGuid);
        //        cache.MarkUpdated(row); // Optional: flag it as dirty so it's saved
        //    }
        //}

        protected virtual void FLRTFinancialReport_RowInserted(PXCache sender, PXRowInsertedEventArgs e)
        {
            // Save the inserted row to DB
            this.Actions.PressSave();

            // Deselect and clear form
            FinancialReport.Current = null;                  // Unbind from the form
            FinancialReport.Cache.Clear();                   // Clear cached view
            FinancialReport.Cache.ClearQueryCache();         // Clear query results
            FinancialReport.View.Clear();                    // Ensure no selection reappears
        }



        //public PXSelect<NoteDoc> NoteDocs;

        //protected virtual void NoteDoc_RowInserted(PXCache cache, PXRowInsertedEventArgs e)
        //{
        //    var link = (NoteDoc)e.Row;
        //    var cur = FinancialReport.Current;

        //    if (cur == null || link?.NoteID != cur.Noteid)
        //        return;

        //    // Update only the display field so the Save button lights up
        //    cur.UploadedFileIDDisplay = link.FileID.ToString();

        //    // Set the status to updated and mark dirty
        //    FinancialReport.Update(cur);         // this sets PXEntryStatus.Updated
        //    FinancialReport.Cache.IsDirty = true; // this enables the Save button
        //

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

            // 🔒 Defensive: Ensure ReportID is not null
            if (selectedRecord.ReportID == null)
                throw new PXException("No report selected or report ID is missing.");

            // 3) Prevent generating if currently in progress
            if (selectedRecord.Status == ReportStatus.InProgress)
                throw new PXException(Messages.FileGenerationInProgress);

            // 4) Validate that the selected template has an attached file (the .docx, for instance)
            if (selectedRecord.Noteid == null)
                throw new PXException(Messages.TemplateHasNoFiles);

            // 5) Get the Company ID from the database (mapped to a tenant name later on)
            int? companyID = GetCompanyIDFromDB(selectedRecord.ReportID);
            PXTrace.WriteInformation($"CompanyID retrieved: {companyID}");
            PXTrace.WriteInformation("Working from new Project folder");
            PXTrace.WriteInformation("Added this trace to see the publication works");
            TraceLogger.Info($"CompanyID retrieved: {companyID}");
            TraceLogger.Info("Working from new Project folder");


            // 6) Map the database's CompanyID to the Acumatica Tenant Name via your custom logic
            string tenantName = MapCompanyIDToTenantName(companyID);
            PXTrace.WriteInformation($"Mapped Tenant Name: {tenantName}");
            TraceLogger.Info($"Mapped Tenant Name: {tenantName}");

            // 7) Retrieve the credentials for this tenant from the FLRTTenantCredentials table
            AcumaticaCredentials tenantCredentials = CredentialProvider.GetCredentials(tenantName);
            PXTrace.WriteInformation($"API Credentials: ClientId={tenantCredentials.ClientId}, Username={tenantCredentials.Username}");
            TraceLogger.Info($"API Credentials: ClientId={tenantCredentials.ClientId}, Username={tenantCredentials.Username}");

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
            TraceLogger.Info($"Successfully authenticated for {tenantName}. Token: {token}");

            #region SendCredentialsToPython
            ////🔁 Send credentials to Python backend
            //string pythonUrl = "http://localhost:8000/receive-credentials"; // ✅ Replace with actual Python endpoint
            //SendCredentialsToPython(
            //    pythonUrl,
            //    tenantCredentials,
            //    tenantName,
            //    selectedRecord.ReportID,
            //    selectedRecord.Noteid,
            //    selectedRecord.Branch,
            //    selectedRecord.Organization,
            //    selectedRecord.Ledger,
            //    selectedRecord.FinancialMonth,
            //    selectedRecord.CurrYear
            //);
            #endregion

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
                    TraceLogger.Info("Report has been generated and is ready for download.");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Report generation failed: {ex.Message}");
                    TraceLogger.Error($"Report generation failed: {ex.Message}");
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
                TraceLogger.Info($"Mapped Tenant Name: {tenantName}");

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
                var (templateFileContent, originalFileName) = GetFileContent(currentRecord.Noteid, currentRecord);
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

                


                // 📄 Optional Debug: Save extracted placeholders to .txt
                string placeholderTxtPath = Path.Combine("C:\\Program Files\\Acumatica ERP\\saga\\App_Data\\Logs\\FinancialReports\\Extracted Placeholder Logs\\", $"{currentRecord.ReportCD}_{DateTime.Now:yyyyMMdd_HHmmssfff}_ExtractedPlaceholders.txt");
                _wordTemplateService.SaveExtractedPlaceholdersToTxt(templatePath, placeholderTxtPath);
                PXTrace.WriteInformation($"🔍 Extracted placeholders saved to: {placeholderTxtPath}");
                TraceLogger.Info($"🔍 Extracted placeholders saved to: {placeholderTxtPath}");

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
                TraceLogger.Info($"Fetching data for Period: {selectedPeriod}, Branch: {selectedBranch}, Org: {selectedOrganization}, Ledger: {selectedLedger}");
                var currYearData = localDataService.FetchAllApiData(selectedBranch, selectedOrganization, selectedLedger, selectedPeriod)
                                    ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching data for Prev Year Period: {prevYearPeriod}, Branch: {selectedBranch}, Org: {selectedOrganization}, Ledger: {selectedLedger}");
                TraceLogger.Info($"Fetching data for Prev Year Period: {prevYearPeriod}, Branch: {selectedBranch}, Org: {selectedOrganization}, Ledger: {selectedLedger}");
                var prevYearData = localDataService.FetchAllApiData(selectedBranch, selectedOrganization, selectedLedger, prevYearPeriod)
                                    ?? new FinancialApiData();

                // NEW STEP: Extract placeholders from template
                List<string> extractedKeys = _wordTemplateService.ExtractPlaceholderKeys(templatePath);

                var requiredAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var requiredPrefixes = new Dictionary<int, HashSet<string>>();

                foreach (var key in extractedKeys)
                {
                    string clean = key.Trim('{', '}');

                    // Match Sum placeholders: DebitSum3_A12_CY
                    var sumMatch = Regex.Match(clean, @"^(CreditSum|DebitSum|BegSum|Sum)(\d)_(.+)_(CY|PY)$", RegexOptions.IgnoreCase);
                    if (sumMatch.Success)
                    {
                        int level = int.Parse(sumMatch.Groups[2].Value);
                        string prefix = sumMatch.Groups[3].Value;

                        if (!requiredPrefixes.ContainsKey(level))
                            requiredPrefixes[level] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        requiredPrefixes[level].Add(prefix);
                        continue;
                    }

                    // Match direct accounts: A11101_credit_CY, A81101_CY, etc.
                    var match = Regex.Match(clean, @"^([A-Z0-9]+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                        requiredAccounts.Add(match.Groups[1].Value);
                }

                // 🔍 Log extracted filters
                TraceLogger.Info("📌 Required exact accounts:");
                foreach (var acct in requiredAccounts)
                    TraceLogger.Info("  - " + acct);

                TraceLogger.Info("📌 Required prefixes:");
                foreach (var kv in requiredPrefixes)
                    foreach (var prefix in kv.Value)
                        TraceLogger.Info($"  - Level {kv.Key}: {prefix}");
                void FilterAccounts(FinancialApiData data)
                {
                    var filtered = new Dictionary<string, FinancialPeriodData>(StringComparer.OrdinalIgnoreCase);

                    foreach (var kvp in data.AccountData)
                    {
                        string account = kvp.Key;

                        if (requiredAccounts.Contains(account))
                        {
                            filtered[account] = kvp.Value;
                            continue;
                        }

                        foreach (var kv in requiredPrefixes)
                        {
                            int level = kv.Key;
                            foreach (var prefix in kv.Value)
                            {
                                if (account.Length >= level &&
                                    account.Substring(0, level).Equals(prefix, StringComparison.OrdinalIgnoreCase))
                                {
                                    filtered[account] = kvp.Value;
                                    break;
                                }
                            }
                        }
                    }

                    data.AccountData = filtered;
                }


                TraceLogger.Info($"🔍 Extracted {extractedKeys.Count} placeholder keys from template");             
                TraceLogger.Info("✅ Placeholder dictionary built using direct CY/PY account values only.");

                // 12) Fetch January beginning balances for both CY and PY
                PXTrace.WriteInformation($"Fetching January {prevYear} Beginning Balance");
                TraceLogger.Info($"Fetching January {prevYear} Beginning Balance");
                var januaryBeginningDataPY = localDataService.FetchJanuaryBeginningBalance(selectedBranch, selectedOrganization, selectedLedger, prevYear)
                                            ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching January {currYear} Beginning Balance");
                TraceLogger.Info($"Fetching January {currYear} Beginning Balance");
                var januaryBeginningDataCY = localDataService.FetchJanuaryBeginningBalance(selectedBranch, selectedOrganization, selectedLedger, currYear)
                                            ?? new FinancialApiData();

                // 13) Fetch cumulative data from Jan to the selected month for CY, and full year for PY
                string fromPeriodCY = "01" + currYear;
                string toPeriodCY = selectedMonth + currYear;
                PXTrace.WriteInformation($"Fetching CY cumulative data from {fromPeriodCY} to {toPeriodCY}");
                TraceLogger.Info($"Fetching CY cumulative data from {fromPeriodCY} to {toPeriodCY}");
                var cumulativeCYData = localDataService.FetchRangeApiData(selectedBranch, selectedOrganization, selectedLedger, fromPeriodCY, toPeriodCY);

                string fromPeriodPY = "01" + prevYear;
                string toPeriodPY = "12" + prevYear;
                PXTrace.WriteInformation($"Fetching PY cumulative data from {fromPeriodPY} to {toPeriodPY}");
                TraceLogger.Info($"Fetching PY cumulative data from {fromPeriodPY} to {toPeriodPY}");
                var cumulativePYData = localDataService.FetchRangeApiData(selectedBranch, selectedOrganization, selectedLedger, fromPeriodPY, toPeriodPY);

                FilterAccounts(currYearData);
                FilterAccounts(prevYearData);
                FilterAccounts(januaryBeginningDataCY);
                FilterAccounts(januaryBeginningDataPY);
                FilterAccounts(cumulativeCYData);
                FilterAccounts(cumulativePYData);


                var sw = System.Diagnostics.Stopwatch.StartNew();
                ///Uncomment to Use Normal process///
                // 💡 Now it's safe to call this:
                Dictionary<string, string> finalPlaceholders =
                    localDataService.BuildSmartPlaceholderMapFromKeys(
                        extractedKeys,
                        currYearData,
                        prevYearData,
                        januaryBeginningDataCY,
                        januaryBeginningDataPY,
                        cumulativeCYData,
                        cumulativePYData
                    );
                ///Till Here///

                sw.Stop();
                PXTrace.WriteInformation($"⏱️ Placeholder mapping completed in {sw.ElapsedMilliseconds} ms");
                TraceLogger.Info($"⏱️ Placeholder mapping completed in {sw.ElapsedMilliseconds} ms");

                ///Uncomment to Use AI process///
                //// 13) Build raw computed dictionary first
                //Dictionary<string, string> computedValues =
                //    localDataService.BuildSmartPlaceholderMapFromKeys(
                //        extractedKeys,
                //        currYearData,
                //        prevYearData,
                //        januaryBeginningDataCY,
                //        januaryBeginningDataPY,
                //        cumulativeCYData,
                //        cumulativePYData
                //    );

                //// 14) Use Gemini to remap placeholders using AI
                //string geminiApiKey = ConfigurationManager.AppSettings["GeminiApiKey"];
                //if (string.IsNullOrWhiteSpace(geminiApiKey))
                //    throw new PXException("Gemini API key not found in config.");

                //TraceLogger.Info("🤖 Sending extracted placeholders and computed values to Gemini...");
                //Dictionary<string, string> finalPlaceholders = GeminiPlaceholderMatcher
                //    .MatchPlaceholdersWithGeminiAsync(extractedKeys, computedValues, geminiApiKey)
                //    .GetAwaiter().GetResult();

                //TraceLogger.Info($"✅ Final placeholders re-mapped using Gemini AI. Count: {finalPlaceholders.Count}");
                ///Till Here///


                // Log matched and unmatched placeholders
                foreach (var key in extractedKeys)
                {
                    if (!finalPlaceholders.ContainsKey(key))
                    {
                        //TraceLogger.Error($"⚠️ Placeholder {key} was extracted but has no mapped value!");
                    }
                    else
                    {
                        //TraceLogger.Info($"✅ Mapped: {key} = {finalPlaceholders[key]}");
                    }
                }

                // 16a) Fetch composite key-based data
                var cyCompositeData = localDataService.FetchCompositeKeyData(selectedBranch, selectedOrganization, selectedLedger, selectedPeriod);
                var pyCompositeData = localDataService.FetchCompositeKeyData(selectedBranch, selectedOrganization, selectedLedger, prevYearPeriod);
                
                



                // 14) Aggregate these data sets into a base dictionary of placeholders
                //Dictionary<string, string> basePlaceholders = GetPlaceholderData(
                //    currYearData,
                //    prevYearData,
                //    januaryBeginningDataPY,
                //    januaryBeginningDataCY,
                //    cumulativeCYData,
                //    cumulativePYData
                //);

                // 15) Use a specialized placeholder calculator, based on the tenant
                //PlaceholderCalculationService.IPlaceholderCalculator calculator;
                //if (tenantName.Equals("IYRES", StringComparison.OrdinalIgnoreCase))
                //{
                //    calculator = new PlaceholderCalculationService.IYRESPlaceholderCalculator();
                //}
                //else if (tenantName.Equals("LPK", StringComparison.OrdinalIgnoreCase))
                //{
                //    calculator = new PlaceholderCalculationService.LPKPlaceholderCalculator();
                //}
                //else if (tenantName.Equals("IKMA", StringComparison.OrdinalIgnoreCase))
                //{
                //    calculator = new PlaceholderCalculationService.IKMAPlaceholderCalculator(_baseUrl, authService, tenantName);
                //}
                //else if (tenantName.Equals("Company", StringComparison.OrdinalIgnoreCase))
                //{
                //    calculator = new PlaceholderCalculationService.TESTPlaceholderCalculator();
                //}
                //else
                //{
                //    throw new PXException(Messages.NoCalculation);
                //}

                

                // 16b) Inject into the main FinancialApiData containers
                currYearData.CompositeKeyData = cyCompositeData.CompositeKeyData;
                prevYearData.CompositeKeyData = pyCompositeData.CompositeKeyData;

                // 16) Perform final calculations on placeholders
                //var finalPlaceholders = mappedPlaceholderValues;



                // 🔥 NEW: Save full financial data for Gemini
                string financialDataTxtPath = Path.Combine("C:\\Program Files\\Acumatica ERP\\saga\\App_Data\\Logs\\FinancialReports\\Financial Data Logs\\", $"{currentRecord.ReportCD}_{DateTime.Now:yyyyMMdd_HHmmssfff}_FinancialData.txt");
                SaveFinancialDataToTxt(
                    currYearData,
                    prevYearData,
                    januaryBeginningDataCY,
                    januaryBeginningDataPY,
                    cumulativeCYData,
                    cumulativePYData,
                    cyCompositeData,
                    pyCompositeData,
                    finalPlaceholders, // 🔥 Final placeholder dictionary (computed!)
                    financialDataTxtPath
                );

                PXTrace.WriteInformation($"Financial data dictionary saved at {financialDataTxtPath}");
                TraceLogger.Info($"Financial data dictionary saved at {financialDataTxtPath}");



                // 17) Merge placeholders into the Word template
                finalPlaceholders["CY"] = currYear;
                finalPlaceholders["PY"] = prevYear;
                TraceLogger.Info($"📌 Injected CY={currYear}, PY={prevYear} for direct template replacement.");

                // Log all placeholders
                string traceLogPath = Path.Combine("C:\\Program Files\\Acumatica ERP\\saga\\App_Data\\Logs\\FinancialReports\\Final Placeholder Logs\\", $"{currentRecord.ReportCD}_{DateTime.Now:yyyyMMdd_HHmmssfff}_FinalPlaceholders.log");
                using (var writer = new StreamWriter(traceLogPath, false, Encoding.UTF8))
                {
                    writer.WriteLine("=== Final Placeholder Values ===");
                    foreach (var kvp in finalPlaceholders.OrderBy(k => k.Key))
                        writer.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
                TraceLogger.Info($"📝 Final placeholder values saved to: {traceLogPath}");

                finalPlaceholders["{{CY}}"] = currYear;
                finalPlaceholders["{{PY}}"] = prevYear;

                _wordTemplateService.PopulateTemplate(templatePath, outputPath, finalPlaceholders);

                // 18) Read the merged file from disk, then attach it to the current record as the generated file
                byte[] generatedFileContent = File.ReadAllBytes(outputPath);
                Guid fileID = SaveGeneratedDocument(outputFileName, generatedFileContent, currentRecord);

                // 19) Update the record's status and the reference to the newly generated file
                PXTrace.WriteInformation("Report generated successfully.");
                TraceLogger.Info("Report generated successfully.");
                currentRecord.GeneratedFileID = fileID;
                currentRecord.Status = ReportStatus.Completed;
                FinancialReport.Update(currentRecord);
                Actions.PressSave();
                FinancialReport.View.RequestRefresh();
            }
            catch (Exception ex)
            {
                // If anything fails, mark status as "Failed" and log the error
                PXTrace.WriteError($"Report generation failed: {ex.Message}");
                TraceLogger.Error($"Report generation failed: {ex.Message}");
                var currentRecord = FinancialReport.Current;
                if (currentRecord != null)
                {
                    currentRecord.Status = ReportStatus.Failed;
                    FinancialReport.Update(currentRecord);
                    Actions.PressSave();
                    FinancialReport.View.RequestRefresh();
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
        #region Parallel Placeholder Data Helper
        private class PrefixAggregator
        {
            private const int MaxPrefixLength = 5;

            // Thread-safe dictionaries
            private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>> _ending =
                new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>>();
            private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>> _debit =
                new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>>();
            private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>> _credit =
                new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>>();
            private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>> _beginning =
                new ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>>();

            // Thread-safe placeholders dictionary
            private readonly ConcurrentDictionary<string, string> _placeholders;

            public PrefixAggregator(ConcurrentDictionary<string, string> placeholders)
            {
                _placeholders = placeholders;
            }

            public void Add(string yearType, string acctId, FinancialPeriodData data)
            {
                _placeholders[Wrap(acctId + "_" + yearType)] = FormatNumber(data.EndingBalance);
                if (yearType == "CY")
                {
                    _placeholders[Wrap("description_" + acctId + "_CY")] = data.Description;
                }
                AddTo(_ending, yearType, acctId, data.EndingBalance);
            }

            public void AddDebitCredit(string yearType, string acctId, FinancialPeriodData data)
            {
                _placeholders[Wrap(acctId + "_debit_" + yearType)] = FormatNumber(data.Debit);
                _placeholders[Wrap(acctId + "_credit_" + yearType)] = FormatNumber(data.Credit);
                AddTo(_debit, yearType, acctId, data.Debit);
                AddTo(_credit, yearType, acctId, data.Credit);
            }

            public void AddBeginning(string yearType, string acctId, decimal value)
            {
                _placeholders[Wrap(acctId + "_Jan1_" + yearType)] = FormatNumber(value);
                AddTo(_beginning, yearType, acctId, value);
            }

            public void InjectSummedPlaceholders()
            {
                Inject("Sum", _ending);
                Inject("DebitSum", _debit);
                Inject("CreditSum", _credit);
                Inject("BegSum", _beginning);
            }

            private void AddTo(ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>> dict,
                              string yearType, string acctId, decimal value)
            {
                var yearMap = dict.GetOrAdd(yearType, _ =>
                    new ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>());

                for (int len = 1; len <= MaxPrefixLength && acctId.Length >= len; len++)
                {
                    string prefix = acctId.Substring(0, len);
                    var levelMap = yearMap.GetOrAdd(len, _ =>
                        new ConcurrentDictionary<string, decimal>(StringComparer.OrdinalIgnoreCase));

                    levelMap.AddOrUpdate(prefix, value, (_, existing) => existing + value);
                }
            }

            private void Inject(string label,
                               ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<string, decimal>>> source)
            {
                foreach (var yearKvp in source)
                {
                    string yearType = yearKvp.Key;
                    var yearMap = yearKvp.Value;

                    foreach (var levelKvp in yearMap)
                    {
                        int level = levelKvp.Key;
                        var levelMap = levelKvp.Value;

                        foreach (var prefixKvp in levelMap)
                        {
                            string prefix = prefixKvp.Key;
                            decimal value = prefixKvp.Value;

                            string key = Wrap(label + level + "_" + prefix + "_" + yearType);
                            _placeholders[key] = FormatNumber(value);
                        }
                    }
                }
            }
        }

        private static string Wrap(string key) => $"{{{{{key}}}}}";
        private static string FormatNumber(decimal value) => value.ToString("#,##0");

        private Dictionary<string, string> GetPlaceholderData(
            FinancialApiData currYearData,
            FinancialApiData prevYearData,
            FinancialApiData januaryBeginningDataPY,
            FinancialApiData januaryBeginningDataCY,
            FinancialApiData cumulativeCYData,
            FinancialApiData cumulativePYData)
        {
            var record = FinancialReport.Current;
            string selectedMonth = record != null && !string.IsNullOrWhiteSpace(record.FinancialMonth)
                ? record.FinancialMonth.PadLeft(2, '0') : "12";
            string currYear = record != null && !string.IsNullOrWhiteSpace(record.CurrYear)
                ? record.CurrYear : DateTime.Now.ToString("yyyy");

            int currYearInt;
            if (!int.TryParse(currYear, out currYearInt))
            {
                currYearInt = DateTime.Now.Year;
            }
            string prevYear = (currYearInt - 1).ToString();
            int monthNumber = int.Parse(selectedMonth);
            string monthName = new DateTime(1, monthNumber, 1).ToString("MMMM");

            // Thread-safe placeholder dictionary
            var placeholders = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Add base metadata
            placeholders[Wrap("financialMonth")] = monthName;
            placeholders[Wrap("MonthNumber")] = selectedMonth;
            placeholders[Wrap("testValue")] = "Success";
            placeholders[Wrap("CY")] = currYear;
            placeholders[Wrap("currmonth")] = DateTime.Now.ToString("MMMM");
            placeholders[Wrap("PY")] = prevYear;

            // Create aggregator with shared placeholders dictionary
            var prefixMaps = new PrefixAggregator(placeholders);

            // Process data sets in parallel
            var tasks = new List<Task>
            {
                // 1. CY direct values
                Task.Run(() => {
                    Parallel.ForEach(currYearData.AccountData, kvp => {
                        prefixMaps.Add("CY", kvp.Key, kvp.Value);
                    });
                }),
        
                // 2. PY direct values
                Task.Run(() => {
                    Parallel.ForEach(prevYearData.AccountData, kvp => {
                        prefixMaps.Add("PY", kvp.Key, kvp.Value);
                    });
                }),
        
                // 3. January Beginning Balances
                Task.Run(() => {
                    Parallel.ForEach(januaryBeginningDataCY.AccountData, kvp => {
                        prefixMaps.AddBeginning("CY", kvp.Key, kvp.Value.BeginningBalance);
                    });
                }),

                Task.Run(() => {
                    Parallel.ForEach(januaryBeginningDataPY.AccountData, kvp => {
                        prefixMaps.AddBeginning("PY", kvp.Key, kvp.Value.BeginningBalance);
                    });
                }),
        
                // 4. Cumulative debits/credits
                Task.Run(() => {
                    Parallel.ForEach(cumulativeCYData.AccountData, kvp => {
                        prefixMaps.AddDebitCredit("CY", kvp.Key, kvp.Value);
                    });
                }),

                Task.Run(() => {
                    Parallel.ForEach(cumulativePYData.AccountData, kvp => {
                        prefixMaps.AddDebitCredit("PY", kvp.Key, kvp.Value);
                    });
                })
            };

            // Wait for all processing to complete
            Task.WaitAll(tasks.ToArray());

            // Generate prefix summaries
            prefixMaps.InjectSummedPlaceholders();

            // Convert back to regular dictionary for compatibility
            return placeholders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        #endregion



        #endregion

        #region File Retrieval / Save Helpers / Make Account Values Positive

        private (byte[] fileBytes, string originalFileName) GetFileContent(Guid? noteID, FLRTFinancialReport currentRecord)
        {
            return _fileService.GetFileContentAndName(noteID, currentRecord);
        }

        private Guid SaveGeneratedDocument(string fileName, byte[] fileContent, FLRTFinancialReport currentRecord)
        {
            return _fileService.SaveGeneratedDocument(fileName, fileContent, currentRecord);
        }

        private void SaveFinancialDataToTxt(
        FinancialApiData currYearData,
        FinancialApiData prevYearData,
        FinancialApiData januaryCYData,
        FinancialApiData januaryPYData,
        FinancialApiData cumulativeCYData,
        FinancialApiData cumulativePYData,
        FinancialApiData cyCompositeData,
        FinancialApiData pyCompositeData,
        Dictionary<string, string> finalPlaceholders, // 🔥 Computed placeholder dictionary!
        string outputFilePath)
        {
            using (var writer = new StreamWriter(outputFilePath, false, Encoding.UTF8))
            {
                writer.WriteLine("\n=== FinalPlaceholders (Computed) ===");
                foreach (var kvp in finalPlaceholders.OrderBy(k => k.Key))
                {
                    writer.WriteLine($"{kvp.Key}: {kvp.Value}");
                }
            }
        }




        #endregion

        #region Download Method

        public PXAction<FLRTFinancialReport> DownloadReport;
        [PXButton]
        [PXUIField(DisplayName = "Download Report", MapEnableRights = PXCacheRights.Select, Visible = true)]
        protected virtual IEnumerable downloadReport(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Cache.Cached
                .Cast<FLRTFinancialReport>()
                .FirstOrDefault(x => x.Selected == true);

            if (selectedRecord == null)
                throw new PXException(Messages.NoRecordIsSelected);

            if (selectedRecord.GeneratedFileID == null)
                throw new PXException(Messages.NoGeneratedFile);

            //throw new PXRedirectToFileException(selectedRecord.GeneratedFileID.Value, true, false);
            throw new PXRedirectToFileException(selectedRecord.GeneratedFileID.Value, 1, false);
        }



        
        #endregion

        #region Company / Tenant Mapping

        private int? GetCompanyIDFromDB(int? reportID)
        {
            if (reportID == null)
                throw new PXException("ReportID cannot be null when retrieving CompanyID.");

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

            throw new PXException($"No CompanyID found for ReportID {reportID}.");
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
                TraceLogger.Error($"No tenant found in FLRTTenantCredentials for CompanyID: {companyID}");
                throw new PXException(Messages.NoTenantMapping);
            }

            return tenantCreds.TenantName;
        }

        #region SendCredentialsToPython
        private void SendCredentialsToPython(string url, AcumaticaCredentials creds, string tenantName, int? reportId, Guid? noteId, string branch, string organization, string ledger, string financialMonth, string currYear)
        {
            var payload = new
            {
                TenantName = tenantName,
                ClientId = creds.ClientId,
                ClientSecret = creds.ClientSecret,
                Username = creds.Username,
                Password = creds.Password,
                ReportID = reportId,
                NoteID = noteId?.ToString(),
                Branch = branch,
                Organization = organization,
                Ledger = ledger,
                FinancialMonth = financialMonth,
                CurrYear = currYear
            };


            using (var client = new HttpClient())
            {
                var json = JsonConvert.SerializeObject(payload);
                // ✅ Debug log to see what exactly is being sent
                PXTrace.WriteInformation("📦 Payload JSON: " + json);
                TraceLogger.Info("📦 Payload JSON: " + json);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = client.PostAsync(url, content).Result;

                if (!response.IsSuccessStatusCode)
                {
                    PXTrace.WriteError($"❌ Failed to send to Python: {response.StatusCode}");
                    TraceLogger.Error($"❌ Failed to send to Python: {response.StatusCode}");

                }
                else
                {
                    PXTrace.WriteInformation("✅ Sent credentials to Python");
                    TraceLogger.Info("✅ Sent credentials to Python");
                }
            }
        }
        #endregion



        #endregion
    }
}
