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
        public SelectFrom<FLRTFinancialReport>.View FinancialReport = null!; // Initialized by PXGraph framework
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
        }

        protected void FLRTFinancialReport_RowPersisting(PXCache cache, PXRowPersistingEventArgs e)
        {
            var row = (FLRTFinancialReport)e.Row;
            if (row == null) return;

            // Update file ID fields before persisting if needed
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
                        }
                    }
                }
            }
        }

        protected virtual void FLRTFinancialReport_RowInserted(PXCache sender, PXRowInsertedEventArgs e)
        {
            // Acuminator disable once PX1043 SavingChangesInEventHandlers [Justification]
            this.Actions.PressSave();
            FinancialReport.Current = null!; // Clear current record in Acumatica framework
            FinancialReport.Cache.Clear();
            FinancialReport.Cache.ClearQueryCache();
            FinancialReport.View.Clear();
        }
        #endregion

        #region Actions
        public PXSave<FLRTFinancialReport> Save = null!; // Initialized by PXGraph framework
        public PXCancel<FLRTFinancialReport> Cancel = null!; // Initialized by PXGraph framework
        public PXAction<FLRTFinancialReport> GenerateReport = null!; // Initialized by PXGraph framework
        public PXAction<FLRTFinancialReport> DownloadReport = null!; // Initialized by PXGraph framework
        public PXAction<FLRTFinancialReport> ResetStatus = null!; // Initialized by PXGraph framework

        [PXButton(CommitChanges = false)]
        [PXUIField(DisplayName = "Generate Report")]
        protected virtual System.Collections.IEnumerable generateReport(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Cache.Cached
                .Cast<FLRTFinancialReport>()
                .FirstOrDefault(item => item.Selected == true);

            // Perform initial validations on the UI thread
            if (selectedRecord == null) throw new PXException(Messages.PleaseSelectTemplate);
            if (selectedRecord.ReportID == null) throw new PXException(Messages.NoReportSelected);
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
            //authService.AuthenticateAndGetToken();
            //PXTrace.WriteInformation($"Successfully authenticated for {tenantName}.");

            // Mark record as In Progress and save before starting background task
            selectedRecord.Status = ReportStatus.InProgress;
            FinancialReport.Update(selectedRecord);
            Actions.PressSave();

            int? reportID = selectedRecord.ReportID;

            PXLongOperation.StartOperation(this, () =>
            {
                var reportGraph = PXGraph.CreateInstance<FLRTFinancialReportMaint>();
                FLRTFinancialReport dbRecord = null;

                try
                {
                    dbRecord = PXSelect<FLRTFinancialReport, Where<FLRTFinancialReport.reportID, Equal<Required<FLRTFinancialReport.reportID>>>>
                        .SelectSingleBound(reportGraph, null, reportID);

                    // Timeout protection: 15 minutes maximum for report generation
                    var timeoutCancellation = new System.Threading.CancellationTokenSource();
                    const int timeoutMinutes = 15;
                    timeoutCancellation.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

                    var timeoutTask = System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            if (dbRecord == null) throw new PXException(Messages.FailedToRetrieveFile);
                            if (dbRecord.Noteid == null) throw new PXException(Messages.TemplateHasNoFiles);

                            // Check for cancellation before starting
                            timeoutCancellation.Token.ThrowIfCancellationRequested();

                        // Pre-authenticate INSIDE the long operation where async/sync issues are less problematic
                        PXTrace.WriteInformation("Authenticating...");
                        authService.AuthenticateAndGetToken();
                        PXTrace.WriteInformation($"Successfully authenticated for {tenantName}.");

                        // Check for cancellation before generation
                        timeoutCancellation.Token.ThrowIfCancellationRequested();

                        // Instantiate and execute the new service
                        var generationService = new ReportGenerationService(reportGraph, dbRecord, authService);
                        Guid generatedFileID = generationService.Execute();

                        // Check for cancellation before saving
                        timeoutCancellation.Token.ThrowIfCancellationRequested();

                        // Update the record with the successful result
                        dbRecord.GeneratedFileID = generatedFileID;
                        dbRecord.Status = ReportStatus.Completed;
                        reportGraph.FinancialReport.Update(dbRecord);
                        reportGraph.Actions.PressSave();
                        PXTrace.WriteInformation("Report generation completed successfully.");
                    }
                    catch (System.OperationCanceledException)
                    {
                        PXTrace.WriteError($"Report generation timed out after {timeoutMinutes} minutes");
                        if (dbRecord != null)
                        {
                            dbRecord.Status = ReportStatus.Failed;
                            reportGraph.FinancialReport.Update(dbRecord);
                            reportGraph.Actions.PressSave();
                        }
                        throw new PXException(Messages.ReportGenerationTimeout, timeoutMinutes);
                    }
                    catch (Exception ex)
                    {
                        PXTrace.WriteError($"Report generation failed: {ex.ToString()}");
                        if (dbRecord != null)
                        {
                            dbRecord.Status = ReportStatus.Failed;
                            reportGraph.FinancialReport.Update(dbRecord);
                            reportGraph.Actions.PressSave();
                        }
                        throw;
                    }
                    finally
                    {
                        authService?.Logout();
                        timeoutCancellation?.Dispose();
                    }
                }, timeoutCancellation.Token);

                    try
                    {
                        timeoutTask.Wait();
                    }
                    catch (System.AggregateException aex)
                    {
                        // Unwrap AggregateException and throw the actual exception
                        throw aex.InnerException ?? aex;
                    }
                }
                catch (Exception)
                {
                    // If ANY unhandled exception occurs (including user cancellation),
                    // ensure status is set to Failed
                    if (dbRecord != null && dbRecord.Status == ReportStatus.InProgress)
                    {
                        dbRecord.Status = ReportStatus.Failed;
                        reportGraph.FinancialReport.Update(dbRecord);
                        try
                        {
                            reportGraph.Actions.PressSave();
                        }
                        catch
                        {
                            // Ignore save errors in cleanup
                        }
                    }
                    throw; // Re-throw to show user the error
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

        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Reset Status", MapEnableRights = PXCacheRights.Update, Visible = true)]
        protected virtual System.Collections.IEnumerable resetStatus(PXAdapter adapter)
        {
            var selectedRecord = FinancialReport.Cache.Cached
                .Cast<FLRTFinancialReport>()
                .FirstOrDefault(x => x.Selected == true);

            if (selectedRecord == null)
            {
                throw new PXException(Messages.UnselectedResetStatus);
            }

            string currentStatus = selectedRecord.Status ?? "Unknown";
            string statusName = currentStatus == ReportStatus.InProgress ? "In Progress" :
                               currentStatus == ReportStatus.Failed ? "Failed" :
                               currentStatus == ReportStatus.Completed ? "Completed" : "Pending";

            // Ask for confirmation before resetting
            if (FinancialReport.Ask("Confirm Reset",
                $"Reset this report from '{statusName}' to 'Pending'? This will allow regeneration.",
                MessageButtons.YesNo) == WebDialogResult.Yes)
            {
                selectedRecord.Status = ReportStatus.Pending;
                selectedRecord.GeneratedFileID = null; // Clear the generated file so it doesn't show old data
                FinancialReport.Update(selectedRecord);
                Actions.PressSave();

                PXTrace.WriteInformation($"Report {selectedRecord.ReportCD} status reset from {statusName} to Pending.");
            }

            return adapter.Get();
        }
        #endregion

        #region Public Helper Methods
        // These methods are made public so they can be called by the ReportGenerationService
        public int? GetCompanyIDFromDB(int? reportID)
        {
            if (reportID == null)
                throw new PXException(Messages.ReportIDNull);

            using (new PXConnectionScope())
            {
                var result = PXDatabase.SelectSingle<FLRTFinancialReport>(
                    new PXDataField("CompanyID"),
                    new PXDataFieldValue(nameof(FLRTFinancialReport.ReportID), reportID)
                );

                if (result != null) return result.GetInt32(0);
            }
            throw new PXException(Messages.NoCompanyIDFound);
        }

        public string MapCompanyIDToTenantName(int? companyID)
        {
            if (companyID == null)
                throw new PXException(Messages.CompanyNumRequired);

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
