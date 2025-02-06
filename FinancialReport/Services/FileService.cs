using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using PX.Data;
using PX.SM;
using PX.Objects.GL;


namespace FinancialReport.Services
{
    public class FileService
    {

        private readonly PXGraph _graph;

        public FileService(PXGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        /// <summary>
        /// Retrieves file content from Acumatica using NoteID.
        /// </summary>
        public byte[] GetFileContent(Guid? noteID)
        {
            if (noteID == null)
                throw new PXException(Messages.NoteIDIsNull);

            var uploadedFiles = new PXSelectJoin<UploadFile,
                InnerJoin<NoteDoc, On<UploadFile.fileID, Equal<NoteDoc.fileID>>>,
                Where<
                    NoteDoc.noteID, Equal<Required<NoteDoc.noteID>>,
                    And<UploadFile.name, Like<Required<UploadFile.name>>>>,
                OrderBy<Asc<UploadFile.createdDateTime>>>(_graph)
                .Select(noteID, "%FRTemplate%");

            if (uploadedFiles == null || uploadedFiles.Count == 0)
                throw new PXException(Messages.NoFilesAssociated);

            foreach (PXResult<UploadFile, NoteDoc> result in uploadedFiles)
            {
                var file = (UploadFile)result;
                // ✅ Retrieve UploadFileRevision separately with proper casting
                UploadFileRevision fileRevision = PXSelect<UploadFileRevision,
                    Where<UploadFileRevision.fileID, Equal<Required<UploadFileRevision.fileID>>>>
                    .Select(_graph, file.FileID)
                    .RowCast<UploadFileRevision>() // ✅ Ensure proper casting
                    .FirstOrDefault();

                if (fileRevision?.BlobData != null) // ✅ Corrected from 'Data' to 'BlobData'
                {
                    return fileRevision.BlobData;
                }
            }

            throw new PXException(Messages.FailedToRetrieveFile);
        }

        /// <summary>
        /// Saves a generated document into Acumatica and returns the FileID.
        /// </summary>
        public Guid SaveGeneratedDocument(string fileName, byte[] fileContent, FLRTFinancialReport currentRecord)
        {
            var fileGraph = PXGraph.CreateInstance<UploadFileMaintenance>();
            var fileInfo = new PX.SM.FileInfo(fileName, null, fileContent)
            {
                IsPublic = true
            };

            bool saved = fileGraph.SaveFile(fileInfo);
            if (!saved)
            {
                throw new PXException(Messages.UnableToSaveGeneratedFile);
            }

            if (fileInfo.UID.HasValue)
            {
                PXNoteAttribute.SetFileNotes(_graph.Caches[typeof(FLRTFinancialReport)], currentRecord, fileInfo.UID.Value);
            }

            return fileInfo.UID ?? Guid.Empty;
        }

    }
}
