-- ============================================================
-- Script 01: Add DefinitionPrefix column to FLRTReportDefinition
-- Run this FIRST before any other migration scripts.
-- ============================================================

-- Add the DefinitionPrefix column (nullable initially to allow migration)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'FLRTReportDefinition'
      AND COLUMN_NAME = 'DefinitionPrefix'
)
BEGIN
    ALTER TABLE FLRTReportDefinition
    ADD DefinitionPrefix NVARCHAR(10) NULL;

    PRINT 'Column DefinitionPrefix added to FLRTReportDefinition.';
END
ELSE
BEGIN
    PRINT 'Column DefinitionPrefix already exists on FLRTReportDefinition — skipping.';
END
GO

-- Pre-populate DefinitionPrefix from the first 10 characters of DefinitionCD
-- for all existing definitions that don't have a prefix yet.
-- After running, review and manually set cleaner prefixes (e.g. BS, PL, CF)
-- before enforcing the NOT NULL constraint below.
UPDATE FLRTReportDefinition
SET DefinitionPrefix = UPPER(LEFT(REPLACE(DefinitionCD, '_', ''), 10))
WHERE DefinitionPrefix IS NULL
   OR DefinitionPrefix = '';

PRINT 'Pre-populated DefinitionPrefix from DefinitionCD for existing rows.';
GO

-- Verify — review these values and update any that are ambiguous before proceeding
SELECT DefinitionID, DefinitionCD, DefinitionPrefix
FROM FLRTReportDefinition
ORDER BY DefinitionCD;
GO

-- ============================================================
-- AFTER reviewing the values above and updating as needed,
-- run the following to enforce uniqueness and NOT NULL:
-- ============================================================

-- Enforce NOT NULL once all rows have been given a valid prefix
-- ALTER TABLE FLRTReportDefinition
-- ALTER COLUMN DefinitionPrefix NVARCHAR(10) NOT NULL;

-- Add unique constraint
-- IF NOT EXISTS (
--     SELECT 1 FROM sys.indexes
--     WHERE name = 'UX_FLRTReportDefinition_Prefix'
--       AND object_id = OBJECT_ID('FLRTReportDefinition')
-- )
-- BEGIN
--     ALTER TABLE FLRTReportDefinition
--     ADD CONSTRAINT UX_FLRTReportDefinition_Prefix UNIQUE (DefinitionPrefix);
--     PRINT 'Unique constraint added on DefinitionPrefix.';
-- END
