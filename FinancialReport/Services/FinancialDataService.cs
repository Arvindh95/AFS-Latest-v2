using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
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

        public FinancialApiData FetchAllApiData(string branch, string ledger, string period)
        {
            string accessToken = _authService.AuthenticateAndGetToken();
            var accountData = FetchPeriodData(accessToken, branch, ledger, period);

            var apiData = new FinancialApiData();
            foreach (var kvp in accountData)
            {
                apiData.AccountData[kvp.Key] = new FinancialPeriodData
                {
                    BeginningBalance = kvp.Value.BeginningBalance,
                    EndingBalance = kvp.Value.EndingBalance,
                    Debit = kvp.Value.Debit,
                    Credit = kvp.Value.Credit,
                    Description = kvp.Value.Description
                };
            }

            return apiData;
        }

        public FinancialApiData FetchJanuaryBeginningBalance(string branch, string ledger, string prevYear)
        {
            string januaryPeriod = $"01{prevYear}"; // e.g., "012023"
            string accessToken = _authService.AuthenticateAndGetToken();
            var accountData = FetchPeriodData(accessToken, branch, ledger, januaryPeriod, "Account,BeginningBalance,Description");

            var apiData = new FinancialApiData();
            foreach (var kvp in accountData)
            {
                apiData.AccountData[kvp.Key] = new FinancialPeriodData
                {
                    BeginningBalance = kvp.Value.BeginningBalance,
                    EndingBalance = kvp.Value.BeginningBalance,
                    Description = kvp.Value.Description
                };
            }

            return apiData;
        }

        /// ✅ **NEW FUNCTION: Fetch Cumulative Debit & Credit for a Given Range**
        public FinancialApiData FetchRangeApiData(string branch, string ledger, string fromPeriod, string toPeriod)
        {
            string accessToken = _authService.AuthenticateAndGetToken();

            string filter = $"FinancialPeriod ge '{fromPeriod}' and FinancialPeriod le '{toPeriod}' and OrganizationID eq '{branch}' and LedgerID eq '{ledger}'";
            string selectColumns = "Account,Debit,Credit,Description,FinancialPeriod,EndingBalance";

            string odataUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance?$filter={filter}&$select={selectColumns}";

            PXTrace.WriteInformation($"Fetching cumulative range data from OData: {odataUrl}");

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

                // ✅ Loop over each record returned for each month within the range
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

                    // ✅ Sum up Debits & Credits across all rows
                    cumulativeDict[accountId].Debit += debit;
                    cumulativeDict[accountId].Credit += credit;

                    // ✅ Store the latest Ending Balance (last month in the range)
                    cumulativeDict[accountId].EndingBalance = endingBalance;
                }

                // Convert dictionary to FinancialApiData
                var apiData = new FinancialApiData();
                foreach (var kvp in cumulativeDict)
                {
                    string acctId = kvp.Key;
                    var data = kvp.Value;

                    apiData.AccountData[acctId] = data;
                }

                return apiData;
            }
        }

        private Dictionary<string, FinancialPeriodData> FetchPeriodData(string accessToken, string branch, string ledger, string period, string selectColumns = "Account,BeginningBalance,EndingBalance,Debit,Credit,Description")
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                string odataUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance?$filter=FinancialPeriod eq '{period}' and OrganizationID eq '{branch}' and LedgerID eq '{ledger}'&$select={selectColumns}";

                PXTrace.WriteInformation($"Fetching period data from OData: {odataUrl}");

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

                    PXTrace.WriteInformation($"Account {accountId}: BeginningBalance = {beginningBalance}, EndingBalance = {endingBalance}, Debit = {debit}, Credit = {credit}, Description = {description}");
                    accountData[accountId] = new FinancialPeriodData
                    {
                        BeginningBalance = beginningBalance,
                        EndingBalance = endingBalance,
                        Debit = debit,
                        Credit = credit,
                        Description = description
                    };
                }

                return accountData;
            }
        }
    }

    public class FinancialPeriodData
    {
        public decimal BeginningBalance { get; set; }
        public decimal EndingBalance { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string Description { get; set; }
    }

    public class FinancialApiData
    {
        public Dictionary<string, FinancialPeriodData> AccountData { get; set; } = new Dictionary<string, FinancialPeriodData>();
    }
}
