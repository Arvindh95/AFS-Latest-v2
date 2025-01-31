using PX.Data;
using System.Collections;
using PX.Objects.GL.FinPeriods;
using PX.Objects.GL.FinPeriods.TableDefinition;
using System;
using System.Collections.Generic;
using PX.Objects;
using PX.Objects.GL;

namespace FinancialReport
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
    public class AccountHistoryBySubEnq_Extension : PXGraphExtension<PX.Objects.GL.AccountHistoryBySubEnq>
    {
         public override void Initialize()
        {
            base.Initialize();
        }

        /// <summary>
        /// Calculates and displays sum of the FinYtdBalance in the custom filter field.
        /// </summary>
        protected void GLHistoryEnqFilter_RowSelected(PXCache cache, PXRowSelectedEventArgs e)
        {
            GLHistoryEnqFilter row = (GLHistoryEnqFilter)e.Row;
            if (row == null) return;

            decimal? sum = 0m;

            // Loop through all rows in the EnqResult view
            foreach (GLHistoryEnquiryResult result in Base.EnqResult.Select())
            {
                // Replace FinYtdBalance with whichever field you actually want to sum
                sum += result.EndBalance ?? 0m;
            }

            // Update the custom field on the filter extension
            GLHistoryEnqFilterExt rowExt = cache.GetExtension<GLHistoryEnqFilterExt>(row);
            rowExt.UsrSumEndingBalance = sum;
        }
    }
}