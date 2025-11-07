using PX.Common;

namespace FinancialReport
{
    [PXLocalizable]
    public static class Messages
    {
        // ==================================================
        // AUTHENTICATION & API MESSAGES
        // ==================================================
        public const string FailedToAuthenticate = "Failed to authenticate";
        public const string InvalidCredentials = "Failed to authenticate. Please check credentials.";
        public const string AccessTokenNotFound = "Access token not found in response.";
        public const string TokenExpirationNotFound = "Token expiration not found in response.";
        public const string FailedToRefreshToken = "Failed to refresh token";

        // ==================================================
        // API & DATA RETRIEVAL MESSAGES  
        // ==================================================
        public const string FailedToFetchOData = "Failed to fetch OData";
        public const string PutRequestFailed = "PUT request failed: {0}";
        public const string PTDBalanceNotFound = "PTDBalance not found in OData response.";
        public const string EndingBalanceNotFound = "Ending Balance not found in API response.";
        public const string NoAPIFound = "No API credentials found for company";
        public const string NoCompanyIDFound = "No CompanyID found for ReportID {0}.";

        // ==================================================
        // FILE & TEMPLATE MESSAGES
        // ==================================================
        public const string NoteIDIsNull = "NoteID is null.";
        public const string NoFilesAssociated = "No files are associated with this record.";
        public const string TemplateHasNoFiles = "The selected template does not have any attached files.";
        public const string TemplateFileIsEmpty = "The selected template file is empty or could not be retrieved.";
        public const string FailedToRetrieveFile = "Failed to retrieve the file content.";
        public const string UnableToSaveGeneratedFile = "Unable to save the generated file.";

        // ==================================================
        // REPORT GENERATION MESSAGES
        // ==================================================
        public const string PleaseSelectTemplate = "Please select a template to generate the report.";
        public const string NoGeneratedFile = "No generated file is available for download. Please generate the report first.";
        public const string FileGenerationInProgress = "A report generation process is already running for this template.";
        public const string CurrentYearNotSpecified = "Current Year is not specified for the selected report.";
        public const string NoRecordIsSelected = "No record is selected";
        public const string NoReportSelected = "No report selected or report ID is missing.";
        public const string ReportIDNull = "ReportID cannot be null when retrieving CompanyID.";

        // ==================================================
        // USER INPUT VALIDATION MESSAGES
        // ==================================================
        public const string FailedToSelectBranch = "Please select a Branch";
        public const string PleaseSelectABranch = "Please select a Branch";
        public const string FailedToSelectLedger = "Please select a Ledger";
        public const string PleaseSelectALedger = "Please select a Ledger";
        public const string FailedToSelectBranchorOrg = "Please select a Branch or Organization";
        public const string NoValueMapping = "No Month or Year Specified";

        // ==================================================
        // COMPANY & TENANT CONFIGURATION MESSAGES
        // ==================================================
        public const string CompanyNumRequired = "Company Number is required.";
        public const string TenantNameRequired = "Tenant Name is required.";
        public const string UnabletoDetermineCompany = "Unable to determine the current company.";
        public const string TenantMissingFromConfig = "Tenant mapping is missing in Web.config.";
        public const string TenantMissingFromDatabase = "Tenant mapping is missing in Database";
        public const string NoTenantMapping = "Tenant mapping not found.";
        public const string NoCalculation = "No placeholder logic configured for this tenant.";

        // ==================================================
        // CONFIGURATION & SYSTEM MESSAGES
        // ==================================================
        public const string MissingConfig = "Missing Config. Check Web Configurations";

        // ==================================================
        // NEW CONSTANTS (Add these)
        // ==================================================
        public const string TenantNameMustBeUnique = "Tenant Name must be unique.";
        public const string FailedToSaveMessage = "Failed to save: {0}";
        public const string TooManyPlaceholders = "Template contains {0} placeholders. Maximum allowed is {1}. Please simplify your template or split it into multiple reports.";
    }
}
