//using FinancialReport.Helper;
using Newtonsoft.Json.Linq;
using PX.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FinancialReport.Services
{
    public class FinancialDataService
    {
        private readonly string _baseUrl;
        private readonly string _tenantName;
        private readonly AuthService _authService;

        // ✅ Static HttpClient shared across all instances and methods
        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            MaxConnectionsPerServer = 10
        })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        // ✅ Thread-safe semaphore for auth header updates
        private static readonly SemaphoreSlim _authSemaphore = new SemaphoreSlim(1, 1);

        static FinancialDataService()
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public FinancialDataService(AuthService authService, string tenantName)
        {
            AcumaticaCredentials credentials = CredentialProvider.GetCredentials(tenantName);
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _tenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName)); 
            _baseUrl = credentials.BaseURL ?? throw new ArgumentNullException(nameof(credentials.BaseURL));
        }

        // --------------------------------------------------------
        // 1) FetchAllApiData (with URL Fallback Logic)
        // --------------------------------------------------------
        public FinancialApiData FetchAllApiData(string branch, string organization, string ledger, string period)
        {
            string accessToken = _authService.AuthenticateAndGetToken();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string filter = $"FinancialPeriod eq '{period}' and {dimensionFilter}";

            var accountData = new Dictionary<string, FinancialPeriodData>();

            SetAuthorizationHeader(accessToken);
            List<JToken> results = ExecuteFetchWithFallback(_httpClient, filter, ledger);

            if (results == null)
            {
                throw new PXException(Messages.FailedToFetchOData);
            }

            foreach (var item in results)
            {
                string accountId = item["Account"]?.ToString();
                if (string.IsNullOrEmpty(accountId)) continue;

                if (!accountData.ContainsKey(accountId))
                {
                    accountData[accountId] = new FinancialPeriodData();
                }

                accountData[accountId].BeginningBalance += item["BeginningBalance"]?.ToObject<decimal>() ?? 0;
                accountData[accountId].EndingBalance += item["EndingBalance"]?.ToObject<decimal>() ?? 0;
                accountData[accountId].Debit += item["Debit"]?.ToObject<decimal>() ?? 0;
                accountData[accountId].Credit += item["Credit"]?.ToObject<decimal>() ?? 0;
            }

            return new FinancialApiData { AccountData = accountData };
        }

        // --------------------------------------------------------
        // 2) FetchJanuaryBeginningBalance
        // --------------------------------------------------------
        public FinancialApiData FetchJanuaryBeginningBalance(string branch, string organization, string ledger, string prevYear)
        {
            string januaryPeriod = "01" + prevYear;
            string accessToken = _authService.AuthenticateAndGetToken();
            string dimensionFilter = BuildDimensionFilter(branch, organization);
            string baseFilter = $"FinancialPeriod eq '{januaryPeriod}' and {dimensionFilter}";

            var apiData = new FinancialApiData();

            SetAuthorizationHeader(accessToken);
            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger);

            if (results == null)
            {
                throw new PXException(Messages.FailedToFetchOData);
            }

            foreach (var item in results)
            {
                string accountId = item["Account"]?.ToString();
                decimal beginningBalance = item["BeginningBalance"]?.ToObject<decimal>() ?? 0;

                if (!apiData.AccountData.ContainsKey(accountId))
                {
                    apiData.AccountData[accountId] = new FinancialPeriodData();
                }
                apiData.AccountData[accountId].BeginningBalance += beginningBalance;
                apiData.AccountData[accountId].EndingBalance += beginningBalance;
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
            string baseFilter = $"FinancialPeriod ge '{fromPeriod}' and FinancialPeriod le '{toPeriod}' and {dimensionFilter}";

            var cumulativeDict = new Dictionary<string, FinancialPeriodData>();

            SetAuthorizationHeader(accessToken);
            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger);

            if (results == null)
            {
                throw new PXException(Messages.FailedToFetchOData);
            }

            foreach (var item in results)
            {
                string accountId = item["Account"]?.ToString();
                decimal debit = item["Debit"]?.ToObject<decimal>() ?? 0;
                decimal credit = item["Credit"]?.ToObject<decimal>() ?? 0;
                decimal endingBalance = item["EndingBalance"]?.ToObject<decimal>() ?? 0;

                if (!cumulativeDict.ContainsKey(accountId))
                {
                    cumulativeDict[accountId] = new FinancialPeriodData();
                }

                cumulativeDict[accountId].Debit += debit;
                cumulativeDict[accountId].Credit += credit;
                cumulativeDict[accountId].EndingBalance += endingBalance;
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
            string baseFilter = $"FinancialPeriod eq '{period}' and 1 eq 1";

            var compositeData = new Dictionary<string, FinancialPeriodData>();

            SetAuthorizationHeader(accessToken);
            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger);

            if (results == null)
            {
                throw new PXException(Messages.FailedToFetchOData);
            }

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
                };

                compositeData[compositeKey] = data;
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
                string.IsNullOrEmpty(organization) ||
                string.IsNullOrEmpty(account) || string.IsNullOrEmpty(subaccount))
            {
                PXTrace.WriteWarning("FetchEndingBalance called with missing filter parameters.");
                return 0m;
            }

            string accessToken = _authService.AuthenticateAndGetToken();
            string baseFilter = $"FinancialPeriod eq '{period}' and BranchID eq '{branch}' and OrganizationID eq '{organization}' and " +
                               $"Account eq '{account}' and Subaccount eq '{subaccount}'";

            SetAuthorizationHeader(accessToken);
            var results = ExecuteFetchWithFallback(_httpClient, baseFilter, ledger);

            if (results == null || results.Count == 0)
            {
                PXTrace.WriteWarning($"No rows found for precise match: {baseFilter}");
                return 0m;
            }

            return results.FirstOrDefault()?["EndingBalance"]?.ToObject<decimal>() ?? 0m;
        }


        // ✅ Thread-safe method to set authorization header
        private static void SetAuthorizationHeader(string accessToken)
        {
            _authSemaphore.Wait();
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
            finally
            {
                _authSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes a fetch operation with fallback logic for both URL format and Ledger filtering.
        /// </summary>
        private List<JToken> ExecuteFetchWithFallback(HttpClient client, string baseFilter, string ledger)
        {
            string modernUrlBase = $"{_baseUrl}/odata/{_tenantName}/TrialBalance";
            string legacyUrlBase = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance";
            string selectColumns = "Account,BeginningBalance,EndingBalance,Debit,Credit";

            // Attempt 1: Modern URL with Ledger
            string filterWithLedger = AppendLedgerFilter(baseFilter, ledger);
            PXTrace.WriteInformation($"Attempt 1: Modern URL with Ledger. URL: {modernUrlBase}, Filter: {filterWithLedger}");
            var results = PaginatedFetch(client, modernUrlBase, filterWithLedger, selectColumns);
            if (results != null) return results;

            // Attempt 2: Modern URL without Ledger
            PXTrace.WriteWarning($"Attempt 1 failed. Retrying with Modern URL without Ledger. URL: {modernUrlBase}, Filter: {baseFilter}");
            results = PaginatedFetch(client, modernUrlBase, baseFilter, selectColumns);
            if (results != null) return results;

            // Attempt 3: Legacy URL with Ledger
            PXTrace.WriteWarning($"Attempt 2 failed. Retrying with Legacy URL with Ledger. URL: {legacyUrlBase}, Filter: {filterWithLedger}");
            results = PaginatedFetch(client, legacyUrlBase, filterWithLedger, selectColumns);
            if (results != null) return results;

            // Attempt 4: Legacy URL without Ledger
            PXTrace.WriteWarning($"Attempt 3 failed. Retrying with Legacy URL without Ledger. URL: {legacyUrlBase}, Filter: {baseFilter}");
            results = PaginatedFetch(client, legacyUrlBase, baseFilter, selectColumns);
            if (results != null) return results;

            PXTrace.WriteError("All fetch attempts failed.");
            return null;
        }

        /// <summary>
        /// Private helper method to execute a paginated OData fetch operation against a specific URL.
        /// </summary>
        private List<JToken> PaginatedFetch(HttpClient client, string baseUrl, string filter, string selectColumns)
        {
            var allResults = new List<JToken>();
            int pageSize = 5000;
            int skip = 0;

            while (true)
            {
                string pagedUrl = $"{baseUrl}?$filter={filter}&$select={selectColumns}&$top={pageSize}&$skip={skip}";

                HttpResponseMessage response;
                try
                {
                    response = client.GetAsync(pagedUrl).Result;
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"HTTP request to {pagedUrl} failed: {ex.Message}");
                    return null; // Return null to indicate failure
                }

                if (!response.IsSuccessStatusCode)
                {
                    // This is an expected failure for a fallback, so log as warning, not error.
                    PXTrace.WriteWarning($"Request to {pagedUrl} returned status {response.StatusCode}.");
                    return null; // Return null to indicate failure, triggering the next fallback.
                }

                string jsonResponse = response.Content.ReadAsStringAsync().Result;

                if (string.IsNullOrWhiteSpace(jsonResponse) || !jsonResponse.TrimStart().StartsWith("{"))
                {
                    PXTrace.WriteError("API returned a non-JSON or empty response.");
                    return null;
                }

                JObject parsed = JObject.Parse(jsonResponse);
                var pageResults = (parsed["value"] as JArray)?.ToObject<List<JToken>>();

                if (pageResults == null || pageResults.Count == 0)
                {
                    break; // Exit loop when no more pages are returned
                }

                allResults.AddRange(pageResults);
                skip += pageSize;
            }

            PXTrace.WriteInformation($"Successfully fetched {allResults.Count} total records for base URL: {baseUrl}");
            return allResults;
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

        private string AppendLedgerFilter(string baseFilter, string ledger)
        {
            return !string.IsNullOrEmpty(ledger)
                ? $"{baseFilter} and LedgerID eq '{ledger}'"
                : baseFilter;
        }


        public Dictionary<string, string> BuildPlaceholderMapFromKeys(List<string> placeholderKeys, Dictionary<string, FinancialPeriodData> cyData, Dictionary<string, FinancialPeriodData> pyData)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in placeholderKeys)
            {
                if (string.IsNullOrWhiteSpace(key) || !key.Contains("_"))
                {
                    continue;
                }

                var parts = key.Split('_');
                if (parts.Length != 2)
                {
                    continue;
                }

                string accountCode = parts[0];
                string period = parts[1].ToUpper();

                var source = period == "CY" ? cyData : period == "PY" ? pyData : null;

                if (source != null && source.TryGetValue(accountCode, out var data))
                {
                    string value = data.EndingBalance.ToString("N2");
                    result[key] = value;
                }
                else
                {
                    result[key] = "0";
                }
            }
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
            var dict = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Parallel.ForEach(keys, key =>
            {
                try
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

                        var source = yearType == "CY"
                            ? (type == "begsum" ? janCY : (type == "debitsum" || type == "creditsum" ? rangeCY : cyData))
                            : (type == "begsum" ? janPY : (type == "debitsum" || type == "creditsum" ? rangePY : pyData));

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
                        return;
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

                        return;
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

                        return;
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

                        return;
                    }

                    // Fallback
                    dict[key] = "0";
                }
                catch (Exception ex)
                {
                    PXTrace.WriteWarning($"[Parallel Placeholder] Failed to process key '{key}': {ex.Message}");
                    dict[key] = "0";
                }
            });

            return new Dictionary<string, string>(dict, StringComparer.OrdinalIgnoreCase);
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


        // ✅ Optional: Static cleanup method
        public static void Cleanup()
        {
            try
            {
                _httpClient?.Dispose();
                _authSemaphore?.Dispose();
                PXTrace.WriteInformation("FinancialDataService: Static resources cleaned up");
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Error during cleanup: {ex.Message}");
            }
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
    }

    public class FinancialApiData
    {
        public Dictionary<string, FinancialPeriodData> AccountData { get; set; } = new Dictionary<string, FinancialPeriodData>();

        // 🔥 New: Optional composite key-level data
        public Dictionary<string, FinancialPeriodData> CompositeKeyData { get; set; } = new Dictionary<string, FinancialPeriodData>();
    }
}
