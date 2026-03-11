-- ============================================================
-- Script 03: Migrate existing reports from legacy DefinitionID
--            to the new FLRTReportDefinitionLink table.
--
-- Run AFTER Scripts 01 and 02.
-- Run AFTER you have reviewed and corrected DefinitionPrefix values
-- for all existing FLRTReportDefinition rows.
--
-- This script is safe to run multiple times (idempotent).
-- ============================================================

-- Preview: show which reports have a legacy DefinitionID set
-- but no rows yet in FLRTReportDefinitionLink
SELECT
    r.ReportID,
    r.ReportCD,
    r.DefinitionID,
    d.DefinitionCD,
    d.DefinitionPrefix
FROM FLRTFinancialReport r
INNER JOIN FLRTReportDefinition d ON d.DefinitionID = r.DefinitionID
WHERE r.DefinitionID IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM FLRTReportDefinitionLink l
      WHERE l.ReportID = r.ReportID
  )
ORDER BY r.ReportCD;
GO

-- Migrate: insert one FLRTReportDefinitionLink row for each report
-- that has a legacy DefinitionID but no link rows yet.
INSERT INTO FLRTReportDefinitionLink
    (ReportID, DefinitionID, DisplayOrder, CreatedDateTime, LastModifiedDateTime)
SELECT
    r.ReportID,
    r.DefinitionID,
    0,                  -- DisplayOrder = 0 (first/only definition)
    GETDATE(),
    GETDATE()
FROM FLRTFinancialReport r
WHERE r.DefinitionID IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM FLRTReportDefinitionLink l
      WHERE l.ReportID = r.ReportID
  );

PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' report(s) migrated to FLRTReportDefinitionLink.';
GO

-- Verify migration result
SELECT
    l.LinkID,
    l.ReportID,
    r.ReportCD,
    l.DefinitionID,
    d.DefinitionCD,
    d.DefinitionPrefix,
    l.DisplayOrder
FROM FLRTReportDefinitionLink l
INNER JOIN FLRTFinancialReport r   ON r.ReportID = l.ReportID
INNER JOIN FLRTReportDefinition d ON d.DefinitionID = l.DefinitionID
ORDER BY r.ReportCD, l.DisplayOrder;
GO

-- ============================================================
-- OPTIONAL: After confirming migration is correct,
-- you may clear the legacy DefinitionID column on the report.
-- The column is kept as a fallback and can remain populated —
-- the generation service checks FLRTReportDefinitionLink first.
-- ============================================================

-- UPDATE FLRTFinancialReport SET DefinitionID = NULL
-- WHERE DefinitionID IS NOT NULL
--   AND EXISTS (
--       SELECT 1 FROM FLRTReportDefinitionLink l WHERE l.ReportID = ReportID
--   );
-- PRINT 'Legacy DefinitionID cleared on migrated reports.';
