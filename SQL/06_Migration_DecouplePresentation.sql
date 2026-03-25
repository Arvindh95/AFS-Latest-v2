-- =============================================
-- FLRT Financial Report Module - Master Deployment Script
-- Idempotent: safe to run on fresh or existing databases.
-- For each table: CREATE if not exists, ALTER to add missing columns if exists.
-- Includes migration: drops legacy presentation columns from FLRTFinancialReport
-- and creates standalone FLRTPresentationGeneration + FLRTPresentationDefinitionLink.
-- =============================================

PRINT '======================================================'
PRINT 'FLRT Financial Report Module - Table Deployment'
PRINT '======================================================'

-- =============================================
-- 1. FLRTFinancialReport
-- =============================================
PRINT ''
PRINT '--- FLRTFinancialReport ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTFinancialReport...'

    CREATE TABLE [dbo].[FLRTFinancialReport] (
        [CompanyID]              [int]            NOT NULL DEFAULT(0),
        [ReportID]               [int]            IDENTITY(1,1) NOT NULL,
        [ReportCD]               [nvarchar](225)  NULL,
        [Description]            [nvarchar](50)   NULL,
        [CurrYear]               [nvarchar](4)    NULL,
        [FinancialMonth]         [nvarchar](50)   NULL DEFAULT('12'),
        [Branch]                 [nvarchar](10)   NULL,
        [Organization]           [nvarchar](50)   NULL,
        [Ledger]                 [nvarchar](20)   NULL,
        [DefinitionID]           [int]            NULL,
        [Status]                 [nvarchar](1)    NOT NULL DEFAULT('N'),
        [GeneratedFileID]        [uniqueidentifier] NULL,
        [UploadedFileID]         [uniqueidentifier] NULL,
        [UploadedFileIDDisplay]  [nvarchar](225)  NULL,
        [CompanyNum]             [int]            NULL,
        [Selected]               [bit]            NULL DEFAULT(0),
        [NoteID]                 [uniqueidentifier] NOT NULL DEFAULT(NEWID()),
        [CreatedDateTime]        [datetime]       NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)        NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]       NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)        NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]      NOT NULL,

        CONSTRAINT [PK_FLRTFinancialReport] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [ReportID] ASC)
    )

    PRINT 'FLRTFinancialReport created.'
