using System;
using PX.Data;
using PX.Objects.GL; // For Ledger and Branch tables
using PX.Objects;
using PX.Objects.GL.FinPeriods.TableDefinition;
using PX.Data.BQL.Fluent;

namespace FinancialReport
{
  [Serializable]
  [PXCacheName("FLRTFinancialReport")]
  public class FLRTFinancialReport : PXBqlTable, IBqlTable
  {
    #region ReportID
    [PXDBIdentity(IsKey = true)]
    public virtual int? ReportID { get; set; }
    public abstract class reportID : PX.Data.BQL.BqlInt.Field<reportID> { }
    #endregion

    #region ReportCD
    [PXDBString(15, IsUnicode = true, InputMask = ">aaaaaaaaaaaaaaa")]      
    [PXDefault]
    [PXUIField(DisplayName = "Template Name")]
    public virtual string ReportCD { get; set; }
    public abstract class reportCD : PX.Data.BQL.BqlString.Field<reportCD> { }       
    #endregion

    #region Description
    [PXDBString(50, IsUnicode = true, InputMask = "")]
    [PXUIField(DisplayName = "Description")]
    public virtual string Description { get; set; }
    public abstract class description : PX.Data.BQL.BqlString.Field<description> { }
    #endregion

    #region CreatedDateTime
    [PXDBCreatedDateTime()]
    public virtual DateTime? CreatedDateTime { get; set; }
    public abstract class createdDateTime : PX.Data.BQL.BqlDateTime.Field<createdDateTime> { }
    #endregion

    #region CreatedByID
    [PXDBCreatedByID()]
    public virtual Guid? CreatedByID { get; set; }
    public abstract class createdByID : PX.Data.BQL.BqlGuid.Field<createdByID> { }
    #endregion

    #region CreatedByScreenID
    [PXDBCreatedByScreenID()]
    public virtual string CreatedByScreenID { get; set; }
    public abstract class createdByScreenID : PX.Data.BQL.BqlString.Field<createdByScreenID> { }
    #endregion

    #region LastModifiedDateTime
    [PXDBLastModifiedDateTime()]
    public virtual DateTime? LastModifiedDateTime { get; set; }
    public abstract class lastModifiedDateTime : PX.Data.BQL.BqlDateTime.Field<lastModifiedDateTime> { }
    #endregion

    #region LastModifiedByID
    [PXDBLastModifiedByID()]
    public virtual Guid? LastModifiedByID { get; set; }
    public abstract class lastModifiedByID : PX.Data.BQL.BqlGuid.Field<lastModifiedByID> { }
    #endregion

    #region LastModifiedByScreenID
    [PXDBLastModifiedByScreenID()]
    public virtual string LastModifiedByScreenID { get; set; }
    public abstract class lastModifiedByScreenID : PX.Data.BQL.BqlString.Field<lastModifiedByScreenID> { }
    #endregion

    #region Tstamp
    [PXDBTimestamp()]
    [PXUIField()]
    public virtual byte[] Tstamp { get; set; }
    public abstract class tstamp : PX.Data.BQL.BqlByteArray.Field<tstamp> { }
    #endregion

    #region Noteid
    [PXNote()]
    public virtual Guid? Noteid { get; set; }
    public abstract class noteid : PX.Data.BQL.BqlGuid.Field<noteid> { }
        #endregion

    #region Select 
    [PXBool]
    [PXUnboundDefault(false)]
    [PXUIField(DisplayName = "Select")]
    public virtual bool? Selected { get; set; }
    public abstract class selected : PX.Data.BQL.BqlBool.Field<selected> { }
        #endregion

    #region Current Year
    [PXDBString(4, IsUnicode = true)]
    [PXUIField(DisplayName = "Current Year")]
    [PXSelector(typeof(SelectFrom<FinPeriod>
                .AggregateTo<GroupBy<FinPeriod.finYear>>       // distinct
                .OrderBy<FinPeriod.finYear.Desc>               // descending
                .SearchFor<FinPeriod.finYear>),                // the field you want
        new Type[] { typeof(FinPeriod.finYear) },
        DescriptionField = typeof(FinPeriod.finYear))]
    public virtual string CurrYear { get; set; }
    public abstract class currYear : PX.Data.BQL.BqlString.Field<currYear> { }
    #endregion

    #region Branch
    [PXDBString(10, IsUnicode = true)]
    [PXUIField(DisplayName = "Branch")]
    [PXSelector(typeof(Search<Branch.branchCD>))]
    public virtual string Branch { get; set; }
    public abstract class branch : PX.Data.BQL.BqlString.Field<branch> { }
    #endregion

    #region Ledger
    [PXDBString(20, IsUnicode = true)]
    [PXUIField(DisplayName = "Ledger")]
    [PXSelector(typeof(Search<Ledger.ledgerCD>), typeof(Ledger.descr))]
    public virtual string Ledger { get; set; }
    public abstract class ledger : PX.Data.BQL.BqlString.Field<ledger> { }
    #endregion

    #region FinancialMonth
    [PXDBString(2, IsFixed = true)]
    [PXDefault("12")] // Default to December
    [PXUIField(DisplayName = "Financial Month")]
    [PXStringList(new string[]{"01", "02", "03", "04", "05", "06","07", "08", "09", "10", "11", "12"},new string[]{"January", "February", "March", "April", "May", "June","July", "August", "September", "October", "November", "December"})]
    public virtual string FinancialMonth { get; set; }
    public abstract class financialMonth : PX.Data.BQL.BqlString.Field<financialMonth> { }
    #endregion

    #region GeneratedFileID
    [PXDBGuid]
    [PXUIField(DisplayName = "Generated File ID", Visible = true)]
    public virtual Guid? GeneratedFileID { get; set; }
    public abstract class generatedFileID : PX.Data.BQL.BqlGuid.Field<generatedFileID> { }
    #endregion

    }
}