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
        public const string MissingConfig = "Missing Config. Check Web Configurations";
    }
}
