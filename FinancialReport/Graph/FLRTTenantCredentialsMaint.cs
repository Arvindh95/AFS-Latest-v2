using System;
using PX.Data;
using System.Text;
using PX.Data.BQL.Fluent;

namespace FinancialReport
{
    public class FLRTTenantCredentialsMaint : PXGraph<FLRTTenantCredentialsMaint>
    {
        public PXSave<FLRTTenantCredentials> Save;
        public PXCancel<FLRTTenantCredentials> Cancel;

        public SelectFrom<FLRTTenantCredentials>.View TenantCredentials;

        //public PXFilter<FLRTTenantCredentials> MasterView;
        //public PXFilter<FLRTTenantCredentials> DetailsView;

        public FLRTTenantCredentialsMaint()
        {
            PXTrace.WriteInformation("Graph initialized.");
        }

        #region Event Handlers
        // FieldUpdating to convert string input from UI to byte[]
        protected void FLRTTenantCredentials_ClientID_FieldUpdating(PXCache cache, PXFieldUpdatingEventArgs e)
        {
            if (e.NewValue is string stringValue && !string.IsNullOrEmpty(stringValue))
            {
                PXTrace.WriteInformation($"Converting ClientID: {stringValue}");
                e.NewValue = Encoding.UTF8.GetBytes(stringValue);
            }
        }

        protected void FLRTTenantCredentials_SecretID_FieldUpdating(PXCache cache, PXFieldUpdatingEventArgs e)
        {
            if (e.NewValue is string stringValue && !string.IsNullOrEmpty(stringValue))
            {
                PXTrace.WriteInformation($"Converting SecretID: {stringValue}");
                e.NewValue = Encoding.UTF8.GetBytes(stringValue);
            }
        }

        protected void FLRTTenantCredentials_Username_FieldUpdating(PXCache cache, PXFieldUpdatingEventArgs e)
        {
            if (e.NewValue is string stringValue && !string.IsNullOrEmpty(stringValue))
            {
                PXTrace.WriteInformation($"Converting Username: {stringValue}");
                e.NewValue = Encoding.UTF8.GetBytes(stringValue);
            }
        }

        protected void FLRTTenantCredentials_Password_FieldUpdating(PXCache cache, PXFieldUpdatingEventArgs e)
        {
            if (e.NewValue is string stringValue && !string.IsNullOrEmpty(stringValue))
            {
                PXTrace.WriteInformation($"Converting Password: {stringValue}");
                e.NewValue = Encoding.UTF8.GetBytes(stringValue);
            }
        }

        // FieldSelecting to display byte[] as string in UI
        protected void FLRTTenantCredentials_ClientID_FieldSelecting(PXCache cache, PXFieldSelectingEventArgs e)
        {
            var row = (FLRTTenantCredentials)e.Row;
            if (row?.ClientID != null)
            {
                string value = Encoding.UTF8.GetString(row.ClientID);
                PXTrace.WriteInformation($"Displaying ClientID: {value}");
                e.ReturnValue = value;
            }
        }

        protected void FLRTTenantCredentials_SecretID_FieldSelecting(PXCache cache, PXFieldSelectingEventArgs e)
        {
            var row = (FLRTTenantCredentials)e.Row;
            if (row?.SecretID != null)
            {
                string value = Encoding.UTF8.GetString(row.SecretID);
                PXTrace.WriteInformation($"Displaying SecretID: {value}");
                e.ReturnValue = value;
            }
        }

        protected void FLRTTenantCredentials_Username_FieldSelecting(PXCache cache, PXFieldSelectingEventArgs e)
        {
            var row = (FLRTTenantCredentials)e.Row;
            if (row?.Username != null)
            {
                string value = Encoding.UTF8.GetString(row.Username);
                PXTrace.WriteInformation($"Displaying Username: {value}");
                e.ReturnValue = value;
            }
        }

        protected void FLRTTenantCredentials_Password_FieldSelecting(PXCache cache, PXFieldSelectingEventArgs e)
        {
            var row = (FLRTTenantCredentials)e.Row;
            if (row?.Password != null)
            {
                string value = Encoding.UTF8.GetString(row.Password);
                PXTrace.WriteInformation($"Displaying Password: {value}");
                e.ReturnValue = value;
            }
        }

