using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PX.Data;

namespace FinancialReport.Services
{
    public class AuthService
    {
        private string _accessToken;
        private string _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        private readonly string _baseUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _username;
        private readonly string _password;
        private readonly object _lock = new object();

        // Increase this if you need more than the default 100s
        private static readonly TimeSpan HttpTimeout = TimeSpan.FromMinutes(3);

        public AuthService(string baseUrl, string clientId, string clientSecret, string username, string password)
        {
            // must be "http://112.137.169.188/UpmTest"
            _baseUrl = baseUrl.TrimEnd('/');
            _clientId = clientId;
            _clientSecret = clientSecret;
            _username = username;
            _password = password;
        }

        public string AuthenticateAndGetToken()
        {
            // reuse still-valid token
            if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.Now)
            {
                PXTrace.WriteInformation("Reusing existing access token.");
                return _accessToken;
            }

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.Now)
                {
                    PXTrace.WriteInformation("Reusing existing access token.");
                    return _accessToken;
                }
            }

            // try refresh
            if (!string.IsNullOrEmpty(_refreshToken))
            {
                PXTrace.WriteInformation("Attempting refresh...");
                try
                {
                    return RefreshAccessToken(_refreshToken);
                }
                catch (PXException ex)
                {
                    PXTrace.WriteError($"Refresh failed: {ex.Message}. Falling back to password grant.");
                }
            }

            // password grant
            PXTrace.WriteInformation("Requesting a new access token...");
            string tokenUrl = $"{_baseUrl}/identity/connect/token";

            // disable proxy if that’s blocking you
            var handler = new HttpClientHandler { UseProxy = false };
            using (var client = new HttpClient(handler) { Timeout = HttpTimeout })
            {
                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string,string>("grant_type",    "password"),
                    new KeyValuePair<string,string>("client_id",     _clientId),
                    new KeyValuePair<string,string>("client_secret", _clientSecret),
                    new KeyValuePair<string,string>("username",      _username),
                    new KeyValuePair<string,string>("password",      _password),
                    new KeyValuePair<string,string>("scope",         "api")
                });

                HttpResponseMessage resp = client.PostAsync(tokenUrl, form).Result;
                string body = resp.Content.ReadAsStringAsync().Result;

                if (!resp.IsSuccessStatusCode)
                {
                    PXTrace.WriteError($"Auth failed ({resp.StatusCode}): {body}");
                    throw new PXException(Messages.FailedToAuthenticate);
                }

                var json = JObject.Parse(body);
                _accessToken = json["access_token"]?.ToString()
                                ?? throw new PXException(Messages.AccessTokenNotFound);
                _refreshToken = json["refresh_token"]?.ToString() ?? string.Empty;

                int expiresIn = json["expires_in"]?.ToObject<int>() ?? 0;
                if (expiresIn == 0)
                    throw new PXException(Messages.TokenExpirationNotFound);

                // buffer 60 seconds
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

                PXTrace.WriteInformation("✅ New access token retrieved.");
                return _accessToken;
            }
        }

        private string RefreshAccessToken(string refreshToken)
        {
            string url = $"{_baseUrl}/identity/connect/token";
            using (var client = new HttpClient { Timeout = HttpTimeout })
            {
                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string,string>("grant_type",    "refresh_token"),
                    new KeyValuePair<string,string>("client_id",     _clientId),
                    new KeyValuePair<string,string>("client_secret", _clientSecret),
                    new KeyValuePair<string,string>("refresh_token", refreshToken)
                });

                HttpResponseMessage resp = client.PostAsync(url, form).Result;
                string body = resp.Content.ReadAsStringAsync().Result;

                if (!resp.IsSuccessStatusCode)
                {
                    PXTrace.WriteError($"Refresh failed ({resp.StatusCode}): {body}");
                    throw new PXException(Messages.FailedToRefreshToken);
                }

                var json = JObject.Parse(body);
                string newToken = json["access_token"]?.ToString()
                                  ?? throw new PXException(Messages.AccessTokenNotFound);

                int expiresIn = json["expires_in"]?.ToObject<int>() ?? 0;
                if (expiresIn == 0)
                    throw new PXException(Messages.TokenExpirationNotFound);

                lock (_lock)
                {
                    _accessToken = newToken;
                    _refreshToken = json["refresh_token"]?.ToString() ?? string.Empty;
                    _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);
                }

                PXTrace.WriteInformation("✅ Access token refreshed.");
                return newToken;
            }
        }

        public void Logout()
        {
            try
            {
                string url = $"{_baseUrl}/entity/auth/logout";
                using (var client = new HttpClient { Timeout = HttpTimeout })
                {
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _accessToken);

                    HttpResponseMessage resp = client.PostAsync(url, null).Result;
                    string body = resp.Content.ReadAsStringAsync().Result;
                    if (resp.IsSuccessStatusCode)
                        PXTrace.WriteInformation("Logged out successfully.");
                    else
                        PXTrace.WriteError($"Logout failed ({resp.StatusCode}): {body}");
                }
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Logout exception: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    _accessToken = _refreshToken = null;
                    _tokenExpiry = DateTime.MinValue;
                }
            }
        }
    }
}
