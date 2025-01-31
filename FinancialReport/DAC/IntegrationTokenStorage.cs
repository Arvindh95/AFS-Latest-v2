using System;
using PX.Data;

namespace FinancialReport
{
  [Serializable]
  [PXCacheName("IntegrationTokenStorage")]
  public class IntegrationTokenStorage : PXBqlTable, IBqlTable
  {
    #region TokenType
    [PXDBString(50, InputMask = "")]
    [PXUIField(DisplayName = "Token Type")]
    public virtual string TokenType { get; set; }
    public abstract class tokenType : PX.Data.BQL.BqlString.Field<tokenType> { }
    #endregion

    #region AccessToken
    [PXDBString(2000, InputMask = "")]
    [PXUIField(DisplayName = "Access Token")]
    public virtual string AccessToken { get; set; }
    public abstract class accessToken : PX.Data.BQL.BqlString.Field<accessToken> { }
    #endregion

    #region RefreshToken
    [PXDBString(2000, InputMask = "")]
    [PXUIField(DisplayName = "Refresh Token")]
    public virtual string RefreshToken { get; set; }
    public abstract class refreshToken : PX.Data.BQL.BqlString.Field<refreshToken> { }
    #endregion

    #region ExpiresAt
    [PXDBDate()]
    [PXUIField(DisplayName = "Expires At")]
    public virtual DateTime? ExpiresAt { get; set; }
    public abstract class expiresAt : PX.Data.BQL.BqlDateTime.Field<expiresAt> { }
    #endregion

    #region Tstamp
    [PXDBTimestamp()]
    [PXUIField(DisplayName = "Tstamp")]
    public virtual byte[] Tstamp { get; set; }
    public abstract class tstamp : PX.Data.BQL.BqlByteArray.Field<tstamp> { }
    #endregion
  }
}