using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
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

            // Build dimension filter for either branch, org, or both
            string dimensionFilter = BuildDimensionFilter(branch, organization);

            string filter = $"FinancialPeriod eq '{period}' and {dimensionFilter} and LedgerID eq '{ledger}'";
            string selectColumns = "Account,BeginningBalance,EndingBalance,Debit,Credit,Description";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                string odataUrl =
                    $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance" +
                    $"?$filter={filter}&$select={selectColumns}";

                //PXTrace.WriteInformation($"Fetching period data from OData: {odataUrl}");

                HttpResponseMessage response = client.GetAsync(odataUrl).Result;
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = response.Content.ReadAsStringAsync().Result;
                    PXTrace.WriteError($"GET request failed. Status: {response.StatusCode}, Response: {errorResponse}");
                    throw new PXException(Messages.FailedToFetchOData);
                }

                string jsonResponse = response.Content.ReadAsStringAsync().Result;
                PXTrace.WriteInformation($"OData Raw Response: {jsonResponse}");
                JObject parsedResponse = JObject.Parse(jsonResponse);

                var accountData = new Dictionary<string, FinancialPeriodData>();
                foreach (var item in parsedResponse["value"])
                {
                    string accountId = item["Account"]?.ToString();
                    string description = item["Description"]?.ToString() ?? "No Description";
                    decimal beginningBalance = item["BeginningBalance"]?.ToObject<decimal>() ?? 0;
                    decimal endingBalance = item["EndingBalance"]?.ToObject<decimal>() ?? 0;
                    decimal debit = item["Debit"]?.ToObject<decimal>() ?? 0;
                    decimal credit = item["Credit"]?.ToObject<decimal>() ?? 0;

                    //PXTrace.WriteInformation($"Account {accountId}: Begin={beginningBalance}, End={endingBalance}, Debit={debit}, Credit={credit}, Desc={description}");

                    accountData[accountId] = new FinancialPeriodData
                    {
                        BeginningBalance = beginningBalance,
                        EndingBalance = endingBalance,
                        Debit = debit,
                        Credit = credit,
                        Description = description
                    };
                }

                var apiData = new FinancialApiData();
                foreach (var kvp in accountData)
                {
                    apiData.AccountData[kvp.Key] = kvp.Value;
                }

                return apiData;
            }
        }

        // --------------------------------------------------------
        // 2) FetchJanuaryBeginningBalance
        // --------------------------------------------------------
        public FinancialApiData FetchJanuaryBeginningBalance(string branch, string organization, string ledger, string prevYear)
        {
            string januaryPeriod = "01" + prevYear; // e.g. "012023"
            string accessToken = _authService.AuthenticateAndGetToken();

            // Build dimension filter for either branch, org, or both
            string dimensionFilter = BuildDimensionFilter(branch, organization);

            string filter = $"FinancialPeriod eq '{januaryPeriod}' and {dimensionFilter} and LedgerID eq '{ledger}'";
            string selectColumns = "Account,BeginningBalance,Description";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                string odataUrl =
                    $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance" +
                    $"?$filter={filter}&$select={selectColumns}";

                //PXTrace.WriteInformation($"Fetching January beginning balances: {odataUrl}");

                HttpResponseMessage response = client.GetAsync(odataUrl).Result;
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = response.Content.ReadAsStringAsync().Result;
                    PXTrace.WriteError($"GET request failed. Status: {response.StatusCode}, Response: {errorResponse}");
                    throw new PXException(Messages.FailedToFetchOData);
                }

                string jsonResponse = response.Content.ReadAsStringAsync().Result;
                PXTrace.WriteInformation($"OData Raw Response: {jsonResponse}");
                JObject parsedResponse = JObject.Parse(jsonResponse);

                var apiData = new FinancialApiData();
                foreach (var item in parsedResponse["value"])
                {
                    string accountId = item["Account"]?.ToString();
                    decimal beginningBalance = item["BeginningBalance"]?.ToObject<decimal>() ?? 0;
                    string description = item["Description"]?.ToString() ?? "No Description";

                    // The "beginning balance" often equals the "ending" at the start
                    apiData.AccountData[accountId] = new FinancialPeriodData
                    {
                        BeginningBalance = beginningBalance,
                        EndingBalance = beginningBalance,
                        Description = description
                    };
                }

                return apiData;
            }
        }

        // --------------------------------------------------------
        // 3) FetchRangeApiData
        // --------------------------------------------------------
        public FinancialApiData FetchRangeApiData(string branch, string organization, string ledger, string fromPeriod, string toPeriod)
        {
            string accessToken = _authService.AuthenticateAndGetToken();

            // Build dimension filter for either branch, org, or both
            string dimensionFilter = BuildDimensionFilter(branch, organization);

            string filter = $"FinancialPeriod ge '{fromPeriod}' and FinancialPeriod le '{toPeriod}' " +
                            $"and {dimensionFilter} and LedgerID eq '{ledger}'";

            string selectColumns = "Account,Debit,Credit,Description,FinancialPeriod,EndingBalance,BranchID,OrganizationID";

            string odataUrl =
                $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance" +
                $"?$filter={filter}&$select={selectColumns}";

            //PXTrace.WriteInformation($"Fetching cumulative range data from OData: {odataUrl}");

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
                PXTrace.WriteInformation($"OData Raw Response: {jsonResponse}");
                JObject parsedResponse = JObject.Parse(jsonResponse);

                var cumulativeDict = new Dictionary<string, FinancialPeriodData>();

                // Loop over each record returned for each month in the specified range
                foreach (var item in parsedResponse["value"])
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

                    // Sum up Debits & Credits across all rows in the range
                    cumulativeDict[accountId].Debit += debit;
                    cumulativeDict[accountId].Credit += credit;

                    // Store the ending balance from the last month in the range
                    cumulativeDict[accountId].EndingBalance = endingBalance;
                }

                var apiData = new FinancialApiData();
                foreach (var kvp in cumulativeDict)
                {
                    apiData.AccountData[kvp.Key] = kvp.Value;
                }

                return apiData;
            }
        }

        // --------------------------------------------------------
        // 4) FetchCompositeKeyData
        // --------------------------------------------------------
        public FinancialApiData FetchCompositeKeyData(string branch, string organization, string ledger, string period)
        {
            string accessToken = _authService.AuthenticateAndGetToken();

            //string dimensionFilter = $"1 eq 1";
            string filter = $"FinancialPeriod eq '{period}' and 1 eq 1 and LedgerID eq '{ledger}'";
            string selectColumns = "Account,Subaccount,BeginningBalance,EndingBalance,Debit,Credit,Description,BranchID,OrganizationID";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                string odataUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance?$filter={filter}&$select={selectColumns}";

                HttpResponseMessage response = client.GetAsync(odataUrl).Result;

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Failed to fetch financial data from OData endpoint.");
                }

                string jsonResponse = response.Content.ReadAsStringAsync().Result;
                JObject parsedResponse = JObject.Parse(jsonResponse);

                var compositeData = new Dictionary<string, FinancialPeriodData>();

                foreach (var item in parsedResponse["value"])
                {
                    string accountId = item["Account"]?.ToString()?.Trim();
                    string subaccountId = item["Subaccount"]?.ToString()?.Trim() ?? "N/A";
                    string branchId = item["BranchID"]?.ToString()?.Trim() ?? branch;
                    string orgId = item["OrganizationID"]?.ToString()?.Trim() ?? organization;
                    string compositeKey = $"{accountId}-{subaccountId}-{branchId}-{orgId}-{period}-{ledger}";

                    if (branchId == "MIP" && accountId == "A73102" || accountId == "A73101")
                    {
                        PXTrace.WriteInformation($"[Store] Composite key added: {compositeKey}");
                    }

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


                var apiData = new FinancialApiData();
                foreach (var kvp in compositeData)
                {
                    apiData.CompositeKeyData[kvp.Key] = kvp.Value;
                }

                return apiData;

            }
        }


        public decimal FetchEndingBalance(string period, string branch, string organization, string ledger, string account, string subaccount)
        {
            // Acquire token
            string accessToken = _authService.AuthenticateAndGetToken();

            // Construct a filter that exactly matches your desired row
            // e.g. ?$filter=FinancialPeriod eq '122022' and BranchID eq 'MIP' and OrganizationID eq 'M' 
            //      and LedgerID eq 'ACTUAL' and Account eq 'A73102' and Subaccount eq 'XXXXXXX'
            string filter =
                $"FinancialPeriod eq '{period}'" +
                $" and BranchID eq '{branch}'" +
                $" and OrganizationID eq '{organization}'" +
                $" and LedgerID eq '{ledger}'" +
                $" and Account eq '{account}'" +
                $" and Subaccount eq '{subaccount}'";

            // We only need columns that will let us read EndingBalance (but you can add more)
            string selectColumns = "Account,Subaccount,BeginningBalance,EndingBalance,Debit,Credit,Description,BranchID,OrganizationID";

            // Build OData URL
            string odataUrl =
                $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance" +
                $"?$filter={filter}&$select={selectColumns}";

            using (HttpClient client = new HttpClient())
            {
                // Set the bearer token
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                // Make synchronous GET call
                HttpResponseMessage response = client.GetAsync(odataUrl).Result;
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = response.Content.ReadAsStringAsync().Result;
                    PXTrace.WriteError($"GET request failed. Status: {response.StatusCode}, Response: {errorResponse}");
                    throw new PXException(Messages.FailedToFetchOData);
                }

                // Parse JSON
                string jsonResponse = response.Content.ReadAsStringAsync().Result;
                JObject parsedResponse = JObject.Parse(jsonResponse);

                // We only expect at most 1 matching row if the filters match exactly 1 record, 
                // but if more than 1 match is possible, we can just read the first "value"
                JToken firstRow = parsedResponse["value"]?.First;
                if (firstRow == null)
                {
                    PXTrace.WriteWarning($"No rows found for: {odataUrl}");
                    return 0m;
                }

                // Extract the EndingBalance field
                decimal endingBalance = firstRow["EndingBalance"]?.ToObject<decimal>() ?? 0;
                return endingBalance;
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
