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

namespace FinancialReport
{
    public class FLRTFinancialReportMaint : PXGraph<FLRTFinancialReportMaint>
    {
        public SelectFrom<FLRTFinancialReport>.View FinancialReport;

        private string _accessToken = null;
        private string _refreshToken = null;
        private DateTime _tokenExpiry = DateTime.MinValue;

        private string GetConfigValue(string key)
        {
            return ConfigurationManager.AppSettings[key] ?? throw new PXException(Messages.MissingConfig);
        }

        private string _baseUrl => GetConfigValue("Acumatica.BaseUrl");
        private string _clientId => GetConfigValue("Acumatica.ClientId");
        private string _clientSecret => GetConfigValue("Acumatica.ClientSecret");
        private string _username => GetConfigValue("Acumatica.Username");
        private string _password => GetConfigValue("Acumatica.Password");



        #region Current Year Value Update
        protected void FLRTFinancialReport_CurrYear_FieldUpdated(PXCache cache, PXFieldUpdatedEventArgs e)
        {
            var row = (FLRTFinancialReport)e.Row;
            if (row != null)
            {
                PXTrace.WriteInformation($"CurrYear Updated to: {row.CurrYear}");
            }
        }

        #endregion

        #region Authentication and Token Management

        private string AuthenticateAndGetToken()
        {
            if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.Now)
            {
                PXTrace.WriteInformation("Reusing existing access token.");
                return _accessToken;
            }

            if (!string.IsNullOrEmpty(_refreshToken))
            {
                PXTrace.WriteInformation("Attempting to refresh access token using refresh token...");
                try
                {
                    return RefreshAccessToken(_refreshToken);
                }
                catch (PXException ex)
                {
                    PXTrace.WriteError($"Refresh token failed: {ex.Message}. Falling back to password grant.");
                }
            }

            PXTrace.WriteInformation("Requesting a new access token...");
            string tokenUrl = $"{_baseUrl}/identity/connect/token";