END
ELSE
BEGIN
    PRINT 'FLRTFinancialReport exists. Checking columns...'

    -- Add missing columns (for databases created before these columns existed)
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'DefinitionID')
    BEGIN ALTER TABLE [dbo].[FLRTFinancialReport] ADD [DefinitionID] [int] NULL
          PRINT '  + DefinitionID' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'CompanyNum')
    BEGIN ALTER TABLE [dbo].[FLRTFinancialReport] ADD [CompanyNum] [int] NULL
          PRINT '  + CompanyNum' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'UploadedFileID')
    BEGIN ALTER TABLE [dbo].[FLRTFinancialReport] ADD [UploadedFileID] [uniqueidentifier] NULL
          PRINT '  + UploadedFileID' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'UploadedFileIDDisplay')
    BEGIN ALTER TABLE [dbo].[FLRTFinancialReport] ADD [UploadedFileIDDisplay] [nvarchar](225) NULL
          PRINT '  + UploadedFileIDDisplay' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'GeneratedFileID')
    BEGIN ALTER TABLE [dbo].[FLRTFinancialReport] ADD [GeneratedFileID] [uniqueidentifier] NULL
          PRINT '  + GeneratedFileID' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'Organization')
    BEGIN ALTER TABLE [dbo].[FLRTFinancialReport] ADD [Organization] [nvarchar](50) NULL
          PRINT '  + Organization' END

    -- Migrate Status from old NVARCHAR(100)/'File not Generated' to NVARCHAR(1)/'N'
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'FLRTFinancialReport' AND COLUMN_NAME = 'Status'
                 AND CHARACTER_MAXIMUM_LENGTH > 1)
    BEGIN
        -- Map old status text values to new single-char codes
        EXEC('UPDATE [dbo].[FLRTFinancialReport] SET [Status] = CASE
            WHEN [Status] = ''File not Generated'' THEN ''N''
            WHEN [Status] = ''In Progress''        THEN ''P''
            WHEN [Status] = ''Ready to Download''  THEN ''C''
            WHEN [Status] = ''Failed''             THEN ''F''
            ELSE ''N'' END')

        -- Drop existing default constraint on Status
        DECLARE @statusConstraint NVARCHAR(256)
        SELECT @statusConstraint = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND c.name = 'Status'

        IF @statusConstraint IS NOT NULL
        BEGIN
            EXEC('ALTER TABLE [dbo].[FLRTFinancialReport] DROP CONSTRAINT [' + @statusConstraint + ']')
            PRINT '  - Status default constraint dropped'
        END

        ALTER TABLE [dbo].[FLRTFinancialReport] ALTER COLUMN [Status] NVARCHAR(1) NOT NULL
        ALTER TABLE [dbo].[FLRTFinancialReport] ADD DEFAULT('N') FOR [Status]
        PRINT '  ~ Status migrated to NVARCHAR(1) with single-char codes'
    END

    -- -------------------------------------------------------
    -- Drop legacy presentation columns (moved to FLRTPresentationGeneration)
    -- -------------------------------------------------------
    PRINT '  Checking for legacy presentation columns to drop...'

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'PresentationTitle')
    BEGIN
        ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [PresentationTitle]
        PRINT '  - PresentationTitle dropped'
    END

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'PresentationDescription')
    BEGIN
        ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [PresentationDescription]
        PRINT '  - PresentationDescription dropped'
    END

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'GammaTemplateId')
    BEGIN
        ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [GammaTemplateId]
        PRINT '  - GammaTemplateId dropped'
    END

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'SlideGeneratedFileID')
    BEGIN
        ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [SlideGeneratedFileID]
        PRINT '  - SlideGeneratedFileID dropped'
    END

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'PresentationMarkdown')
    BEGIN
        ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [PresentationMarkdown]
        PRINT '  - PresentationMarkdown dropped'
    END

    -- SlideStatus has a default constraint — must drop it first
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND name = 'SlideStatus')
    BEGIN
        DECLARE @slideConstraint NVARCHAR(256)
        SELECT @slideConstraint = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID(N'[dbo].[FLRTFinancialReport]') AND c.name = 'SlideStatus'

        IF @slideConstraint IS NOT NULL
        BEGIN
            EXEC('ALTER TABLE [dbo].[FLRTFinancialReport] DROP CONSTRAINT [' + @slideConstraint + ']')
            PRINT '  - SlideStatus default constraint dropped'
        END

        ALTER TABLE [dbo].[FLRTFinancialReport] DROP COLUMN [SlideStatus]
        PRINT '  - SlideStatus dropped'
    END

    PRINT 'FLRTFinancialReport column check done.'
END
GO

-- =============================================
-- 2. FLRTTenantCredentials
-- =============================================
PRINT ''
PRINT '--- FLRTTenantCredentials ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTTenantCredentials...'

    CREATE TABLE [dbo].[FLRTTenantCredentials] (
        [CompanyID]              [int]            NOT NULL DEFAULT(0),
        [CompanyNum]             [int]            NOT NULL,
        [TenantName]             [nvarchar](50)   NULL,
        [BaseURL]                [nvarchar](255)  NULL,
        [ClientIDNew]            [nvarchar](255)  NULL,
        [ClientSecretNew]        [nvarchar](255)  NULL,
        [UsernameNew]            [nvarchar](255)  NULL,
        [PasswordNew]            [nvarchar](255)  NULL,
        [AlaiApiKey]             [nvarchar](255)  NULL,
        [SlidesGptApiKey]        [nvarchar](255)  NULL,
        [GammaApiKey]            [nvarchar](255)  NULL,
        [NoteID]                 [uniqueidentifier] NOT NULL DEFAULT(NEWID()),
        [CreatedDateTime]        [datetime]       NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)        NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]       NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)        NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]      NOT NULL,

        CONSTRAINT [PK_FLRTTenantCredentials] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [CompanyNum] ASC)
    )

    PRINT 'FLRTTenantCredentials created.'
