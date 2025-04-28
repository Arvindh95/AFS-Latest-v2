using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PX.Common;

namespace FinancialReport
{
    [PXLocalizable]
    public static class Messages
    {
        public const string FailedToAuthenticate = "Failed to authenticate";
        public const string AccessTokenNotFound = "Access token not found in response.";
        public const string TokenExpirationNotFound = "Token expiration not found in response.";
        public const string FailedToFetchOData = "Failed to fetch OData";
        public const string PTDBalanceNotFound = "PTDBalance not found in OData response.";
        public const string PutRequestFailed = "PUT request failed: {0}";
        public const string EndingBalanceNotFound = "Ending Balance not found in API response.";
        public const string PleaseSelectTemplate = "Please select a template to generate the report.";
        public const string TemplateHasNoFiles = "The selected template does not have any attached files.";
        public const string TemplateFileIsEmpty = "The selected template file is empty or could not be retrieved.";
        public const string CurrentYearNotSpecified = "Current Year is not specified for the selected report.";
        public const string NoteIDIsNull = "NoteID is null.";
        public const string NoFilesAssociated = "No files are associated with this record.";
        public const string FailedToRetrieveFile = "Failed to retrieve the file content.";
        public const string UnableToSaveGeneratedFile = "Unable to save the generated file.";
        public const string FailedToRefreshToken = "Failed to refresh token";
        public const string FailedToSelectBranch = "Please select a Branch";
        public const string FailedToSelectLedger = "Please select a Ledger";
        public const string FailedToSelectBranchorOrg = "Please select a Branch or Organization";
        public const string MissingConfig = "Missing Config. Check Web Configurations";
        public const string PleaseSelectABranch = "Please select a Branch";
        public const string PleaseSelectALedger = "Please select a Ledger";
        public const string NoRecordIsSelected = "No record is selected";
        public const string NoGeneratedFile = "No generated file is available for download. Please generate the report first.";
        public const string FileGenerationInProgress = "A report generation process is already running for this template.";
        public const string NoAPIFound = "No API credentials found for company";
        public const string UnabletoDetermineCompany = "Unable to determine the current company.";
        public const string InvalidCredentials = "Failed to authenticate. Please check credentials.";
        public const string TenantMissingFromConfig = "Tenant mapping is missing in Web.config.";
        public const string TenantMissingFromDatabase = "Tenant mapping is missing in Database";
        public const string NoCalculation = "No placeholder logic configured for this tenant.";
        public const string NoCompanyID = "CompanyID is required for tenant mapping.";
        public const string NoTenantMapping = "Tenant mapping not found for the specified company number.";
        public const string NoValueMapping = "No Month or Year Specified";

    }

}
