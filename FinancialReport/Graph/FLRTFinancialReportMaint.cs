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

namespace FinancialReport
{
    public class FLRTFinancialReportMaint : PXGraph<FLRTFinancialReportMaint>
    {
        public SelectFrom<FLRTFinancialReport>.View FinancialReport;
        private readonly AuthService _authService;
        private readonly FinancialDataService _dataService;
        private string GetConfigValue(string key)
        {
            return ConfigurationManager.AppSettings[key] ?? throw new PXException(Messages.MissingConfig);
        }
        private string _baseUrl => GetConfigValue("Acumatica.BaseUrl");

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

        public FLRTFinancialReportMaint()
        {
            _authService = new AuthService(_baseUrl, GetConfigValue("Acumatica.ClientId"), GetConfigValue("Acumatica.ClientSecret"), GetConfigValue("Acumatica.Username"), GetConfigValue("Acumatica.Password"));
            _dataService = new FinancialDataService(_baseUrl, _authService, GetAccountNumbers);
        }
        

        private string FormatNumber(string value)
        {
            if (decimal.TryParse(value, out decimal number))
            {
                return number.ToString("N2"); // Formats as ###,###.00
            }
            return value; // Return original if parsing fails
        }


        protected void FLRTFinancialReport_CurrYear_FieldUpdated(PXCache cache, PXFieldUpdatedEventArgs e)
        {
            var row = (FLRTFinancialReport)e.Row;
            if (row != null)
            {
                PXTrace.WriteInformation($"CurrYear Updated to: {row.CurrYear}");
            }
        }



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
            var row = (FLRTFinancialReport)e.Row;
            if (row != null && string.IsNullOrEmpty(row.Branch))
            {
                PXTrace.WriteError("Branch cannot be empty.");
                throw new PXException(Messages.PleaseSelectABranch);
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
        
        #endregion

        public PXSave<FLRTFinancialReport> Save;
        public PXCancel<FLRTFinancialReport> Cancel;

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
            if (selectedRecord.Noteid == null)
                throw new PXException(Messages.TemplateHasNoFiles);

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

                // Generate the report.
                reportGraph.GenerateFinancialReport();

                // Log or store the file ID so the UI can later display a download link.
                PXTrace.WriteInformation("Report has been generated and is ready for download.");
            });