END
ELSE
BEGIN
    PRINT 'FLRTTenantCredentials exists. Checking missing columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'BaseURL')
    BEGIN ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [BaseURL] [nvarchar](255) NULL
          PRINT '  + BaseURL' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'ClientIDNew')
    BEGIN ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [ClientIDNew] [nvarchar](255) NULL
          PRINT '  + ClientIDNew' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'ClientSecretNew')
    BEGIN ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [ClientSecretNew] [nvarchar](255) NULL
          PRINT '  + ClientSecretNew' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'UsernameNew')
    BEGIN ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [UsernameNew] [nvarchar](255) NULL
          PRINT '  + UsernameNew' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'PasswordNew')
    BEGIN ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [PasswordNew] [nvarchar](255) NULL
          PRINT '  + PasswordNew' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'AlaiApiKey')
    BEGIN ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [AlaiApiKey] [nvarchar](255) NULL
          PRINT '  + AlaiApiKey' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'SlidesGptApiKey')
    BEGIN ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [SlidesGptApiKey] [nvarchar](255) NULL
          PRINT '  + SlidesGptApiKey' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'GammaApiKey')
    BEGIN ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [GammaApiKey] [nvarchar](255) NULL
          PRINT '  + GammaApiKey' END

    -- Migrate: add CompanyID column and rebuild PK if CompanyID is missing
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTTenantCredentials]') AND name = 'CompanyID')
    BEGIN
        PRINT '  Migrating: adding CompanyID to FLRTTenantCredentials...'

        -- Drop existing PK (name may be auto-generated, so look it up dynamically)
        DECLARE @pkName NVARCHAR(128)
        SELECT @pkName = name FROM sys.key_constraints
            WHERE parent_object_id = OBJECT_ID('FLRTTenantCredentials') AND type = 'PK'
        EXEC('ALTER TABLE [dbo].[FLRTTenantCredentials] DROP CONSTRAINT [' + @pkName + ']')

        -- Add CompanyID column
        ALTER TABLE [dbo].[FLRTTenantCredentials] ADD [CompanyID] [int] NOT NULL DEFAULT(0)

        -- Set CompanyID = CompanyNum for all existing rows
        EXEC('UPDATE [dbo].[FLRTTenantCredentials] SET [CompanyID] = [CompanyNum]')

        -- Recreate PK with CompanyID as first component
        ALTER TABLE [dbo].[FLRTTenantCredentials]
            ADD CONSTRAINT [PK_FLRTTenantCredentials] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [CompanyNum] ASC)

        PRINT '  + CompanyID (PK rebuilt)'
    END

    PRINT 'FLRTTenantCredentials column check done.'
END
GO

