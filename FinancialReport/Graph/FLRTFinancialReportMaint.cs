using System;
using PX.Data;
using PX.Data.BQL.Fluent;
using System.Collections.Generic;
using System.IO;
using PX.SM;
using System.Linq;
using FinancialReport.Helper;
using FinancialReport.Services;
using static FinancialReport.FLRTFinancialReport;

namespace FinancialReport
{
    public class FLRTFinancialReportMaint : PXGraph<FLRTFinancialReportMaint>
    {
        #region DAC View
        public SelectFrom<FLRTFinancialReport>.View FinancialReport;
        #endregion

        #region Events
        protected void FLRTFinancialReport_Selected_FieldUpdated(PXCache cache, PXFieldUpdatedEventArgs e)
        {
            var current = (FLRTFinancialReport)e.Row;
            if (current?.Selected != true) return;

            foreach (FLRTFinancialReport item in FinancialReport.Cache.Cached)
            {
                if (item.ReportID != current.ReportID && item.Selected == true)
                {
                    item.Selected = false;
                }
            }
        }

        protected void FLRTFinancialReport_RowSelected(PXCache cache, PXRowSelectedEventArgs e)
        {
            var row = (FLRTFinancialReport)e.Row;
            if (row == null) return;

            bool anyInProgress = FinancialReport.Cache.Cached
                .Cast<FLRTFinancialReport>()
                .Any(r => r.Status == ReportStatus.InProgress);

            PXUIFieldAttribute.SetEnabled(cache, row, !anyInProgress);
            GenerateReport.SetEnabled(!anyInProgress);
            DownloadReport.SetEnabled(!anyInProgress);

            if (row.Noteid != null)
            {
                using (new PXConnectionScope())
                {
                    var fileCmd = new PXSelectJoin<UploadFile,
                                        InnerJoin<NoteDoc, On<NoteDoc.fileID, Equal<UploadFile.fileID>>>,
                                    Where<NoteDoc.noteID, Equal<Required<NoteDoc.noteID>>>,
                                    OrderBy<Desc<UploadFile.createdDateTime>>>(this);

                    var file = fileCmd.SelectSingle(row.Noteid);

                    if (file?.Name?.Contains(Constants.TemplateFileFilter) == true)
                    {
                        string fileIdString = file.FileID.ToString();
                        if (row.UploadedFileIDDisplay != fileIdString)
                        {
                            cache.SetValue<FLRTFinancialReport.uploadedFileIDDisplay>(row, fileIdString);
                            cache.SetValueExt<FLRTFinancialReport.uploadedFileID>(row, file.FileID);
                            cache.MarkUpdated(row);
                            cache.IsDirty = true;
                        }
                    }
                }
            }
        }

        protected virtual void FLRTFinancialReport_RowInserted(PXCache sender, PXRowInsertedEventArgs e)
        {
            this.Actions.PressSave();
            FinancialReport.Current = null;
            FinancialReport.Cache.Clear();
            FinancialReport.Cache.ClearQueryCache();
            FinancialReport.View.Clear();
        }
        #endregion

        #region Actions
        public PXSave<FLRTFinancialReport> Save;
        public PXCancel<FLRTFinancialReport> Cancel;
        public PXAction<FLRTFinancialReport> GenerateReport;
        public PXAction<FLRTFinancialReport> DownloadReport;

