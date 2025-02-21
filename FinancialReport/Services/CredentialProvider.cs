using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using PX.Data;

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
            string prefix = string.IsNullOrEmpty(tenant) ? "Acumatica.Company" : $"Acumatica.{tenant}";

            AcumaticaCredentials credentials = new AcumaticaCredentials
            {
                ClientId = ConfigurationManager.AppSettings[$"{prefix}.ClientId"],
                ClientSecret = ConfigurationManager.AppSettings[$"{prefix}.ClientSecret"],
                Username = ConfigurationManager.AppSettings[$"{prefix}.Username"],
                Password = ConfigurationManager.AppSettings[$"{prefix}.Password"]
            };

            // Log missing config issues
            if (string.IsNullOrEmpty(credentials.ClientId) ||
                string.IsNullOrEmpty(credentials.ClientSecret) ||
                string.IsNullOrEmpty(credentials.Username) ||
                string.IsNullOrEmpty(credentials.Password))
            {
                PXTrace.WriteError($"Missing credentials for {tenant}. Check Web.config.");
                throw new PXException(Messages.TenantMissingFromConfig);
            }

            return credentials;
        }
    }


}
