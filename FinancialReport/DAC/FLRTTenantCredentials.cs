using PX.Data;
using System;

namespace FinancialReport
{
    [Serializable]
    [PXCacheName("Tenant Credentials")]
    public class FLRTTenantCredentials : PXBqlTable, IBqlTable
    {
        #region CompanyNum
        [PXDBInt(IsKey = true)]
        [PXUIField(DisplayName = "Company Number")]
        public virtual int? CompanyNum { get; set; }
        public abstract class companyNum : PX.Data.BQL.BqlInt.Field<companyNum> { }
        #endregion
          
        #region TenantName
        [PXDBString(50, IsUnicode = true)]
        [PXUIField(DisplayName = "Tenant Name")]
        public virtual string TenantName { get; set; }
        public abstract class tenantName : PX.Data.BQL.BqlString.Field<tenantName> { }
        #endregion

        #region ClientID
        [PXDBBinary]
        [PXUIField(DisplayName = "Client ID")]
        public virtual byte[] ClientID { get; set; }
        public abstract class clientID : PX.Data.BQL.BqlByteArray.Field<clientID> { }
        #endregion

        #region SecretID
        [PXDBBinary]
        [PXUIField(DisplayName = "Client Secret")]
        public virtual byte[] SecretID { get; set; }
        public abstract class secretID : PX.Data.BQL.BqlByteArray.Field<secretID> { }
        #endregion

        #region Username
        [PXDBBinary]
        [PXUIField(DisplayName = "Username")]
        public virtual byte[] Username { get; set; }
        public abstract class username : PX.Data.BQL.BqlByteArray.Field<username> { }
        #endregion

        #region Password
        [PXDBBinary]
        [PXUIField(DisplayName = "Password")]
        public virtual byte[] Password { get; set; }
        public abstract class password : PX.Data.BQL.BqlByteArray.Field<password> { }
        #endregion

        #region CreatedDateTime
        [PXDBDateAndTime]
        [PXDefault(typeof(AccessInfo.businessDate))]
        [PXUIField(DisplayName = "Created Date Time", Enabled = false)]
        public virtual DateTime? CreatedDateTime { get; set; }
        public abstract class createdDateTime : PX.Data.BQL.BqlDateTime.Field<createdDateTime> { }
        #endregion

        #region CreatedByID
        [PXDBCreatedByID]
        public virtual Guid? CreatedByID { get; set; }
        public abstract class createdByID : PX.Data.BQL.BqlGuid.Field<createdByID> { }
        #endregion

        #region CreatedByScreenID
        [PXDBCreatedByScreenID]
        public virtual string CreatedByScreenID { get; set; }
        public abstract class createdByScreenID : PX.Data.BQL.BqlString.Field<createdByScreenID> { }
        #endregion

        #region LastModifiedDateTime
        [PXDBDateAndTime]
        [PXDefault(typeof(AccessInfo.businessDate))]
        [PXUIField(DisplayName = "Last Modified Date Time", Enabled = false)]
        public virtual DateTime? LastModifiedDateTime { get; set; }
        public abstract class lastModifiedDateTime : PX.Data.BQL.BqlDateTime.Field<lastModifiedDateTime> { }
        #endregion

        #region LastModifiedByID
        [PXDBLastModifiedByID]
        public virtual Guid? LastModifiedByID { get; set; }
        public abstract class lastModifiedByID : PX.Data.BQL.BqlGuid.Field<lastModifiedByID> { }
        #endregion

        #region LastModifiedByScreenID
        [PXDBLastModifiedByScreenID]
        public virtual string LastModifiedByScreenID { get; set; }
        public abstract class lastModifiedByScreenID : PX.Data.BQL.BqlString.Field<lastModifiedByScreenID> { }
        #endregion

        #region Tstamp
        [PXDBTimestamp]
        public virtual byte[] Tstamp { get; set; }
        public abstract class tstamp : PX.Data.BQL.BqlByteArray.Field<tstamp> { }
        #endregion

        #region NoteID
        [PXNote]
        public virtual Guid? NoteID { get; set; }
        public abstract class noteID : PX.Data.BQL.BqlGuid.Field<noteID> { }
        #endregion
    }
}