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

        public PXAction<FLRTFinancialReport> GenerateReport;
        [PXButton(CommitChanges = false)]
        [PXUIField(DisplayName = "Generate Report")]
        protected virtual IEnumerable generateReport(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Cache.Cached
                .Cast<FLRTFinancialReport>()
                .FirstOrDefault(item => item.Selected == true);

            if (selectedRecord == null)
                throw new PXException(Messages.PleaseSelectTemplate);

            if (selectedRecord.Status == ReportStatus.InProgress)
                throw new PXException(Messages.FileGenerationInProgress);

            if (selectedRecord.Noteid == null)
                throw new PXException(Messages.TemplateHasNoFiles);

            int? companyID = GetCompanyIDFromDB(selectedRecord.ReportID);
            PXTrace.WriteInformation($"CompanyID retrieved: {companyID}");

            string tenantName = MapCompanyIDToTenantName(companyID);
            PXTrace.WriteInformation($"Mapped Tenant Name: {tenantName}");

            AcumaticaCredentials tenantCredentials = CredentialProvider.GetCredentials(tenantName);
            PXTrace.WriteInformation($"API Credentials: ClientId={tenantCredentials.ClientId}, Username={tenantCredentials.Username}");

            // Create a shared AuthService instance
            var authService = new AuthService(
                _baseUrl,
                tenantCredentials.ClientId,
                tenantCredentials.ClientSecret,
                tenantCredentials.Username,
                tenantCredentials.Password
            );

            // Optionally authenticate here if you need the token immediately
            string token = authService.AuthenticateAndGetToken();
            PXTrace.WriteInformation($"Successfully authenticated for {tenantName}. Token: {token}");

            selectedRecord.Status = ReportStatus.InProgress;
            selectedRecord.Selected = true;
            FinancialReport.Update(selectedRecord);
            Actions.PressSave();

            int? reportID = selectedRecord.ReportID;
            if (reportID == null)
                throw new PXException(Messages.PleaseSelectTemplate);

            // Pass the authService instance to the background operation
            PXLongOperation.StartOperation(this, () =>
            {
                var reportGraph = PXGraph.CreateInstance<FLRTFinancialReportMaint>();
                FLRTFinancialReport dbRecord = PXSelect<FLRTFinancialReport,
                    Where<FLRTFinancialReport.reportID, Equal<Required<FLRTFinancialReport.reportID>>>>
                    .SelectSingleBound(reportGraph, null, reportID);

                if (dbRecord == null)
                    throw new PXException(Messages.FailedToRetrieveFile);

                if (dbRecord.Noteid == null)
                    throw new PXException(Messages.TemplateHasNoFiles);

                reportGraph.FinancialReport.Current = dbRecord;
                try
                {
                    reportGraph.GenerateFinancialReport(authService); // Pass authService instead of token
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

        private void GenerateFinancialReport(AuthService authService)
        {
            try
            {
                var currentRecord = FinancialReport.Current;
                if (currentRecord == null)
                    throw new PXException(Messages.PleaseSelectTemplate);

                if (currentRecord.Noteid == null)
                    throw new PXException(Messages.TemplateHasNoFiles);

                int? companyID = GetCompanyIDFromDB(currentRecord.ReportID);
                string tenantName = MapCompanyIDToTenantName(companyID);
                PXTrace.WriteInformation($"Mapped Tenant Name: {tenantName}");

                // Use the provided authService instance
                var localDataService = new FinancialDataService(_baseUrl, authService, tenantName);

                string branch = string.IsNullOrEmpty(currentRecord.Branch) ? currentRecord.Organization : currentRecord.Branch;
                if (string.IsNullOrEmpty(branch))
                    throw new PXException(Messages.PleaseSelectABranch);
                if (string.IsNullOrEmpty(currentRecord.Ledger))
                    throw new PXException(Messages.PleaseSelectALedger);

                // 1) Retrieve file content + original name
                var (templateFileContent, originalFileName) = GetFileContent(currentRecord.Noteid);
                if (templateFileContent == null || templateFileContent.Length == 0)
                    throw new PXException(Messages.TemplateFileIsEmpty);

                // 2) Parse extension (default to .docx if something is missing)
                string extension = Path.GetExtension(originalFileName);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".docx";
                }

                // 3) Build your temp file paths using that extension
                string templatePath = Path.Combine(
                    Path.GetTempPath(),
                    $"{currentRecord.ReportCD}_Template{extension}"
                );
                File.WriteAllBytes(templatePath, templateFileContent);

                string outputFileName = $"{currentRecord.ReportCD}_Generated_{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
                string outputPath = Path.Combine(Path.GetTempPath(), outputFileName);


                string currYear = currentRecord.CurrYear ?? DateTime.Now.ToString("yyyy");
                string selectedMonth = currentRecord.FinancialMonth ?? "12";
                int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
                string prevYear = (currYearInt - 1).ToString();

                string selectedPeriod = $"{selectedMonth}{currYear}";
                string prevYearPeriod = $"{selectedMonth}{prevYear}";

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

                // Create an instance of your placeholder calculation service.
                var placeholderService = new PlaceholderCalculationService();

                // Call your method (for example, Susutnilai_Loji_dan_Peralatan) to calculate and return the placeholders.
                Dictionary<string, string> susutNilaiPlaceholders = placeholderService.Susutnilai_Loji_dan_Peralatan(cumulativeCYData, cumulativePYData);

                var placeholderData = GetPlaceholderData(
                    currYearData,
                    prevYearData,
                    januaryBeginningDataPY,
                    januaryBeginningDataCY,
                    cumulativeCYData,
                    cumulativePYData
                );

                foreach (var kvp in susutNilaiPlaceholders)
                {
                    placeholderData[kvp.Key] = kvp.Value;
                }


                // Optionally call additional methods to calculate more placeholders
                #region Placeholder Calculation Methods
                placeholderData = placeholderService.Lebihan_Kurangan_Sebelum_Cukai(placeholderData);
                placeholderData = placeholderService.Pelunasan_Aset_Tak_Ketara(placeholderData);
                placeholderData = placeholderService.Lebihan_Terkumpul(placeholderData);
                placeholderData = placeholderService.Emolumen(placeholderData);
                placeholderData = placeholderService.Manfaat_Pekerja(placeholderData);
                placeholderData = placeholderService.Perkhidmatan_Ikhtisas_DLL(placeholderData);
                placeholderData = placeholderService.Perbelanjaan_Kajian_Dan_Program(placeholderData);
                placeholderData = placeholderService.Baki1JanPenyataPerubahanAsetBersih(placeholderData);
                placeholderData = placeholderService.Perubahan_Bersih_Pelbagai_Penghutang_Deposit(placeholderData);
                placeholderData = placeholderService.Perubahan_Bersih_Pelbagai_Pemiutang_Akruan(placeholderData);
                placeholderData = placeholderService.Perubahan_Bersih_Akaun_Khas(placeholderData);
                placeholderData = placeholderService.Perubahan_Bersih_Geran_Pembangunan(placeholderData);
                placeholderData = placeholderService.Penambahan_Loji_Peralatan(placeholderData);
                placeholderData = placeholderService.Penerimaan_Pelupusan_Loji_Peralatan(placeholderData);
                placeholderData = placeholderService.Penerimaan_Daripada_Pengeluaran_Simpanan_Tetap(placeholderData);
                placeholderData = placeholderService.Faedah_Atas_Pelaburan_Diterima(placeholderData);
                placeholderData = placeholderService.Geran_Pembangunan_Dilunaskan(placeholderData);
                placeholderData = placeholderService.Lebihan_Terkumpul_Aset_Bersih(placeholderData);
                placeholderData = placeholderService.Cukai(placeholderData);

                placeholderData = placeholderService.NegatePlaceholders(placeholderData, new Dictionary<string, string>
                {
                    //5. Faedah Atas Pelaburan
                    { "{{Sum3_H75_CY}}", "{{5_CY}}" },
                    { "{{Sum3_H75_PY}}", "{{5_PY}}" },

                    //21. Akaun Khas Dilunaskan
                    { "{{H83303_CY}}", "{{21_CY}}" },
                    { "{{H83303_PY}}", "{{21_PY}}" },

                    //22. Keuntungan Pelupusan Loji dan Peralatan
                    { "{{H79101_CY}}", "{{22_CY}}" },
                    { "{{H79101_PY}}", "{{22_PY}}" },

                    //23. Jumlah Hasil
                    { "{{Sum1_H_CY}}", "{{23_CY}}" },
                    { "{{Sum1_H_PY}}", "{{23_PY}}" },

                    //25. Kumpulan Wang Komputer
                    { "{{E14102_CY}}", "{{25_CY}}" },
                    { "{{E14102_PY}}", "{{25_PY}}" }
                });

                #endregion

                _wordTemplateService.PopulateTemplate(templatePath, outputPath, placeholderData);

                byte[] generatedFileContent = File.ReadAllBytes(outputPath);
                Guid fileID = SaveGeneratedDocument(outputFileName, generatedFileContent, currentRecord);

                PXTrace.WriteInformation("Report generated successfully.");
                currentRecord.GeneratedFileID = fileID;
                currentRecord.Status = ReportStatus.Completed;
                FinancialReport.Update(currentRecord);
                Actions.PressSave();
            }
            catch (Exception ex)
            {
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
                return string.Empty;

            string mappingConfig = ConfigurationManager.AppSettings["TenantMapping"];
            if (string.IsNullOrEmpty(mappingConfig))
            {
                PXTrace.WriteError("TenantMapping is missing from Web.config.");
                throw new PXException(Messages.TenantMissingFromConfig);
            }

            var tenantMap = mappingConfig
                .Split(',')
                .Select(entry => entry.Split(':'))
                .Where(parts => parts.Length == 2)
                .ToDictionary(
                    parts => int.Parse(parts[0]),
                    parts => parts[1]
                );

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