        [PXButton(CommitChanges = false)]
        [PXUIField(DisplayName = "Generate Report")]
        protected virtual System.Collections.IEnumerable generateReport(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Cache.Cached
                .Cast<FLRTFinancialReport>()
                .FirstOrDefault(item => item.Selected == true);

            // Perform initial validations on the UI thread
            if (selectedRecord == null) throw new PXException(Messages.PleaseSelectTemplate);
            if (selectedRecord.ReportID == null) throw new PXException("No report selected or report ID is missing.");
            if (selectedRecord.Status == ReportStatus.InProgress) throw new PXException(Messages.FileGenerationInProgress);
            if (selectedRecord.Noteid == null) throw new PXException(Messages.TemplateHasNoFiles);

            int? companyID = GetCompanyIDFromDB(selectedRecord.ReportID);
            string tenantName = MapCompanyIDToTenantName(companyID);
            AcumaticaCredentials tenantCredentials = CredentialProvider.GetCredentials(tenantName);

            var authService = new AuthService(
                tenantCredentials.BaseURL,
                tenantCredentials.ClientId,
                tenantCredentials.ClientSecret,
                tenantCredentials.Username,
                tenantCredentials.Password
            );

            // Pre-authenticate to ensure credentials are valid before starting the long operation
            authService.AuthenticateAndGetToken();
            PXTrace.WriteInformation($"Successfully authenticated for {tenantName}.");

            // Mark record as In Progress and save before starting background task
            selectedRecord.Status = ReportStatus.InProgress;
            FinancialReport.Update(selectedRecord);
            Actions.PressSave();

            int? reportID = selectedRecord.ReportID;

            PXLongOperation.StartOperation(this, () =>
            {
                var reportGraph = PXGraph.CreateInstance<FLRTFinancialReportMaint>();
                FLRTFinancialReport dbRecord = PXSelect<FLRTFinancialReport, Where<FLRTFinancialReport.reportID, Equal<Required<FLRTFinancialReport.reportID>>>>
                    .SelectSingleBound(reportGraph, null, reportID);

                try
                {
                    if (dbRecord == null) throw new PXException(Messages.FailedToRetrieveFile);
                    if (dbRecord.Noteid == null) throw new PXException(Messages.TemplateHasNoFiles);

                    // Instantiate and execute the new service
                    var generationService = new ReportGenerationService(reportGraph, dbRecord, authService);
                    Guid generatedFileID = generationService.Execute();

                    // Update the record with the successful result
                    dbRecord.GeneratedFileID = generatedFileID;
                    dbRecord.Status = ReportStatus.Completed;
                    reportGraph.FinancialReport.Update(dbRecord);
                    reportGraph.Actions.PressSave();
                    PXTrace.WriteInformation("Report generation completed successfully.");
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError($"Report generation failed: {ex.Message}");
                    if (dbRecord != null)
                    {
                        dbRecord.Status = ReportStatus.Failed;
                        reportGraph.FinancialReport.Update(dbRecord);
                        reportGraph.Actions.PressSave();
                    }
                }
                finally
                {
                    authService?.Logout();
                }
            });

            return adapter.Get();
        }

        [PXButton]
        [PXUIField(DisplayName = "Download Report", MapEnableRights = PXCacheRights.Select, Visible = true)]
        protected virtual System.Collections.IEnumerable downloadReport(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Cache.Cached
                .Cast<FLRTFinancialReport>()
                .FirstOrDefault(x => x.Selected == true);

            if (selectedRecord == null)
                throw new PXException(Messages.NoRecordIsSelected);

            if (selectedRecord.GeneratedFileID == null)
                throw new PXException(Messages.NoGeneratedFile);

            throw new PXRedirectToFileException(selectedRecord.GeneratedFileID.Value, true);
        }
        #endregion

        #region Public Helper Methods
        // These methods are made public so they can be called by the ReportGenerationService
        public int? GetCompanyIDFromDB(int? reportID)
        {
            if (reportID == null)
                throw new PXException("ReportID cannot be null when retrieving CompanyID.");

            using (new PXConnectionScope())
            {
                var result = PXDatabase.SelectSingle<FLRTFinancialReport>(
                    new PXDataField("CompanyID"),
                    new PXDataFieldValue(nameof(FLRTFinancialReport.ReportID), reportID)
                );

                if (result != null) return result.GetInt32(0);
            }
            throw new PXException($"No CompanyID found for ReportID {reportID}.");
        }

        public string MapCompanyIDToTenantName(int? companyID)
        {
            if (companyID == null)
                throw new PXException(Messages.NoCompanyNum);

            FLRTTenantCredentials tenantCreds = PXSelect<FLRTTenantCredentials,
                Where<FLRTTenantCredentials.companyNum, Equal<Required<FLRTTenantCredentials.companyNum>>>>
                .Select(this, companyID);

            if (tenantCreds == null || string.IsNullOrEmpty(tenantCreds.TenantName))
            {
                PXTrace.WriteError($"No tenant found in FLRTTenantCredentials for CompanyID: {companyID}");
                throw new PXException(Messages.NoTenantMapping);
            }

            return tenantCreds.TenantName;
        }
        #endregion
    }
}
