-- ============================================================
-- Script 02: Create FLRTReportDefinitionLink table
-- Run AFTER Script 01.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'FLRTReportDefinitionLink'
)
BEGIN
    CREATE TABLE FLRTReportDefinitionLink
    (
        -- Primary key (auto-identity, maps to [PXDBIdentity])
        LinkID                  INT             IDENTITY(1,1)   NOT NULL,

        -- Parent report (FK to FLRTFinancialReport.ReportID)
        ReportID                INT             NOT NULL,

        -- Linked definition (FK to FLRTReportDefinition.DefinitionID)
        DefinitionID            INT             NOT NULL,

        -- Display order in the grid (cosmetic only — no effect on calculation order)
        DisplayOrder            INT             NOT NULL        DEFAULT 0,

        -- Standard Acumatica audit fields
        CreatedDateTime         DATETIME        NULL,
        CreatedByID             UNIQUEIDENTIFIER NULL,
        CreatedByScreenID       CHAR(8)         NULL,
        LastModifiedDateTime    DATETIME        NULL,
        LastModifiedByID        UNIQUEIDENTIFIER NULL,
        LastModifiedByScreenID  CHAR(8)         NULL,
        Tstamp                  ROWVERSION      NOT NULL,

        CONSTRAINT PK_FLRTReportDefinitionLink PRIMARY KEY (LinkID)
    );

    PRINT 'Table FLRTReportDefinitionLink created.';
END
ELSE
BEGIN
    PRINT 'Table FLRTReportDefinitionLink already exists — skipping creation.';
END
GO

-- Index for fast lookup of all links for a given report
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_FLRTReportDefinitionLink_ReportID'
      AND object_id = OBJECT_ID('FLRTReportDefinitionLink')
)
BEGIN
    CREATE INDEX IX_FLRTReportDefinitionLink_ReportID
    ON FLRTReportDefinitionLink (ReportID);

    PRINT 'Index IX_FLRTReportDefinitionLink_ReportID created.';
END
GO

-- Prevent the same definition being linked twice to the same report
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_FLRTReportDefinitionLink_ReportDef'
      AND object_id = OBJECT_ID('FLRTReportDefinitionLink')
)
BEGIN
    ALTER TABLE FLRTReportDefinitionLink
    ADD CONSTRAINT UX_FLRTReportDefinitionLink_ReportDef
    UNIQUE (ReportID, DefinitionID);

    PRINT 'Unique constraint UX_FLRTReportDefinitionLink_ReportDef added.';
END
GO

-- Optional foreign keys (add if referential integrity is desired)
-- ALTER TABLE FLRTReportDefinitionLink
-- ADD CONSTRAINT FK_DefinitionLink_Report
--     FOREIGN KEY (ReportID) REFERENCES FLRTFinancialReport(ReportID);

-- ALTER TABLE FLRTReportDefinitionLink
-- ADD CONSTRAINT FK_DefinitionLink_Definition
--     FOREIGN KEY (DefinitionID) REFERENCES FLRTReportDefinition(DefinitionID);

-- Verify
SELECT 'FLRTReportDefinitionLink table created successfully.' AS Result;
GO
