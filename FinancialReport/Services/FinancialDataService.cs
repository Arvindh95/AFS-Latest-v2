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
            var accountData = FetchEndingBalances(accessToken, branch, ledger, period);

            var apiData = new FinancialApiData();
            foreach (var kvp in accountData)
            {
                apiData.AccountData[kvp.Key] = (kvp.Value.EndingBalance.ToString(), kvp.Value.Description);
            }

            return apiData;
        }

        private Dictionary<string, (decimal EndingBalance, string Description)> FetchEndingBalances(string accessToken, string branch, string ledger, string period)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);               
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

        public FinancialApiData FetchJanuaryBeginningBalance(string branch, string ledger, string prevYear)
        {
            string januaryPeriod = $"01{prevYear}"; // e.g., "012023"
            string accessToken = _authService.AuthenticateAndGetToken();
            var accountData = FetchBeginningBalances(accessToken, branch, ledger, januaryPeriod);

            var apiData = new FinancialApiData();
            foreach (var kvp in accountData)
            {
                apiData.AccountData[kvp.Key] = (kvp.Value.BeginningBalance.ToString(), kvp.Value.Description);
            }
            return apiData;
        }

        private Dictionary<string, (decimal BeginningBalance, string Description)> FetchBeginningBalances(string accessToken, string branch, string ledger, string period)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                string odataUrl = $"{_baseUrl}/t/{_tenantName}/api/odata/gi/TrialBalance?$filter=FinancialPeriod eq '{period}' and OrganizationID eq '{branch}' and LedgerID eq '{ledger}'&$select=Account,BeginningBalance,Description";

                PXTrace.WriteInformation($"Fetching beginning balance from OData: {odataUrl}");

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

                var accountData = new Dictionary<string, (decimal BeginningBalance, string Description)>();
                foreach (var item in parsedResponse["value"])
                {
                    string accountId = item["Account"]?.ToString();
                    string description = item["Description"]?.ToString() ?? "No Description";
                    decimal beginningBalance = item["BeginningBalance"]?.ToObject<decimal>() ?? 0;

                    PXTrace.WriteInformation($"Account {accountId}: BeginningBalance = {beginningBalance}, Description = {description}");
                    accountData[accountId] = (beginningBalance, description);
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
