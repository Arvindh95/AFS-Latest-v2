using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using PX.Data;
using static PX.Objects.CA.CABankFeed;

namespace FinancialReport.Services
{
    public class AuthService
    {
        private string _accessToken = null;
        private string _refreshToken = null;
        private DateTime _tokenExpiry = DateTime.MinValue;
        private string _baseUrl;
        private string _clientId;
        private string _clientSecret;
        private string _username;
        private string _password;


        public AuthService(string baseUrl, string clientId, string clientSecret, string username, string password)
        {
            _baseUrl = baseUrl;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _username = username;
            _password = password;
        }

        public void SetToken(string token)
        {
            _accessToken = token;
            _tokenExpiry = DateTime.Now.AddMinutes(30); // Assume token is valid for 30 minutes
            PXTrace.WriteInformation("AuthService: Token has been set manually.");
        }


        #region AuthenticationAndGetToken();

        public string AuthenticateAndGetToken()
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

        #endregion

        #region RefreshAccessToken();
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

        #endregion

        #region Logout();
        public void Logout()
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
    }

    #endregion

}
