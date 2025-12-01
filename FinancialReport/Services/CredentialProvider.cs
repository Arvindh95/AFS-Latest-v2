using PX.Data;
using System.Collections.Generic;
using System.Text;

namespace FinancialReport.Services
{
    public class AcumaticaCredentials
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string BaseURL { get; set; } // Add this line
    }

    public static class CredentialProvider
    {
        // In-memory cache for credentials (thread-safe dictionary)
        private static readonly Dictionary<string, AcumaticaCredentials> _credentialCache = new Dictionary<string, AcumaticaCredentials>();
        private static readonly object _cacheLock = new object();

        public static AcumaticaCredentials GetCredentials(string tenant)
        {
            // Check cache first
            lock (_cacheLock)
            {
                if (_credentialCache.ContainsKey(tenant))
                {
                    PXTrace.WriteInformation($"✅ Credentials retrieved from cache for tenant: {tenant}");
                    return _credentialCache[tenant];
                }
            }

            // Not in cache - fetch from database and decrypt
            PXTrace.WriteInformation($"🔓 Decrypting credentials for tenant: {tenant}");
            PXGraph graph = PXGraph.CreateInstance<PXGraph>();
            try
            {
                // Query the FLRTTenantCredentials table by TenantName
                FLRTTenantCredentials record = PXSelect<FLRTTenantCredentials,
                    Where<FLRTTenantCredentials.tenantName, Equal<Required<FLRTTenantCredentials.tenantName>>>>
                    .Select(graph, tenant);

                if (record == null)
                {
                    PXTrace.WriteError($"No credentials found for tenant: {tenant}");
                    throw new PXException(Messages.TenantMissingFromDatabase);
                }

                // Convert byte[] fields to strings and return
                AcumaticaCredentials credentials = new AcumaticaCredentials
                {
                    //ClientId = !string.IsNullOrWhiteSpace(record.ClientIDNew)
                    //    ? record.ClientIDNew
                    //    : Encoding.UTF8.GetString(record.ClientID),

                    //ClientSecret = !string.IsNullOrWhiteSpace(record.ClientSecretNew)
                    //    ? record.ClientSecretNew
                    //    : Encoding.UTF8.GetString(record.SecretID),

                    //Username = !string.IsNullOrWhiteSpace(record.UsernameNew)
                    //    ? record.UsernameNew
                    //    : Encoding.UTF8.GetString(record.Username),

                    //Password = !string.IsNullOrWhiteSpace(record.PasswordNew)
                    //    ? record.PasswordNew
                    //    : Encoding.UTF8.GetString(record.Password),

                    //BaseURL = record.BaseURL

                    ClientId = record.ClientIDNew,
                    ClientSecret = record.ClientSecretNew,
                    Username = record.UsernameNew,
                    Password = record.PasswordNew,
                    BaseURL = record.BaseURL
                };

                PXTrace.WriteInformation($"Credentials decrypted and cached for tenant {tenant} (CompanyNum {record.CompanyNum}): ClientId={credentials.ClientId}, Username={credentials.Username}");

                // Add to cache
                lock (_cacheLock)
                {
                    if (!_credentialCache.ContainsKey(tenant))
                    {
                        _credentialCache[tenant] = credentials;
                    }
                }

                return credentials;
            }
            finally
            {
                graph.Clear();
            }
        }

        /// <summary>
        /// Clears the credential cache. Should be called after report generation completes.
        /// </summary>
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                int count = _credentialCache.Count;
                _credentialCache.Clear();
                if (count > 0)
                {
                    PXTrace.WriteInformation($"🧹 Cleared credential cache ({count} tenant(s))");
                }
            }
        }

        /// <summary>
        /// Clears credentials for a specific tenant from the cache.
        /// </summary>
        public static void ClearCache(string tenant)
        {
            lock (_cacheLock)
            {
                if (_credentialCache.Remove(tenant))
                {
                    PXTrace.WriteInformation($"🧹 Cleared cached credentials for tenant: {tenant}");
                }
            }
        }
    }
}