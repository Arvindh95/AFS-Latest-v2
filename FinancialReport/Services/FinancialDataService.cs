using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using FinancialReport.Helper;
using Newtonsoft.Json.Linq;
using PX.Data;

namespace FinancialReport.Services
{
    public class FinancialDataService
    {
        private readonly string _baseUrl;
        private readonly string _tenantName;
        private readonly AuthService _authService;

        public FinancialDataService(string baseUrl, AuthService authService, string tenantName)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _tenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
        }

        // --------------------------------------------------------
        // 1) FetchAllApiData
        // --------------------------------------------------------
        public FinancialApiData FetchAllApiData(string branch, string organization, string ledger, string period)
        {
            string accessToken = _authService.AuthenticateAndGetToken();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string filter = $"FinancialPeriod eq '{period}' and {dimensionFilter} and LedgerID eq '{ledger}'";
            string selectColumns = "Account,BeginningBalance,EndingBalance,Debit,Credit,Description";
            int pageSize = 1000;
            int skip = 0;

            var accountData = new Dictionary<string, FinancialPeriodData>();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                while (true)
                {
                    string pagedUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance" +
                                      $"?$filter={filter}&$select={selectColumns}&$top={pageSize}&$skip={skip}";

                    HttpResponseMessage response = client.GetAsync(pagedUrl).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorResponse = response.Content.ReadAsStringAsync().Result;
                        PXTrace.WriteError($"GET request failed. Status: {response.StatusCode}, Response: {errorResponse}");
                        throw new PXException(Messages.FailedToFetchOData);
                    }

                    string jsonResponse = response.Content.ReadAsStringAsync().Result;
                    JObject parsed = JObject.Parse(jsonResponse);
                    var results = (parsed["value"] as JArray)?.ToObject<List<JToken>>();

                    //PXTrace.WriteInformation($"Fetched {results?.Count ?? 0} records (skip = {skip})");

                    if (results == null || results.Count == 0)
                        break;

                    foreach (var item in results)
                    {
                        string accountId = item["Account"]?.ToString();
                        if (!accountData.ContainsKey(accountId))
                        {
                            accountData[accountId] = new FinancialPeriodData
                            {
                                BeginningBalance = item["BeginningBalance"]?.ToObject<decimal>() ?? 0,
                                EndingBalance = item["EndingBalance"]?.ToObject<decimal>() ?? 0,
                                Debit = item["Debit"]?.ToObject<decimal>() ?? 0,
                                Credit = item["Credit"]?.ToObject<decimal>() ?? 0,
                                Description = item["Description"]?.ToString() ?? "No Description"
                            };
                        }
                    }

                    skip += pageSize;
                }
            }

            return new FinancialApiData { AccountData = accountData };
        }
        // --------------------------------------------------------
        // 2) FetchJanuaryBeginningBalance
        // --------------------------------------------------------
        public FinancialApiData FetchJanuaryBeginningBalance(string branch, string organization, string ledger, string prevYear)
        {
            string januaryPeriod = "01" + prevYear; // e.g. "012023"
            string accessToken = _authService.AuthenticateAndGetToken();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string filter = $"FinancialPeriod eq '{januaryPeriod}' and {dimensionFilter} and LedgerID eq '{ledger}'";
            string selectColumns = "Account,BeginningBalance,Description";
            int pageSize = 1000;
            int skip = 0;

            var apiData = new FinancialApiData();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                while (true)
                {
                    string pagedUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance" +
                                      $"?$filter={filter}&$select={selectColumns}&$top={pageSize}&$skip={skip}";

                    HttpResponseMessage response = client.GetAsync(pagedUrl).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorResponse = response.Content.ReadAsStringAsync().Result;
                        PXTrace.WriteError($"GET request failed. Status: {response.StatusCode}, Response: {errorResponse}");
                        throw new PXException(Messages.FailedToFetchOData);
                    }

                    string jsonResponse = response.Content.ReadAsStringAsync().Result;
                    JObject parsed = JObject.Parse(jsonResponse);
                    var results = (parsed["value"] as JArray)?.ToObject<List<JToken>>();

                    //PXTrace.WriteInformation($"[January Balance] Fetched {results?.Count ?? 0} records (skip = {skip})");

                    if (results == null || results.Count == 0)
                        break;

                    foreach (var item in results)
                    {
                        string accountId = item["Account"]?.ToString();
                        decimal beginningBalance = item["BeginningBalance"]?.ToObject<decimal>() ?? 0;
                        string description = item["Description"]?.ToString() ?? "No Description";

                        apiData.AccountData[accountId] = new FinancialPeriodData
                        {
                            BeginningBalance = beginningBalance,
                            EndingBalance = beginningBalance,
                            Description = description
                        };
                    }

                    skip += pageSize;
                }
            }

            return apiData;
        }

        // --------------------------------------------------------
        // 3) FetchRangeApiData
        // --------------------------------------------------------
        public FinancialApiData FetchRangeApiData(string branch, string organization, string ledger, string fromPeriod, string toPeriod)
        {
            string accessToken = _authService.AuthenticateAndGetToken();
            string dimensionFilter = BuildDimensionFilter(branch, organization);

            string filter = $"FinancialPeriod ge '{fromPeriod}' and FinancialPeriod le '{toPeriod}' and {dimensionFilter} and LedgerID eq '{ledger}'";
            string selectColumns = "Account,Debit,Credit,Description,FinancialPeriod,EndingBalance,BranchID,OrganizationID";
            int pageSize = 1000;
            int skip = 0;

            var cumulativeDict = new Dictionary<string, FinancialPeriodData>();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                while (true)
                {
                    string pagedUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance" +
                                      $"?$filter={filter}&$select={selectColumns}&$top={pageSize}&$skip={skip}";

                    HttpResponseMessage response = client.GetAsync(pagedUrl).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorResponse = response.Content.ReadAsStringAsync().Result;
                        PXTrace.WriteError($"GET request failed. Status: {response.StatusCode}, Response: {errorResponse}");
                        throw new PXException(Messages.FailedToFetchOData);
                    }

                    string jsonResponse = response.Content.ReadAsStringAsync().Result;
                    JObject parsed = JObject.Parse(jsonResponse);
                    var results = (parsed["value"] as JArray)?.ToObject<List<JToken>>();

                    //PXTrace.WriteInformation($"[Range] Fetched {results?.Count ?? 0} records (skip = {skip})");

                    if (results == null || results.Count == 0)
                        break;

                    foreach (var item in results)
                    {
                        string accountId = item["Account"]?.ToString();
                        decimal debit = item["Debit"]?.ToObject<decimal>() ?? 0;
                        decimal credit = item["Credit"]?.ToObject<decimal>() ?? 0;
                        string description = item["Description"]?.ToString() ?? "No Description";
                        decimal endingBalance = item["EndingBalance"]?.ToObject<decimal>() ?? 0;

                        if (!cumulativeDict.ContainsKey(accountId))
                        {
                            cumulativeDict[accountId] = new FinancialPeriodData
                            {
                                Description = description
                            };
                        }

                        cumulativeDict[accountId].Debit += debit;
                        cumulativeDict[accountId].Credit += credit;
                        cumulativeDict[accountId].EndingBalance = endingBalance;
                    }

                    skip += pageSize;
                }
            }

            var apiData = new FinancialApiData();
            foreach (var kvp in cumulativeDict)
            {
                apiData.AccountData[kvp.Key] = kvp.Value;
            }

            return apiData;
        }

        // --------------------------------------------------------
        // 4) FetchCompositeKeyData
        // --------------------------------------------------------
        public FinancialApiData FetchCompositeKeyData(string branch, string organization, string ledger, string period)
        {
            string accessToken = _authService.AuthenticateAndGetToken();

            string filter = $"FinancialPeriod eq '{period}' and 1 eq 1 and LedgerID eq '{ledger}'";
            string selectColumns = "Account,Subaccount,BeginningBalance,EndingBalance,Debit,Credit,Description,BranchID,OrganizationID";
            int pageSize = 1000;
            int skip = 0;

            var compositeData = new Dictionary<string, FinancialPeriodData>();

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                while (true)
                {
                    string pagedUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance" +
                                      $"?$filter={filter}&$select={selectColumns}&$top={pageSize}&$skip={skip}";

                    HttpResponseMessage response = client.GetAsync(pagedUrl).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        string errorResponse = response.Content.ReadAsStringAsync().Result;
                        PXTrace.WriteError($"GET request failed. Status: {response.StatusCode}, Response: {errorResponse}");
                        throw new PXException(Messages.FailedToFetchOData);
                    }

                    string jsonResponse = response.Content.ReadAsStringAsync().Result;
                    JObject parsed = JObject.Parse(jsonResponse);
                    var results = (parsed["value"] as JArray)?.ToObject<List<JToken>>();

                    //PXTrace.WriteInformation($"[Composite] Fetched {results?.Count ?? 0} records (skip = {skip})");

                    if (results == null || results.Count == 0)
                        break;

                    foreach (var item in results)
                    {
                        string accountId = item["Account"]?.ToString()?.Trim();
                        string subaccountId = item["Subaccount"]?.ToString()?.Trim() ?? "N/A";
                        string branchId = item["BranchID"]?.ToString()?.Trim() ?? branch;
                        string orgId = item["OrganizationID"]?.ToString()?.Trim() ?? organization;
                        string compositeKey = $"{accountId}-{subaccountId}-{branchId}-{orgId}-{period}-{ledger}";

                        var data = new FinancialPeriodData
                        {
                            Account = accountId,
                            Subaccount = subaccountId,
                            BeginningBalance = item["BeginningBalance"]?.ToObject<decimal>() ?? 0,
                            EndingBalance = item["EndingBalance"]?.ToObject<decimal>() ?? 0,
                            Debit = item["Debit"]?.ToObject<decimal>() ?? 0,
                            Credit = item["Credit"]?.ToObject<decimal>() ?? 0,
                            Description = item["Description"]?.ToString() ?? "No Description"
                        };

                        compositeData[compositeKey] = data;
                    }

                    skip += pageSize;
                }
            }

            var apiData = new FinancialApiData();
            foreach (var kvp in compositeData)
            {
                apiData.CompositeKeyData[kvp.Key] = kvp.Value;
            }

            return apiData;
        }

        // --------------------------------------------------------
        // 5) FetchEndingBalance
        // --------------------------------------------------------
        public decimal FetchEndingBalance(string period, string branch, string organization, string ledger, string account, string subaccount)
        {
            if (string.IsNullOrEmpty(period) || string.IsNullOrEmpty(branch) ||
                string.IsNullOrEmpty(organization) || string.IsNullOrEmpty(ledger) ||
                string.IsNullOrEmpty(account) || string.IsNullOrEmpty(subaccount))
            {
                PXTrace.WriteWarning("FetchEndingBalance called with missing filter parameters.");
                return 0m;
            }

            string accessToken = _authService.AuthenticateAndGetToken();

            string filter =
                $"FinancialPeriod eq '{period}' and BranchID eq '{branch}' and OrganizationID eq '{organization}' and " +
                $"LedgerID eq '{ledger}' and Account eq '{account}' and Subaccount eq '{subaccount}'";

            string selectColumns = "EndingBalance"; // keep it minimal for performance

            string odataUrl =
                $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance" +
                $"?$filter={filter}&$select={selectColumns}";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                HttpResponseMessage response = client.GetAsync(odataUrl).Result;
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = response.Content.ReadAsStringAsync().Result;
                    PXTrace.WriteError($"GET request failed. Status: {response.StatusCode}, Response: {errorResponse}");
                    throw new PXException(Messages.FailedToFetchOData);
                }

                string jsonResponse = response.Content.ReadAsStringAsync().Result;
                JObject parsed = JObject.Parse(jsonResponse);
                var firstRow = (parsed["value"] as JArray)?.FirstOrDefault();

                if (firstRow == null)
                {
                    PXTrace.WriteWarning($"No rows found for precise match: {odataUrl}");
                    return 0m;
                }

                return firstRow["EndingBalance"]?.ToObject<decimal>() ?? 0m;
            }
        }

        // --------------------------------------------------------
        // HELPER: BuildDimensionFilter
        // --------------------------------------------------------
        private string BuildDimensionFilter(string branch, string organization)
        {
            // Handle the scenario where both or either is selected
            if (!string.IsNullOrEmpty(branch) && !string.IsNullOrEmpty(organization))
            {
                // Return a filter that requires BOTH match
                return $"BranchID eq '{branch}' and OrganizationID eq '{organization}'";
            }
            else if (!string.IsNullOrEmpty(branch))
            {
                return $"BranchID eq '{branch}'";
            }
            else if (!string.IsNullOrEmpty(organization))
            {
                return $"OrganizationID eq '{organization}'";
            }
            else
            {
                // Nothing selected => user must pick one
                return $"1 eq 1";
            }
        }


        public Dictionary<string, string> BuildPlaceholderMapFromKeys(List<string> placeholderKeys,Dictionary<string, FinancialPeriodData> cyData,Dictionary<string, FinancialPeriodData> pyData)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            TraceLogger.Info($"🔍 Building placeholder map from {placeholderKeys.Count} keys...");

            foreach (var key in placeholderKeys)
            {
                if (string.IsNullOrWhiteSpace(key) || !key.Contains("_"))
                {
                    TraceLogger.Error($"⚠️ Skipped invalid placeholder key: '{key}'");
                    continue;
                }

                var parts = key.Split('_');
                if (parts.Length != 2)
                {
                    TraceLogger.Error($"⚠️ Malformed placeholder (should be 'CODE_CY' or 'CODE_PY'): '{key}'");
                    continue;
                }

                string accountCode = parts[0];
                string period = parts[1].ToUpper();

                var source = period == "CY" ? cyData : period == "PY" ? pyData : null;

                if (source != null && source.TryGetValue(accountCode, out var data))
                {
                    string value = data.EndingBalance.ToString("N2");
                    result[key] = value;
                    //TraceLogger.Info($"✅ Matched placeholder: {key} → {value}");
                }
                else
                {
                    result[key] = "0";
                    //TraceLogger.Error($"🚫 No match for placeholder: {key}. Defaulted to 0.");
                }
            }

            TraceLogger.Info($"✅ Finished building placeholder map. Total mapped: {result.Count}");
            return result;
        }

        public Dictionary<string, string> BuildSmartPlaceholderMapFromKeys(
            List<string> keys,
            FinancialApiData cyData,
            FinancialApiData pyData,
            FinancialApiData janCY,
            FinancialApiData janPY,
            FinancialApiData rangeCY,
            FinancialApiData rangePY)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in keys)
            {
                string cleanKey = key.Trim('{', '}');

                // Match: CreditSum3_B1234_CY
                var sumMatch = Regex.Match(cleanKey, @"^(CreditSum|DebitSum|BegSum|Sum)(\d)_(.+)_(CY|PY)$", RegexOptions.IgnoreCase);
                if (sumMatch.Success)
                {
                    var type = sumMatch.Groups[1].Value.ToLower();
                    int level = int.Parse(sumMatch.Groups[2].Value);
                    string prefix = sumMatch.Groups[3].Value;
                    string yearType = sumMatch.Groups[4].Value.ToUpper();

                    var source = yearType == "CY" ? (type == "begsum" ? janCY : (type == "debitsum" || type == "creditsum" ? rangeCY : cyData)) :
                                                    (type == "begsum" ? janPY : (type == "debitsum" || type == "creditsum" ? rangePY : pyData));

                    decimal sum = 0;
                    foreach (var kvp in source.AccountData)
                    {
                        if (kvp.Key.Length >= level && kvp.Key.Substring(0, level) == prefix)
                        {
                            var data = kvp.Value;
                            switch (type)
                            {
                                case "debitsum":
                                    sum += data.Debit;
                                    break;
                                case "creditsum":
                                    sum += data.Credit;
                                    break;
                                case "begsum":
                                    sum += data.BeginningBalance;
                                    break;
                                default:
                                    sum += data.EndingBalance;
                                    break;
                            }

                        }
                    }
                    dict[key] = sum.ToString("#,##0");
                    continue;
                }

                // Match: A81101_Jan1_PY
                var janMatch = Regex.Match(cleanKey, @"^(.+)_Jan1_(CY|PY)$", RegexOptions.IgnoreCase);
                if (janMatch.Success)
                {
                    var acct = janMatch.Groups[1].Value;
                    var yearType = janMatch.Groups[2].Value.ToUpper();
                    var source = yearType == "CY" ? janCY : janPY;

                    if (source.AccountData.TryGetValue(acct, out var data))
                    {
                        dict[key] = data.BeginningBalance.ToString("#,##0");
                    }
                    continue;
                }

                // Match: B1234_credit_CY
                var partMatch = Regex.Match(cleanKey, @"^(.+?)_(credit|debit|ending)_(CY|PY)$", RegexOptions.IgnoreCase);
                if (partMatch.Success)
                {
                    var acct = partMatch.Groups[1].Value;
                    var part = partMatch.Groups[2].Value.ToLower();
                    var yearType = partMatch.Groups[3].Value.ToUpper();
                    var source = yearType == "CY" ? rangeCY : rangePY;

                    if (source.AccountData.TryGetValue(acct, out var data))
                    {
                        switch (part)
                        {
                            case "credit":
                                dict[key] = data.Credit.ToString("#,##0");
                                break;
                            case "debit":
                                dict[key] = data.Debit.ToString("#,##0");
                                break;
                            default:
                                dict[key] = data.EndingBalance.ToString("#,##0");
                                break;
                        }

                    }
                    continue;
                }

                // Match: A123456_CY / A123456_PY
                var basicMatch = Regex.Match(cleanKey, @"^(.+)_(CY|PY)$", RegexOptions.IgnoreCase);
                if (basicMatch.Success)
                {
                    var acct = basicMatch.Groups[1].Value;
                    var yearType = basicMatch.Groups[2].Value.ToUpper();
                    var source = yearType == "CY" ? cyData : pyData;

                    if (source.AccountData.TryGetValue(acct, out var data))
                    {
                        dict[key] = data.EndingBalance.ToString("#,##0");
                    }
                }
            }

            return dict;
        }


        public Dictionary<string, string> BuildAllFinancialDataMap(
        FinancialApiData currYearData,
        FinancialApiData prevYearData,
        FinancialApiData januaryBeginningDataCY,
        FinancialApiData januaryBeginningDataPY,
        FinancialApiData cumulativeCYData,
        FinancialApiData cumulativePYData)
        {
            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void AddAccountData(FinancialApiData data, string suffix)
            {
                foreach (var kvp in data.AccountData)
                {
                    string key = kvp.Key;
                    var val = kvp.Value;
                    placeholders[$"{key}_Ending_{suffix}"] = val.EndingBalance.ToString("#,##0.##");
                    placeholders[$"{key}_Debit_{suffix}"] = val.Debit.ToString("#,##0.##");
                    placeholders[$"{key}_Credit_{suffix}"] = val.Credit.ToString("#,##0.##");
                    placeholders[$"{key}_Desc_{suffix}"] = val.Description ?? "";
                    placeholders[$"{key}_Beg_{suffix}"] = val.BeginningBalance.ToString("#,##0.##");
                }
            }

            AddAccountData(currYearData, "CY");
            AddAccountData(prevYearData, "PY");
            AddAccountData(januaryBeginningDataCY, "JanBegCY");
            AddAccountData(januaryBeginningDataPY, "JanBegPY");
            AddAccountData(cumulativeCYData, "RangeCY");
            AddAccountData(cumulativePYData, "RangePY");

            return placeholders;
        }





    }



    // Classes for convenience
    public class FinancialPeriodData
    {
        public string Account { get; set; }
        public string Subaccount { get; set; }
        public decimal BeginningBalance { get; set; }
        public decimal EndingBalance { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Description { get; set; }
    }

    public class FinancialApiData
    {
        public Dictionary<string, FinancialPeriodData> AccountData { get; set; } = new Dictionary<string, FinancialPeriodData>();

        // 🔥 New: Optional composite key-level data
        public Dictionary<string, FinancialPeriodData> CompositeKeyData { get; set; } = new Dictionary<string, FinancialPeriodData>();
    }
}