            return adapter.Get();
        }

        

        #region Main Report Generation GenerateFinancialReport()

        private void GenerateFinancialReport()
        {
            try
            {
                // Use the persisted current record.
                var currentRecord = FinancialReport.Current;
                if (currentRecord == null)
                    throw new PXException(Messages.PleaseSelectTemplate);
                if (currentRecord.Noteid == null)
                    throw new PXException(Messages.TemplateHasNoFiles);

                // Use currentRecord directly (avoid searching the cache for Selected).
                string branch = currentRecord.Branch;
                string ledger = currentRecord.Ledger;

                if (string.IsNullOrEmpty(branch))
                    throw new PXException(Messages.PleaseSelectABranch);
                if (string.IsNullOrEmpty(ledger))
                    throw new PXException(Messages.PleaseSelectALedger);

                // Fetch template file content
                var templateFileContent = GetFileContent(currentRecord.Noteid);
                if (templateFileContent == null || templateFileContent.Length == 0)
                    throw new PXException(Messages.TemplateFileIsEmpty);

                // Create paths for template and output
                string templatePath = Path.Combine(Path.GetTempPath(), $"{currentRecord.ReportCD}_Template.docx");
                File.WriteAllBytes(templatePath, templateFileContent);

                string uniqueFileName = $"{currentRecord.ReportCD}_Generated_{DateTime.Now:yyyyMMdd_HHmmssfff}.docx";
                string outputPath = Path.Combine(Path.GetTempPath(), uniqueFileName);

                // Determine periods for current and previous years.
                string currYear = currentRecord.CurrYear ?? DateTime.Now.ToString("yyyy");
                string selectedMonth = currentRecord.FinancialMonth ?? "12"; // Default to December
                string selectedPeriod = $"{selectedMonth}{currYear}";
                int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
                string prevYear = (currYearInt - 1).ToString();
                string prevYearPeriod = selectedMonth + prevYear;

                PXTrace.WriteInformation($"Fetching data for Period: {selectedPeriod}, Branch: {branch}, Ledger: {ledger}");
                var currYearData = _dataService.FetchAllApiData(branch, ledger, selectedPeriod) ?? new FinancialApiData();

                PXTrace.WriteInformation($"Fetching data for Prev Year Period: {prevYearPeriod}, Branch: {branch}, Ledger: {ledger}");
                var prevYearData = _dataService.FetchAllApiData(branch, ledger, prevYearPeriod) ?? new FinancialApiData(); 

                // Prepare placeholder data and populate the template.
                var placeholderData = GetPlaceholderData(currYearData, prevYearData);
                PopulateTemplate(templatePath, outputPath, placeholderData);

                // Upload the generated document and store the file ID (instead of redirecting immediately).
                byte[] generatedFileContent = File.ReadAllBytes(outputPath);
                Guid fileID = SaveGeneratedDocument(uniqueFileName, generatedFileContent, currentRecord);

                PXTrace.WriteInformation("Report generated successfully.");

                // Redirect to the generated file
                //throw new PXRedirectToFileException(fileID, 1, false);

                // Optionally, store the fileID on the record or in a related table so that the UI can display a download link.
                // For example:
                currentRecord.GeneratedFileID = fileID;
                FinancialReport.Update(currentRecord);
                Actions.PressSave();

                
            }
            finally
            {
                _authService.Logout();
            }
        }


        private Dictionary<string, string> GetPlaceholderData(FinancialApiData currYearData, FinancialApiData prevYearData)
        {
            var selectedRecord = FinancialReport.Current;
            string selectedMonth = selectedRecord?.FinancialMonth ?? "12"; // Default to December
            string currYear = selectedRecord?.CurrYear ?? DateTime.Now.ToString("yyyy");

            // Validate CurrYear
            if (string.IsNullOrEmpty(currYear))
                throw new PXException(Messages.CurrentYearNotSpecified);

            // Compute PrevYear
            int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
            string prevYear = (currYearInt - 1).ToString();


            // ✅ Convert "01" to "January", "02" to "February", etc.
            int monthNumber = int.Parse(selectedMonth);
            string monthName = new DateTime(1, monthNumber, 1).ToString("MMMM");

            var placeholderData = new Dictionary<string, string>
            {
                { "{{financialMonth}}", monthName},
                { "{{branchName}}", "Censof-Test" },
                { "{{agencyname}}", "Suruhanjaya Tenaga" },
                { "{{chairmanname}}", "Dato' Khir bin Osman" },
                { "{{chairmanname2}}", "Dato' Shaik Hussein bin Anggota" },
                { "{{testData}}", DateTime.Now.ToShortDateString() },
                { "{{month/year}}", DateTime.Now.ToString("MMMM dd, yyyy") },
                { "{{CY}}", currYear },
                { "{{currmonth}}", DateTime.Now.ToString("MMMM") },
                { "{{PY}}", prevYear }
            };

            // Add fetched data for CurrYear
            foreach (var account in currYearData.AccountData)
            {
                placeholderData[$"{{{{{account.Key}_CY}}}}"] = FormatNumber(account.Value.EndingBalance); // {{101000_2024}}
                placeholderData[$"{{{{description_{account.Key}_CY}}}}"] = account.Value.Description; // {{description_101000_CurrYear}}
            }

            // Add fetched data for PrevYear
            foreach (var account in prevYearData.AccountData)
            {
                placeholderData[$"{{{{{account.Key}_PY}}}}"] = FormatNumber(account.Value.EndingBalance); // {{101000_2023}}
            }

            return placeholderData;
        }


        #endregion

        #region File Retrieval and Storage

        private byte[] GetFileContent(Guid? noteID)
        {
            if (noteID == null)
                throw new PXException(Messages.NoteIDIsNull);

            var uploadedFiles = new PXSelectJoin<UploadFile,
                InnerJoin<NoteDoc, On<UploadFile.fileID, Equal<NoteDoc.fileID>>>,
                Where<
                    NoteDoc.noteID, Equal<Required<NoteDoc.noteID>>,
                    And<UploadFile.name, Like<Required<UploadFile.name>>>>,
                OrderBy<Asc<UploadFile.createdDateTime>>>(this)
                .Select(noteID, "%FRTemplate%");

            if (uploadedFiles == null || uploadedFiles.Count == 0)
                throw new PXException(Messages.NoFilesAssociated);

            foreach (PXResult<UploadFile, NoteDoc> result in uploadedFiles)
            {
                var file = (UploadFile)result;
                var fileRevision = (UploadFileRevision)PXSelect<UploadFileRevision,
                    Where<UploadFileRevision.fileID, Equal<Required<UploadFileRevision.fileID>>>>
                    .Select(this, file.FileID).FirstOrDefault();

                if (fileRevision?.Data != null)
                {
                    return fileRevision.Data;
                }
            }

            throw new PXException(Messages.FailedToRetrieveFile);
        }


        private Guid SaveGeneratedDocument(string fileName, byte[] fileContent, FLRTFinancialReport currentRecord)
        {
            var fileGraph = PXGraph.CreateInstance<UploadFileMaintenance>();
            var fileInfo = new PX.SM.FileInfo(fileName, null, fileContent)
            {
                IsPublic = true
            };

            bool saved = fileGraph.SaveFile(fileInfo);
            if (!saved)
            {
                throw new PXException(Messages.UnableToSaveGeneratedFile);
            }

            if (fileInfo.UID.HasValue)
            {
                PXNoteAttribute.SetFileNotes(FinancialReport.Cache, currentRecord, fileInfo.UID.Value);
            }
            return fileInfo.UID ?? Guid.Empty;
        }

        #endregion

        #region Word Template Population

        private void PopulateTemplate(string templatePath, string outputPath, Dictionary<string, string> data)
        {
            File.Copy(templatePath, outputPath, true);
            using (WordprocessingDocument doc = WordprocessingDocument.Open(outputPath, true))
            {
                var mainPart = doc.MainDocumentPart;
                var paragraphs = mainPart.Document.Descendants<Paragraph>();

                foreach (var kvp in data)
                {
                    PXTrace.WriteInformation($"Placeholder: {kvp.Key}, Value: {kvp.Value}");
                }

                foreach (var kvp in data)
                {
                    string placeholder = kvp.Key;
                    string replacement = kvp.Value;

                    foreach (var paragraph in paragraphs)
                    {
                        ReplacePlaceholderInRuns(paragraph, placeholder, replacement);
                    }
                }
            }
        }

        private void ReplacePlaceholderInRuns(Paragraph paragraph, string placeholder, string replacement)
        {
            MergeRunsWithSameFormatting(paragraph);
            var runs = paragraph.Elements<Run>().ToList();

            for (int i = 0; i < runs.Count; i++)
            {
                Run run = runs[i];
                Text textElement = run.GetFirstChild<Text>();
                if (textElement == null) continue;

                string runText = textElement.Text;
                int idx;
                while ((idx = runText.IndexOf(placeholder, StringComparison.Ordinal)) >= 0)
                {
                    string before = runText.Substring(0, idx);
                    string after = runText.Substring(idx + placeholder.Length);

                    paragraph.RemoveChild(run);
                    runs.RemoveAt(i);

                    int insertPos = i;

                    if (!string.IsNullOrEmpty(before))
                    {
                        var beforeRun = CloneRunWithNewText(run, before);
                        paragraph.InsertBefore(beforeRun, insertPos < runs.Count ? runs[insertPos] : null);
                        runs.Insert(insertPos, beforeRun);
                        insertPos++;
                        i++;
                    }

                    var replacementRun = CloneRunWithNewText(run, replacement);
                    paragraph.InsertBefore(replacementRun, insertPos < runs.Count ? runs[insertPos] : null);
                    runs.Insert(insertPos, replacementRun);
                    insertPos++;
                    i++;

                    if (!string.IsNullOrEmpty(after))
                    {
                        run = CloneRunWithNewText(run, after);
                        paragraph.InsertBefore(run, insertPos < runs.Count ? runs[insertPos] : null);
                        runs.Insert(insertPos, run);

                        runText = after;
                    }
                    else
                    {
                        runText = string.Empty;
                        break;
                    }
                }
            }
        }

        private Run CloneRunWithNewText(Run originalRun, string newText)
        {
            var newRun = new Run();
            if (originalRun.RunProperties != null)
            {
                newRun.RunProperties = (RunProperties)originalRun.RunProperties.CloneNode(true);
            }
            newRun.AppendChild(new Text(newText));
            return newRun;
        }

        private void MergeRunsWithSameFormatting(Paragraph paragraph)
        {
            var runs = paragraph.Elements<Run>().ToList();
            for (int i = 0; i < runs.Count - 1;)
            {
                if (HaveSameFormatting(runs[i], runs[i + 1]))
                {
                    var text1 = runs[i].GetFirstChild<Text>();
                    var text2 = runs[i + 1].GetFirstChild<Text>();

                    if (text1 == null || text2 == null)
                    {
                        i++;
                        continue;
                    }

                    text1.Text += text2.Text;
                    runs[i + 1].Remove();
                    runs.RemoveAt(i + 1);
                }
                else
                {
                    i++;
                }
            }
        }

        private bool HaveSameFormatting(Run r1, Run r2)
        {
            string rp1 = r1.RunProperties?.OuterXml ?? string.Empty;
            string rp2 = r2.RunProperties?.OuterXml ?? string.Empty;
            return rp1 == rp2;
        }

        #endregion

        #region Adding download button

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
    }
}