        // RowPersisting for validation and logging
        protected void FLRTTenantCredentials_RowPersisting(PXCache cache, PXRowPersistingEventArgs e)
        {
            var row = (FLRTTenantCredentials)e.Row;
            if (row == null) return;

            PXTrace.WriteInformation($"Persisting row: CompanyNum={row.CompanyNum}, TenantName={row.TenantName}, ClientID={ByteArrayToString(row.ClientID)}, SecretID={ByteArrayToString(row.SecretID)}, Username={ByteArrayToString(row.Username)}, Password={ByteArrayToString(row.Password)}");

            // Validate required fields
            if (row.CompanyNum == null)
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.companyNum>(row, null, new PXSetPropertyException("Company Number is required.", PXErrorLevel.Error));
                throw new PXException("Company Number is required.");
            }
            if (string.IsNullOrEmpty(row.TenantName))
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.tenantName>(row, null, new PXSetPropertyException("Tenant Name is required.", PXErrorLevel.Error));
                throw new PXException("Tenant Name is required.");
            }
            if (row.ClientID == null || row.ClientID.Length == 0)
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.clientID>(row, null, new PXSetPropertyException("Client ID is required.", PXErrorLevel.Error));
                throw new PXException("Client ID is required.");
            }
            if (row.SecretID == null || row.SecretID.Length == 0)
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.secretID>(row, null, new PXSetPropertyException("Client Secret is required.", PXErrorLevel.Error));
                throw new PXException("Client Secret is required.");
            }
            if (row.Username == null || row.Username.Length == 0)
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.username>(row, null, new PXSetPropertyException("Username is required.", PXErrorLevel.Error));
                throw new PXException("Username is required.");
            }
            if (row.Password == null || row.Password.Length == 0)
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.password>(row, null, new PXSetPropertyException("Password is required.", PXErrorLevel.Error));
                throw new PXException("Password is required.");
            }

            // Check for duplicate TenantName (excluding the current row)
            FLRTTenantCredentials existing = PXSelect<FLRTTenantCredentials,
                Where<FLRTTenantCredentials.tenantName, Equal<Required<FLRTTenantCredentials.tenantName>>,
                    And<FLRTTenantCredentials.companyNum, NotEqual<Required<FLRTTenantCredentials.companyNum>>>>>
                .Select(this, row.TenantName, row.CompanyNum);

            if (existing != null)
            {
                cache.RaiseExceptionHandling<FLRTTenantCredentials.tenantName>(row, row.TenantName, new PXSetPropertyException("Tenant Name must be unique.", PXErrorLevel.Error));
                throw new PXException("Tenant Name must be unique.");
            }
        }

        // RowPersisted to confirm success or failure
        protected void FLRTTenantCredentials_RowPersisted(PXCache cache, PXRowPersistedEventArgs e)
        {
            var row = (FLRTTenantCredentials)e.Row;
            if (row == null) return;

            if (e.TranStatus == PXTranStatus.Completed)
            {
                PXTrace.WriteInformation($"Row persisted successfully: CompanyNum={row.CompanyNum}, TenantName={row.TenantName}, Operation={e.Operation}");
            }
            else if (e.TranStatus == PXTranStatus.Aborted)
            {
                PXTrace.WriteError($"Row persistence failed: CompanyNum={row.CompanyNum}, TenantName={row.TenantName}, Operation={e.Operation}");
                if (e.Exception != null)
                {
                    PXTrace.WriteError($"Exception: {e.Exception.Message}\nStack Trace: {e.Exception.StackTrace}");
                }
            }
        }
        #endregion

        #region Helper Methods
        public static byte[] StringToByteArray(string value) => Encoding.UTF8.GetBytes(value);
        public static string ByteArrayToString(byte[] value) => value != null ? Encoding.UTF8.GetString(value) : string.Empty;
        #endregion

        #region Actions
        // Override the existing Save action
        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Save")]
        protected virtual void save()
        {
            try
            {
                PXTrace.WriteInformation("Save action triggered.");

                // Log the state of the cache before saving
                foreach (FLRTTenantCredentials row in TenantCredentials.Cache.Cached)
                {
                    PXTrace.WriteInformation($"Cache state before save: CompanyNum={row.CompanyNum}, TenantName={row.TenantName}, ClientID={ByteArrayToString(row.ClientID)}, SecretID={ByteArrayToString(row.SecretID)}, Username={ByteArrayToString(row.Username)}, Password={ByteArrayToString(row.Password)}");
                }

                // Persist the cache
                TenantCredentials.Cache.Persist(PXDBOperation.Insert | PXDBOperation.Update);

                // Log success
                PXTrace.WriteInformation("Cache persisted successfully.");

                // Call the default save to commit the transaction
                Actions.PressSave();
                PXTrace.WriteInformation("Save action completed.");
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Save failed: {ex.Message}\nStack Trace: {ex.StackTrace}");
                throw new PXException($"Failed to save: {ex.Message}", ex);
            }
        }

        //public PXAction<FLRTTenantCredentials> forceInsertTest;
        //[PXButton]
        //[PXUIField(DisplayName = "Force Insert Test")]
        //protected void ForceInsertTest()
        //{
        //    PXDatabase.Insert<FLRTTenantCredentials>(
        //        new PXDataFieldAssign<FLRTTenantCredentials.companyNum>(7),
        //        new PXDataFieldAssign<FLRTTenantCredentials.tenantName>("Tenant2"),
        //        new PXDataFieldAssign<FLRTTenantCredentials.clientID>(Encoding.UTF8.GetBytes("Client7")),
        //        new PXDataFieldAssign<FLRTTenantCredentials.secretID>(Encoding.UTF8.GetBytes("Secret7")),
        //        new PXDataFieldAssign<FLRTTenantCredentials.username>(Encoding.UTF8.GetBytes("User7")),
        //        new PXDataFieldAssign<FLRTTenantCredentials.password>(Encoding.UTF8.GetBytes("Pass7"))
        //    );
        //}
        #endregion
    }
}