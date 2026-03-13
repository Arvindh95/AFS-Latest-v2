-- ============================================================
-- Fix #18 — Migrate Status / SlideStatus to single-char codes
-- Table: FLRTFinancialReport
--
-- Old values (English sentences)  →  New codes
--   'File not Generated'           →  'N'  (Not generated)
--   'File Generation In Progress'  →  'P'  (In Progress)
--   'Ready to Download'            →  'C'  (Completed)
--   'Failed to Generate File'      →  'F'  (Failed)
--
-- Safe to run on any DB state:
--   • No data yet    — column resize is the only change
--   • Old data       — values migrated before column is shrunk
--   • Already migrated — UPDATE WHERE clauses match nothing; no-op
-- ============================================================

SET NOCOUNT ON;

PRINT '=== Fix #18: Status code migration ===';

-- ── 1. Migrate Status ────────────────────────────────────────
PRINT 'Migrating Status column...';

UPDATE [dbo].[FLRTFinancialReport] SET [Status] = 'N' WHERE [Status] = 'File not Generated';
PRINT '  N: ' + CAST(@@ROWCOUNT AS NVARCHAR) + ' row(s)';

UPDATE [dbo].[FLRTFinancialReport] SET [Status] = 'P' WHERE [Status] = 'File Generation In Progress';
PRINT '  P: ' + CAST(@@ROWCOUNT AS NVARCHAR) + ' row(s)';

UPDATE [dbo].[FLRTFinancialReport] SET [Status] = 'C' WHERE [Status] = 'Ready to Download';
PRINT '  C: ' + CAST(@@ROWCOUNT AS NVARCHAR) + ' row(s)';

UPDATE [dbo].[FLRTFinancialReport] SET [Status] = 'F' WHERE [Status] = 'Failed to Generate File';
PRINT '  F: ' + CAST(@@ROWCOUNT AS NVARCHAR) + ' row(s)';

-- ── 2. Migrate SlideStatus ───────────────────────────────────
PRINT 'Migrating SlideStatus column...';

UPDATE [dbo].[FLRTFinancialReport] SET [SlideStatus] = 'N' WHERE [SlideStatus] = 'File not Generated';
PRINT '  N: ' + CAST(@@ROWCOUNT AS NVARCHAR) + ' row(s)';

UPDATE [dbo].[FLRTFinancialReport] SET [SlideStatus] = 'P' WHERE [SlideStatus] = 'File Generation In Progress';
PRINT '  P: ' + CAST(@@ROWCOUNT AS NVARCHAR) + ' row(s)';

UPDATE [dbo].[FLRTFinancialReport] SET [SlideStatus] = 'C' WHERE [SlideStatus] = 'Ready to Download';
PRINT '  C: ' + CAST(@@ROWCOUNT AS NVARCHAR) + ' row(s)';

UPDATE [dbo].[FLRTFinancialReport] SET [SlideStatus] = 'F' WHERE [SlideStatus] = 'Failed to Generate File';
PRINT '  F: ' + CAST(@@ROWCOUNT AS NVARCHAR) + ' row(s)';

-- ── 3. Resize Status → NVARCHAR(1) if still wider ────────────
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'FLRTFinancialReport'
      AND COLUMN_NAME = 'Status'
      AND CHARACTER_MAXIMUM_LENGTH > 1
)
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport]
        ALTER COLUMN [Status] NVARCHAR(1) NOT NULL;
    PRINT 'Status column resized to NVARCHAR(1)';
END
ELSE
    PRINT 'Status column already NVARCHAR(1) — skipped';

-- ── 4. Resize SlideStatus → NVARCHAR(1) if still wider ───────
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'FLRTFinancialReport'
      AND COLUMN_NAME = 'SlideStatus'
      AND CHARACTER_MAXIMUM_LENGTH > 1
)
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport]
        ALTER COLUMN [SlideStatus] NVARCHAR(1) NULL;
    PRINT 'SlideStatus column resized to NVARCHAR(1)';
END
ELSE
    PRINT 'SlideStatus column already NVARCHAR(1) — skipped';

PRINT '=== Done ===';