            using (HttpClient client = new HttpClient())
            {
                var tokenRequest = new[]
                {
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("username", _username),
                    new KeyValuePair<string, string>("password", _password),
                    new KeyValuePair<string, string>("scope", "api")
                };

                HttpResponseMessage tokenResponse = client.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequest)).Result;

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    PXTrace.WriteError($"Authentication failed: {tokenResponse.StatusCode}");
                    throw new PXException(Messages.FailedToAuthenticate);
                }

                string responseContent = tokenResponse.Content.ReadAsStringAsync().Result;
                JObject tokenResult = JObject.Parse(responseContent);

                _accessToken = tokenResult["access_token"]?.ToString() ?? throw new PXException(Messages.AccessTokenNotFound);
                _refreshToken = tokenResult["refresh_token"]?.ToString() ?? string.Empty;

                int expiresIn = tokenResult["expires_in"]?.ToObject<int>() ?? 0;
                if (expiresIn == 0)
                {
                    throw new PXException(Messages.TokenExpirationNotFound);
                }
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

                PXTrace.WriteInformation("New access token retrieved.");
                return _accessToken;
            }
        }


        private string RefreshAccessToken(string refreshToken)
        {
            string tokenUrl = $"{_baseUrl}/identity/connect/token";
            using (HttpClient client = new HttpClient())
            {
                var tokenRequest = new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("refresh_token", refreshToken)
                };

                HttpResponseMessage tokenResponse = client.PostAsync(tokenUrl, new FormUrlEncodedContent(tokenRequest)).Result;
                if (!tokenResponse.IsSuccessStatusCode)
                {
                    string errorContent = tokenResponse.Content.ReadAsStringAsync().Result;
                    PXTrace.WriteError($"Failed to refresh access token: {tokenResponse.StatusCode}, Response: {errorContent}");
                    throw new PXException(Messages.FailedToRefreshToken);
                }

                string responseContent = tokenResponse.Content.ReadAsStringAsync().Result;
                JObject tokenResult = JObject.Parse(responseContent);

                // Retrieve and save access token
                _accessToken = tokenResult["access_token"]?.ToString();
                if (string.IsNullOrEmpty(_accessToken))
                {
                    throw new PXException(Messages.AccessTokenNotFound);
                }

                // Set token expiry
                int expiresIn = tokenResult["expires_in"]?.ToObject<int>() ?? 0;
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

                PXTrace.WriteInformation("Access token successfully refreshed.");
                return _accessToken;
            }
        }

        private void Logout()
        {
            try
            {
                string logoutUrl = $"{_baseUrl}/entity/auth/logout";
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    HttpResponseMessage response = client.PostAsync(logoutUrl, null).Result;

                    if (response.IsSuccessStatusCode)
                    {
                        PXTrace.WriteInformation("Successfully logged out from Acumatica API.");
                    }
                    else
                    {
                        string errorResponse = response.Content.ReadAsStringAsync().Result;
                        PXTrace.WriteError($"Failed to logout. Status Code: {response.StatusCode}, Response: {errorResponse}");
                    }
                }

                _accessToken = null;
                _refreshToken = null;
                _tokenExpiry = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Error during logout: {ex.Message}");
            }
        }

        #endregion

        #region Data Models

        private class FinancialApiData
        {
            public Dictionary<string, (string EndingBalance, string Description)> AccountData { get; set; }
                = new Dictionary<string, (string EndingBalance, string Description)>();
        }

        #endregion

        #region Data Fetching

        private Dictionary<string, (decimal EndingBalance, string Description)> FetchEndingBalances(
            HttpClient client, string branch, string ledger, string period, List<string> accounts)
        {
            string apiUrl = $"{_baseUrl}/entity/AccountsFilter/24.200.001/AccountBySubaccount";
            var accountData = new Dictionary<string, (decimal EndingBalance, string Description)>();

            foreach (var account in accounts)
            {
                try
                {
                    var payload = new
                    {
                        Company_Branch = new { value = branch },
                        Ledger = new { value = ledger },
                        Period = new { value = period },
                        Account = new { value = account }
                    };

                    string payloadJson = JsonConvert.SerializeObject(payload);
                    var httpContent = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = client.PutAsync(apiUrl, httpContent).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                        string errorResponse = response.Content.ReadAsStringAsync().Result;
                        PXTrace.WriteError($"PUT request failed for Account {account}. Status: {response.StatusCode}, Response: {errorResponse}");
                        continue;
                    }

                    string jsonResponse = response.Content.ReadAsStringAsync().Result;
                    var parsed = JObject.Parse(jsonResponse);

                    string endingBalanceStr = parsed["EndingBalance"]?["value"]?.ToString();
                    string description = parsed["Description"]?["value"]?.ToString() ?? "No Description";

                    if (decimal.TryParse(endingBalanceStr, out decimal endingBalance))
                    {
                        PXTrace.WriteInformation($"Account {account}: EndingBalance = {endingBalance}, Description = {description}");
                        accountData[account] = (endingBalance, description);
                    }
                    else
                    {
                        PXTrace.WriteError($"Invalid or missing EndingBalance for Account {account}");
                    }
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Error fetching data for Account {account}: {ex.Message}");
                }
            }
            return accountData;
        }

        private FinancialApiData FetchAllApiData(string period)
        {
            string accessToken = AuthenticateAndGetToken();
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Define the accounts to fetch data for
                var accounts = new List<string>
                {
                    "101000", "102000", "102050", "104000", "105000",
                    "110000", "120000", "130000", "138000", "139000",
                    "140000", "150000", "190000",
                    "200000", "200010", "200011", "210000", "213000",
                    "215000", "230000", "244000", "250020", "270800",
                    "301000", "302000", "303000", "403000", "405000",
                    "410000", "431000", "432000", "435000", "440000",
                    "455000", "460000", "490000", "520000", "530000",
                    "540000", "550000", "595000", "610000", "615000",
                    "620000", "630000", "631000", "675000", "740000",
                    "745000", "755000", "758000", "760000", "770000",
                    "790000", "999999"
                };

                // Fetch data for the provided period
                var accountData = FetchEndingBalances(client, "SOFT", "ACTUALSOFT", period, accounts);

                // Build the result object
                var apiData = new FinancialApiData();
                foreach (var kvp in accountData)
                {
                    apiData.AccountData[kvp.Key] = (kvp.Value.EndingBalance.ToString(), kvp.Value.Description);
                }

                return apiData;
            }
        }


        #endregion

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

        public PXSave<FLRTFinancialReport> Save;
        public PXCancel<FLRTFinancialReport> Cancel;

        public PXAction<FLRTFinancialReport> GenerateReport;
        [PXButton(CommitChanges = false)]
        [PXUIField(DisplayName = "Generate Report")]
        protected virtual void generateReport()
        {
            GenerateFinancialReport();
            PXTrace.WriteInformation("Generate Report button was pressed.");
        }

        #endregion

        #region Main Report Generation GenerateFinancialReport()

        private void GenerateFinancialReport()
        {
            try
            {
                // Step 1: Get the selected record
                var selectedRecord = FinancialReport.Cache
                    .Cached
                    .Cast<FLRTFinancialReport>()
                    .FirstOrDefault(item => item.Selected == true);

                if (selectedRecord == null)
                    throw new PXException(Messages.PleaseSelectTemplate);

                if (selectedRecord.Noteid == null)
                    throw new PXException(Messages.TemplateHasNoFiles);

                // Fetch template file content
                var templateFileContent = GetFileContent(selectedRecord.Noteid);
                if (templateFileContent == null || templateFileContent.Length == 0)
                    throw new PXException(Messages.TemplateFileIsEmpty);

                // Create paths for template and output
                string templatePath = Path.Combine(Path.GetTempPath(), $"{selectedRecord.ReportCD}_Template.docx");
                File.WriteAllBytes(templatePath, templateFileContent);

                string uniqueFileName = $"{selectedRecord.ReportCD}_Generated_{DateTime.Now:yyyyMMdd_HHmmssfff}.docx";
                string outputPath = Path.Combine(Path.GetTempPath(), uniqueFileName);

                // Step 2: Fetch data for CurrYear and PrevYear
                string currYear = selectedRecord?.CurrYear ?? DateTime.Now.ToString("yyyy");
                int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
                string prevYear = (currYearInt - 1).ToString();

                PXTrace.WriteInformation($"Fetching data for CurrYear: {currYear}");
                var currYearData = FetchAllApiData($"12{currYear}"); // Fetch data for the current year

                PXTrace.WriteInformation($"Fetching data for PrevYear: {prevYear}");
                var prevYearData = FetchAllApiData($"12{prevYear}"); // Fetch data for the previous year

                // Step 3: Prepare placeholder data (use the fetched data)
                var placeholderData = GetPlaceholderData(currYearData, prevYearData);

                // Step 4: Populate the template with the placeholder data
                PopulateTemplate(templatePath, outputPath, placeholderData);

                // Step 5: Upload the generated document
                byte[] generatedFileContent = File.ReadAllBytes(outputPath);
                Guid fileID = SaveGeneratedDocument(uniqueFileName, generatedFileContent, selectedRecord);

                // Redirect to the generated file
                throw new PXRedirectToFileException(fileID, 1, false);
            }
            finally
            {
                // Always log out from the external API
                Logout();
            }
        }


        private Dictionary<string, string> GetPlaceholderData(FinancialApiData currYearData, FinancialApiData prevYearData)
        {
            var selectedRecord = FinancialReport.Current;
            string currYear = selectedRecord?.CurrYear ?? DateTime.Now.ToString("yyyy");

            // Validate CurrYear
            if (string.IsNullOrEmpty(currYear))
                throw new PXException(Messages.CurrentYearNotSpecified);

            // Compute PrevYear
            int currYearInt = int.TryParse(currYear, out int parsedYear) ? parsedYear : DateTime.Now.Year;
            string prevYear = (currYearInt - 1).ToString();

            var placeholderData = new Dictionary<string, string>
            {
                { "{{branchName}}", "Censof-Test" },
                { "{{testData}}", DateTime.Now.ToShortDateString() },
                { "{{month/year}}", DateTime.Now.ToString("MMMM dd, yyyy") },
                { "{{curryear}}", currYear },
                { "{{currmonth}}", DateTime.Now.ToString("MMMM") },
                { "{{prevyear}}", prevYear }
            };

            // Add fetched data for CurrYear
            foreach (var account in currYearData.AccountData)
            {
                placeholderData[$"{{{{{account.Key}_CY}}}}"] = account.Value.EndingBalance; // {{101000_2024}}
                placeholderData[$"{{{{description_{account.Key}_CY}}}}"] = account.Value.Description; // {{description_101000_CurrYear}}
            }

            // Add fetched data for PrevYear
            foreach (var account in prevYearData.AccountData)
            {
                placeholderData[$"{{{{{account.Key}_PY}}}}"] = account.Value.EndingBalance; // {{101000_2023}}
            }

            return placeholderData;
        }


        #endregion

        #region File Retrieval and Storage

        private byte[] GetFileContent(Guid? noteID)
        {
            if (noteID == null)
                throw new PXException(Messages.NoteIDIsNull);

            var uploadedFiles = PXSelectJoin<UploadFile,
                InnerJoin<NoteDoc, On<UploadFile.fileID, Equal<NoteDoc.fileID>>>,
                Where<NoteDoc.noteID, Equal<Required<NoteDoc.noteID>>>>
                .Select(this, noteID);

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
    }
}