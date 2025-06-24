using PX.Data;
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
        public static AcumaticaCredentials GetCredentials(string tenant)
        {
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
                    ClientId = !string.IsNullOrWhiteSpace(record.ClientIDNew)
                        ? record.ClientIDNew
                        : Encoding.UTF8.GetString(record.ClientID),

                    ClientSecret = !string.IsNullOrWhiteSpace(record.ClientSecretNew)
                        ? record.ClientSecretNew
                        : Encoding.UTF8.GetString(record.SecretID),

                    Username = !string.IsNullOrWhiteSpace(record.UsernameNew)
                        ? record.UsernameNew
                        : Encoding.UTF8.GetString(record.Username),

                    Password = !string.IsNullOrWhiteSpace(record.PasswordNew)
                        ? record.PasswordNew
                        : Encoding.UTF8.GetString(record.Password),

                    BaseURL = record.BaseURL
                };

                PXTrace.WriteInformation($"Credentials retrieved for tenant {tenant} (CompanyNum {record.CompanyNum}): ClientId={credentials.ClientId}, Username={credentials.Username}");

                return credentials;
            }
            finally
            {
                graph.Clear();
            }
        }
    }
}