-- =============================================
-- 3. FLRTReportDefinition
-- =============================================
PRINT ''
PRINT '--- FLRTReportDefinition ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTReportDefinition...'

    CREATE TABLE [dbo].[FLRTReportDefinition] (
        [CompanyID]              [int]            NOT NULL DEFAULT(0),
        [DefinitionID]           [int]            IDENTITY(1,1) NOT NULL,
        [DefinitionCD]           [nvarchar](50)   NOT NULL,
        [DefinitionPrefix]       [nvarchar](10)   NULL,
        [Description]            [nvarchar](255)  NULL,
        [ReportType]             [nvarchar](10)   NULL DEFAULT('BS'),
        [IsActive]               [bit]            NULL DEFAULT(1),
        [GIName]                 [nvarchar](100)  NULL DEFAULT('TrialBalance'),
        [AccountColumn]          [nvarchar](100)  NULL DEFAULT('Account'),
        [TypeColumn]             [nvarchar](100)  NULL DEFAULT('Type'),
        [BeginningBalColumn]     [nvarchar](100)  NULL DEFAULT('BeginningBalance'),
        [EndingBalColumn]        [nvarchar](100)  NULL DEFAULT('EndingBalance'),
        [DebitColumn]            [nvarchar](100)  NULL DEFAULT('Debit'),
        [CreditColumn]           [nvarchar](100)  NULL DEFAULT('Credit'),
        [RoundingLevel]          [nvarchar](10)   NULL DEFAULT('UNITS'),
        [DecimalPlaces]          [int]            NULL DEFAULT(0),
        [CreatedDateTime]        [datetime]       NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)        NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]       NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)        NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]      NOT NULL,

        CONSTRAINT [PK_FLRTReportDefinition] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [DefinitionCD] ASC)
    )

    PRINT 'FLRTReportDefinition created.'
END
ELSE
BEGIN
    PRINT 'FLRTReportDefinition exists. Checking missing columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'DefinitionPrefix')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [DefinitionPrefix] [nvarchar](10) NULL
          PRINT '  + DefinitionPrefix' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'GIName')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [GIName] [nvarchar](100) NULL DEFAULT('TrialBalance')
          PRINT '  + GIName' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'AccountColumn')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [AccountColumn] [nvarchar](100) NULL DEFAULT('Account')
          PRINT '  + AccountColumn' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'TypeColumn')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [TypeColumn] [nvarchar](100) NULL DEFAULT('Type')
          PRINT '  + TypeColumn' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'BeginningBalColumn')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [BeginningBalColumn] [nvarchar](100) NULL DEFAULT('BeginningBalance')
          PRINT '  + BeginningBalColumn' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'EndingBalColumn')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [EndingBalColumn] [nvarchar](100) NULL DEFAULT('EndingBalance')
          PRINT '  + EndingBalColumn' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'DebitColumn')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [DebitColumn] [nvarchar](100) NULL DEFAULT('Debit')
          PRINT '  + DebitColumn' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'CreditColumn')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [CreditColumn] [nvarchar](100) NULL DEFAULT('Credit')
          PRINT '  + CreditColumn' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'RoundingLevel')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [RoundingLevel] [nvarchar](10) NULL DEFAULT('UNITS')
          PRINT '  + RoundingLevel' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'DecimalPlaces')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [DecimalPlaces] [int] NULL DEFAULT(0)
          PRINT '  + DecimalPlaces' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinition]') AND name = 'IsActive')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinition] ADD [IsActive] [bit] NULL DEFAULT(1)
          PRINT '  + IsActive' END

    PRINT 'FLRTReportDefinition column check done.'
END
GO

-- =============================================
-- 4. FLRTReportDefinitionLink
-- =============================================
PRINT ''
PRINT '--- FLRTReportDefinitionLink ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinitionLink]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTReportDefinitionLink...'

    CREATE TABLE [dbo].[FLRTReportDefinitionLink] (
        [CompanyID]              [int]            NOT NULL DEFAULT(0),
        [LinkID]                 [int]            IDENTITY(1,1) NOT NULL,
        [ReportID]               [int]            NULL,
        [DefinitionID]           [int]            NULL,
        [DisplayOrder]           [int]            NULL DEFAULT(0),
        [CreatedDateTime]        [datetime]       NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)        NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]       NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)        NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]      NOT NULL,

        CONSTRAINT [PK_FLRTReportDefinitionLink] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LinkID] ASC)
    )

    PRINT 'FLRTReportDefinitionLink created.'
