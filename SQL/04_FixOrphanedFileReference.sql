-- ============================================================
-- Script 04: Diagnose and fix orphaned file reference
-- Addresses validation warning:
--   "Cannot access the uploaded file.
--    Failed to get the latest revision of the file
--    757864dc-0595-49c3-963d-baa649bb6969"
-- ============================================================

DECLARE @BadFileID UNIQUEIDENTIFIER = '757864DC-0595-49C3-963D-BAA649BB6969';

-- ============================================================
-- SECTION 1: DIAGNOSE — run this first, review before cleanup
-- ============================================================

PRINT '--- 1. UploadFile record ---';
SELECT FileID, [Name], CreatedDateTime, LastModifiedDateTime
FROM UploadFile
WHERE FileID = @BadFileID;

PRINT '--- 2. UploadFileRevision records ---';
SELECT FileID, FileRevisionID, [Size], CreatedDateTime
FROM UploadFileRevision
WHERE FileID = @BadFileID;

PRINT '--- 3. NoteDoc references (which records link to this file) ---';
SELECT nd.NoteID, nd.FileID
FROM NoteDoc nd
WHERE nd.FileID = @BadFileID;

PRINT '--- 4. FLRTFinancialReport references ---';
SELECT ReportID, ReportCD, UploadedFileID, GeneratedFileID, Noteid
FROM FLRTFinancialReport
WHERE UploadedFileID    = @BadFileID
   OR GeneratedFileID   = @BadFileID;

PRINT '--- 5. NoteDoc + FLRTFinancialReport join (file attached via note) ---';
SELECT fr.ReportID, fr.ReportCD, nd.FileID, nd.NoteID
FROM FLRTFinancialReport fr
INNER JOIN NoteDoc nd ON nd.NoteID = fr.Noteid
WHERE nd.FileID = @BadFileID;

PRINT '--- 6. Customisation project content referencing this file ---';
-- SYProjectContent stores items in the customisation project;
-- some item types embed a FileID in the ProjectID / ExternalKey column.
SELECT ProjectID, [Key], [Type], [ExternalKey], CAST(LEFT(Content, 200) AS NVARCHAR(200)) AS ContentPreview
FROM SYProjectContent
WHERE [ExternalKey] LIKE '%757864DC%'
   OR CAST(Content AS NVARCHAR(MAX)) LIKE '%757864dc%';

PRINT '--- 7. All SYProject projects (for context) ---';
SELECT ProjectID, [Name], [Status]
FROM SYProject;

PRINT '--- 8. All UploadFile records with no revision (broader orphan check) ---';
SELECT uf.FileID, uf.[Name], uf.CreatedDateTime
FROM UploadFile uf
WHERE NOT EXISTS (
    SELECT 1 FROM UploadFileRevision ufr WHERE ufr.FileID = uf.FileID
);


-- ============================================================
-- SECTION 2: CLEANUP — uncomment ONE of the blocks below
-- ONLY after reviewing Section 1 output above.
-- ============================================================

/*
-- ── Option A: Remove the NoteDoc link (safest — removes the association
--              but keeps the UploadFile record intact) ──────────────────
DELETE FROM NoteDoc
WHERE FileID = @BadFileID;
PRINT 'NoteDoc link removed.';
*/

/*
-- ── Option B: Remove the UploadFile record and any orphaned revision ───
-- Use this if UploadFile exists but UploadFileRevision is missing/empty.
DELETE FROM UploadFileRevision WHERE FileID = @BadFileID;
DELETE FROM NoteDoc            WHERE FileID = @BadFileID;
DELETE FROM UploadFile         WHERE FileID = @BadFileID;
PRINT 'UploadFile, UploadFileRevision, and NoteDoc entries removed.';
*/

/*
-- ── Option C: Clear the stale fileID from FLRTFinancialReport ──────────
-- Use this if the GUID is stored directly in UploadedFileID or
-- GeneratedFileID on a report record that no longer needs it.
UPDATE FLRTFinancialReport
SET UploadedFileID    = NULL,
    UploadedFileIDDisplay = NULL
WHERE UploadedFileID = @BadFileID;

UPDATE FLRTFinancialReport
SET GeneratedFileID = NULL
WHERE GeneratedFileID = @BadFileID;
PRINT 'Stale file ID cleared from FLRTFinancialReport.';
*/

/*
-- ── Option D: Remove the SYProjectContent entry ────────────────────────
-- Use this if Section 6 found a row in SYProjectContent.
-- Replace @ProjectID and @Key with the actual values from Section 6.
DELETE FROM SYProjectContent
WHERE [ExternalKey] LIKE '%757864DC%';
PRINT 'SYProjectContent entry removed.';
*/
