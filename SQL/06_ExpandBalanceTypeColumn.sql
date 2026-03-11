-- Migration 06: Expand FLRTReportLineItem.BalanceType column from varchar(10) to varchar(15)
-- Required because JANBEGINNING (12 chars) and new PDEBIT/PCREDIT/PMOVEMENT types exceed the original length.
-- Run once against the target Acumatica company database.

IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'FLRTReportLineItem'
      AND COLUMN_NAME = 'BalanceType'
      AND CHARACTER_MAXIMUM_LENGTH < 15
)
BEGIN
    ALTER TABLE [dbo].[FLRTReportLineItem]
        ALTER COLUMN [BalanceType] NVARCHAR(15) NULL;

    PRINT 'FLRTReportLineItem.BalanceType expanded to NVARCHAR(15).';
END
ELSE
BEGIN
    PRINT 'FLRTReportLineItem.BalanceType already NVARCHAR(15) or wider — no change needed.';
END
