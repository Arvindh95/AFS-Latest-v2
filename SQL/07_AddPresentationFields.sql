-- =============================================
-- FLRT Financial Report Module
-- Migration 07: Add Presentation / Alai API fields
-- Idempotent: safe to run multiple times.
-- =============================================

PRINT '======================================================'
PRINT 'Migration 07 - Add Presentation Fields'
PRINT '======================================================'

-- =============================================
-- FLRTFinancialReport: add presentation columns
-- =============================================
PRINT ''
PRINT '--- FLRTFinancialReport: presentation fields ---'

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'PresentationTitle')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport]
        ADD [PresentationTitle] [nvarchar](500) NULL;
    PRINT '  Added PresentationTitle'
END
ELSE
    PRINT '  PresentationTitle already exists — skipped'

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'PresentationDescription')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport]
        ADD [PresentationDescription] [nvarchar](2000) NULL;
    PRINT '  Added PresentationDescription'
END
ELSE
    PRINT '  PresentationDescription already exists — skipped'

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'SlideGeneratedFileID')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport]
        ADD [SlideGeneratedFileID] [uniqueidentifier] NULL;
    PRINT '  Added SlideGeneratedFileID'
END
ELSE
    PRINT '  SlideGeneratedFileID already exists — skipped'

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'SlideStatus')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport]
        ADD [SlideStatus] [nvarchar](20) NULL DEFAULT('File not Generated');
    PRINT '  Added SlideStatus'
END
ELSE
    PRINT '  SlideStatus already exists — skipped'

-- =============================================
-- FLRTTenantCredentials: add Alai API key column
-- =============================================
PRINT ''
PRINT '--- FLRTTenantCredentials: Alai API key ---'

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'AlaiApiKey')
BEGIN
    ALTER TABLE [dbo].[FLRTTenantCredentials]
        ADD [AlaiApiKey] [nvarchar](255) NULL;
    PRINT '  Added AlaiApiKey'
END
ELSE
    PRINT '  AlaiApiKey already exists — skipped'

PRINT ''
PRINT '======================================================'
PRINT 'Migration 07 complete.'
PRINT '======================================================'