END
ELSE
BEGIN
    PRINT 'FLRTReportDefinitionLink exists. Checking missing columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinitionLink]') AND name = 'DisplayOrder')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinitionLink] ADD [DisplayOrder] [int] NULL DEFAULT(0)
          PRINT '  + DisplayOrder' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinitionLink]') AND name = 'ReportID')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinitionLink] ADD [ReportID] [int] NULL
          PRINT '  + ReportID' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportDefinitionLink]') AND name = 'DefinitionID')
    BEGIN ALTER TABLE [dbo].[FLRTReportDefinitionLink] ADD [DefinitionID] [int] NULL
          PRINT '  + DefinitionID' END

    PRINT 'FLRTReportDefinitionLink column check done.'
END
GO

-- =============================================
-- 5. FLRTReportLineItem
-- =============================================
PRINT ''
PRINT '--- FLRTReportLineItem ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTReportLineItem...'

    CREATE TABLE [dbo].[FLRTReportLineItem] (
        [CompanyID]              [int]            NOT NULL DEFAULT(0),
        [LineID]                 [int]            IDENTITY(1,1) NOT NULL,
        [DefinitionID]           [int]            NULL,
        [SortOrder]              [int]            NULL DEFAULT(0),
        [LineCode]               [nvarchar](100)  NULL,
        [Description]            [nvarchar](255)  NULL,
        [LineType]               [nvarchar](20)   NULL DEFAULT('ACCOUNT'),
        [AccountFrom]            [nvarchar](50)   NULL,
        [AccountTo]              [nvarchar](50)   NULL,
        [AccountTypeFilter]      [nvarchar](5)    NULL,
        [SignRule]               [nvarchar](10)   NULL DEFAULT('ASIS'),
        [BalanceType]            [nvarchar](15)   NULL DEFAULT('ENDING'),
        [ParentLineCode]         [nvarchar](100)  NULL,
        [Formula]                [nvarchar](500)  NULL,
        [IsVisible]              [bit]            NULL DEFAULT(1),
        [SubaccountFilter]       [nvarchar](30)   NULL,
        [BranchFilter]           [nvarchar](30)   NULL,
        [OrganizationFilter]     [nvarchar](30)   NULL,
        [LedgerFilter]           [nvarchar](20)   NULL,
        [NoteID]                 [uniqueidentifier] NULL,
        [CreatedDateTime]        [datetime]       NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)        NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]       NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)        NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]      NOT NULL,

        CONSTRAINT [PK_FLRTReportLineItem] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LineID] ASC)
    )

    PRINT 'FLRTReportLineItem created.'
END
ELSE
BEGIN
    PRINT 'FLRTReportLineItem exists. Checking missing columns...'

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = 'SubaccountFilter')
    BEGIN ALTER TABLE [dbo].[FLRTReportLineItem] ADD [SubaccountFilter] [nvarchar](30) NULL
          PRINT '  + SubaccountFilter' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = 'BranchFilter')
    BEGIN ALTER TABLE [dbo].[FLRTReportLineItem] ADD [BranchFilter] [nvarchar](30) NULL
          PRINT '  + BranchFilter' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = 'OrganizationFilter')
    BEGIN ALTER TABLE [dbo].[FLRTReportLineItem] ADD [OrganizationFilter] [nvarchar](30) NULL
          PRINT '  + OrganizationFilter' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = 'LedgerFilter')
    BEGIN ALTER TABLE [dbo].[FLRTReportLineItem] ADD [LedgerFilter] [nvarchar](20) NULL
          PRINT '  + LedgerFilter' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = 'IsVisible')
    BEGIN ALTER TABLE [dbo].[FLRTReportLineItem] ADD [IsVisible] [bit] NULL DEFAULT(1)
          PRINT '  + IsVisible' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = 'Formula')
    BEGIN ALTER TABLE [dbo].[FLRTReportLineItem] ADD [Formula] [nvarchar](500) NULL
          PRINT '  + Formula' END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FLRTReportLineItem]') AND name = 'NoteID')
    BEGIN ALTER TABLE [dbo].[FLRTReportLineItem] ADD [NoteID] [uniqueidentifier] NULL
          PRINT '  + NoteID' END

    -- Expand BalanceType to NVARCHAR(15) if still at old size
    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'FLRTReportLineItem' AND COLUMN_NAME = 'BalanceType'
                 AND CHARACTER_MAXIMUM_LENGTH < 15)
    BEGIN ALTER TABLE [dbo].[FLRTReportLineItem] ALTER COLUMN [BalanceType] NVARCHAR(15) NULL
          PRINT '  ~ BalanceType expanded to NVARCHAR(15)' END

    PRINT 'FLRTReportLineItem column check done.'
