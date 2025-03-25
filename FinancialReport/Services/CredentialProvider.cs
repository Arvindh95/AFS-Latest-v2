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
                    ClientId = Encoding.UTF8.GetString(record.ClientID),
                    ClientSecret = Encoding.UTF8.GetString(record.SecretID),
                    Username = Encoding.UTF8.GetString(record.Username),
                    Password = Encoding.UTF8.GetString(record.Password)
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