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
        private readonly AuthService _authService;
        private readonly Func<List<string>> _getAccountNumbers;  // Function delegate for fetching accounts
        private readonly Dictionary<string, (FinancialApiData Data, DateTime Expiry)> _cache = new Dictionary<string, (FinancialApiData, DateTime)>();

        public FinancialDataService(string baseUrl, AuthService authService, Func<List<string>> getAccountNumbers)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _getAccountNumbers = getAccountNumbers ?? throw new ArgumentNullException(nameof(getAccountNumbers));
        }

        public Dictionary<string, (decimal EndingBalance, string Description)> FetchEndingBalances(
            string accessToken, string branch, string ledger, string period, List<string> accounts)
        {

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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
        }

        public FinancialApiData FetchAllApiData(string branch, string ledger, string period)
        {

            string cacheKey = $"{branch}_{ledger}_{period}";
            DateTime now = DateTime.UtcNow;

            // ✅ Check if data exists in cache and has not expired
            if (_cache.ContainsKey(cacheKey) && _cache[cacheKey].Expiry > now)
            {
                PXTrace.WriteInformation($"Using cached data for: {cacheKey}");
                return _cache[cacheKey].Data;
            }

            string accessToken = _authService.AuthenticateAndGetToken();
            var accounts = _getAccountNumbers(); // Fetch account numbers using the delegate
            var accountData = FetchEndingBalances(accessToken, branch, ledger, period, accounts);

            var apiData = new FinancialApiData();
            foreach (var kvp in accountData)
            {
                apiData.AccountData[kvp.Key] = (kvp.Value.EndingBalance.ToString(), kvp.Value.Description);
            }

            return apiData;
        }
    }

    public class FinancialApiData
    {
        public Dictionary<string, (string EndingBalance, string Description)> AccountData { get; set; } = new Dictionary<string, (string EndingBalance, string Description)>();
    }
}