END
GO

-- =============================================
-- 6. FLRTPresentationGeneration (NEW - decoupled from FLRTFinancialReport)
-- =============================================
PRINT ''
PRINT '--- FLRTPresentationGeneration ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationGeneration]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTPresentationGeneration...'

    CREATE TABLE [dbo].[FLRTPresentationGeneration] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [PresentationID]         [int]              IDENTITY(1,1) NOT NULL,
        [PresentationCD]         [nvarchar](225)    NULL,
        [Description]            [nvarchar](50)     NULL,
        [CurrYear]               [nvarchar](4)      NULL,
        [FinancialMonth]         [nvarchar](2)      NULL DEFAULT('12'),
        [Branch]                 [nvarchar](10)     NULL,
        [Organization]           [nvarchar](50)     NULL,
        [Ledger]                 [nvarchar](20)     NULL,
        [PresentationTitle]      [nvarchar](500)    NULL,
        [PresentationDescription][nvarchar](2000)   NULL,
        [GammaTemplateId]        [nvarchar](100)    NULL,
        [SlideGeneratedFileID]   [uniqueidentifier] NULL,
        [PresentationMarkdown]   [nvarchar](max)    NULL,
        [SlideStatus]            [nvarchar](1)      NULL DEFAULT('N'),
        [CompanyNum]             [int]              NULL,
        [NoteID]                 [uniqueidentifier] NOT NULL DEFAULT(NEWID()),
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTPresentationGeneration] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [PresentationID] ASC)
    )

    PRINT 'FLRTPresentationGeneration created.'
END
ELSE
BEGIN
    PRINT 'FLRTPresentationGeneration already exists. Skipping.'
END
GO

-- =============================================
-- 7. FLRTPresentationDefinitionLink (NEW - links definitions to presentations)
-- =============================================
PRINT ''
PRINT '--- FLRTPresentationDefinitionLink ---'

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FLRTPresentationDefinitionLink]') AND type = N'U')
BEGIN
    PRINT 'Creating FLRTPresentationDefinitionLink...'

    CREATE TABLE [dbo].[FLRTPresentationDefinitionLink] (
        [CompanyID]              [int]              NOT NULL DEFAULT(0),
        [LinkID]                 [int]              IDENTITY(1,1) NOT NULL,
        [PresentationID]         [int]              NULL,
        [DefinitionID]           [int]              NULL,
        [DisplayOrder]           [int]              NULL DEFAULT(0),
        [CreatedDateTime]        [datetime]         NOT NULL DEFAULT(GETDATE()),
        [CreatedByID]            [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [CreatedByScreenID]      [char](8)          NOT NULL DEFAULT('        '),
        [LastModifiedDateTime]   [datetime]         NOT NULL DEFAULT(GETDATE()),
        [LastModifiedByID]       [uniqueidentifier] NOT NULL DEFAULT('00000000-0000-0000-0000-000000000000'),
        [LastModifiedByScreenID] [char](8)          NOT NULL DEFAULT('        '),
        [tstamp]                 [timestamp]        NOT NULL,

        CONSTRAINT [PK_FLRTPresentationDefinitionLink] PRIMARY KEY CLUSTERED ([CompanyID] ASC, [LinkID] ASC)
    )

    PRINT 'FLRTPresentationDefinitionLink created.'
END
ELSE
BEGIN
    PRINT 'FLRTPresentationDefinitionLink already exists. Skipping.'
END
GO

PRINT ''
PRINT '======================================================'
PRINT 'Deployment complete.'
PRINT '======================================================'
