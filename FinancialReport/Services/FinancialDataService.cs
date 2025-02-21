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
        private readonly Dictionary<string, (FinancialApiData Data, DateTime Expiry)> _cache = new Dictionary<string, (FinancialApiData, DateTime)>();

        public FinancialDataService(string baseUrl, AuthService authService, string tenantName)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _tenantName = tenantName ?? throw new ArgumentNullException(nameof(tenantName));
        }

        public FinancialApiData FetchAllApiData(string branch, string ledger, string period)
        {
            string cacheKey = $"{branch}_{ledger}_{period}";
            DateTime now = DateTime.UtcNow;

            if (_cache.ContainsKey(cacheKey) && _cache[cacheKey].Expiry > now)
            {
                PXTrace.WriteInformation($"Using cached data for: {cacheKey}");
                return _cache[cacheKey].Data;
            }

            string accessToken = _authService.AuthenticateAndGetToken();
            var accountData = FetchEndingBalances(accessToken, branch, ledger, period);

            var apiData = new FinancialApiData();
            foreach (var kvp in accountData)
            {
                apiData.AccountData[kvp.Key] = (kvp.Value.EndingBalance.ToString(), kvp.Value.Description);
            }

            _cache[cacheKey] = (apiData, DateTime.UtcNow.AddMinutes(10));
            return apiData;
        }

        private Dictionary<string, (decimal EndingBalance, string Description)> FetchEndingBalances(
            string accessToken, string branch, string ledger, string period)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                //string odataUrl = $"{_baseUrl}/api/odata/gi/TrialBalance?$filter=FinancialPeriod eq '{period}' and OrganizationID eq '{branch}' and LedgerID eq '{ledger}'&$select=Account,EndingBalance,Description";
                //string odataUrl = $"{_baseUrl}/t/{tenant}/api/odata/gi/TrialBalance?$filter=FinancialPeriod eq '{period}' and OrganizationID eq '{branch}' and LedgerID eq '{ledger}'&$select=Account,EndingBalance,Description";
                string odataUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance?$filter=FinancialPeriod eq '{period}' and OrganizationID eq '{branch}' and LedgerID eq '{ledger}'&$select=Account,EndingBalance,Description";

                PXTrace.WriteInformation($"Fetching data from OData: {odataUrl}");

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

                var accountData = new Dictionary<string, (decimal EndingBalance, string Description)>();
                foreach (var item in parsedResponse["value"])
                {
                    string accountId = item["Account"]?.ToString();
                    string description = item["Description"]?.ToString() ?? "No Description";
                    decimal endingBalance = item["EndingBalance"]?.ToObject<decimal>() ?? 0;

                    PXTrace.WriteInformation($"Account {accountId}: EndingBalance = {endingBalance}, Description = {description}");
                    accountData[accountId] = (endingBalance, description);
                }

                return accountData;
            }
        }
    }

    public class FinancialApiData
    {
        public Dictionary<string, (string EndingBalance, string Description)> AccountData { get; set; } = new Dictionary<string, (string EndingBalance, string Description)>();
    }
}
