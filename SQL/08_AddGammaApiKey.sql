-- =============================================
-- FLRT Financial Report Module
-- Migration 08: Add Gamma API Key column
-- Idempotent: safe to run multiple times.
-- =============================================

PRINT '======================================================'
PRINT 'Migration 08 - Add Gamma API Key'
PRINT '======================================================'

-- =============================================
-- FLRTTenantCredentials: add GammaApiKey column
-- =============================================
PRINT ''
PRINT '--- FLRTTenantCredentials: GammaApiKey ---'

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'GammaApiKey')
BEGIN
    ALTER TABLE [dbo].[FLRTTenantCredentials]
        ADD [GammaApiKey] [nvarchar](255) NULL;
    PRINT '  Added GammaApiKey'
END
ELSE
    PRINT '  GammaApiKey already exists — skipped'

-- =============================================
-- FLRTFinancialReport: add GammaTemplateId column
-- =============================================
PRINT ''
PRINT '--- FLRTFinancialReport: GammaTemplateId ---'

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'GammaTemplateId')
BEGIN
    ALTER TABLE [dbo].[FLRTFinancialReport]
        ADD [GammaTemplateId] [nvarchar](100) NULL;
    PRINT '  Added GammaTemplateId'
END
ELSE
    PRINT '  GammaTemplateId already exists — skipped'

PRINT ''
PRINT '======================================================'
PRINT 'Migration 08 complete.'
PRINT '======================================================'
