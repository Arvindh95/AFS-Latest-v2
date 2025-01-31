using PX.Data;
using PX.Objects.CM;
using PX.Objects.CS;
using PX.Objects.GL.FinPeriods.TableDefinition;
using PX.Objects.GL.FinPeriods;
using PX.Objects.GL;
using PX.Objects;
using System.Collections.Generic;
using System.Collections;
using System.Text.RegularExpressions;
using System;

namespace FinancialReport
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
    public class GLHistoryEnquiryResultExt : PXCacheExtension<PX.Objects.GL.GLHistoryEnquiryResult>
  {
       #region SumEndingBalance
        [PXDecimal]
        [PXUIField(DisplayName = "Sum Ending Balance")]
        public virtual decimal? SumEndingBalance { get; set; }
        public abstract class sumEndingBalance : PX.Data.BQL.BqlDecimal.Field<sumEndingBalance> { }
        #endregion
  }
}