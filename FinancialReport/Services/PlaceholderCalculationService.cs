using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Drawing.Charts;
using PX.Common;
using PX.Data;

namespace FinancialReport.Services
{
    public class PlaceholderCalculationService
    {
        public interface IPlaceholderCalculator
        {
            Dictionary<string, string> CalculatePlaceholders(FinancialApiData cyData,FinancialApiData pyData,Dictionary<string, string> basePlaceholders);
            Dictionary<string, string> CalculateCompositePlaceholders(FinancialApiData cyData, FinancialApiData pyData, Dictionary<string, string> basePlaceholders);
        }

        public class IYRESPlaceholderCalculator : IPlaceholderCalculator
        {
            public Dictionary<string, string> CalculatePlaceholders(FinancialApiData cyData,FinancialApiData pyData,Dictionary<string, string> basePlaceholders)
            {
                PXTrace.WriteInformation("IYRES.CalculatePlaceholders() - Handling Account Level logic.");

                // Call your IYRES-specific methods.
                basePlaceholders = Susutnilai_Loji_dan_Peralatan(basePlaceholders);
                basePlaceholders = Lebihan_Kurangan_Sebelum_Cukai(basePlaceholders);
                basePlaceholders = Pelunasan_Aset_Tak_Ketara(basePlaceholders);
                basePlaceholders = Lebihan_Terkumpul(basePlaceholders);
                basePlaceholders = Emolumen(basePlaceholders);
                basePlaceholders = Manfaat_Pekerja(basePlaceholders);
                basePlaceholders = Perkhidmatan_Ikhtisas_DLL(basePlaceholders);
                basePlaceholders = Perbelanjaan_Kajian_Dan_Program(basePlaceholders);
                basePlaceholders = Baki1JanPenyataPerubahanAsetBersih(basePlaceholders);
                basePlaceholders = Perubahan_Bersih_Pelbagai_Penghutang_Deposit(basePlaceholders);
                basePlaceholders = Perubahan_Bersih_Pelbagai_Pemiutang_Akruan(basePlaceholders);
                basePlaceholders = Perubahan_Bersih_Akaun_Khas(basePlaceholders);
                basePlaceholders = Perubahan_Bersih_Geran_Pembangunan(basePlaceholders);
                basePlaceholders = Penambahan_Loji_Peralatan(basePlaceholders);
                basePlaceholders = Penerimaan_Pelupusan_Loji_Peralatan(basePlaceholders);
                basePlaceholders = Penerimaan_Daripada_Pengeluaran_Simpanan_Tetap(basePlaceholders);
                basePlaceholders = Faedah_Atas_Pelaburan_Diterima(basePlaceholders);
                basePlaceholders = Geran_Pembangunan_Dilunaskan(basePlaceholders);
                basePlaceholders = Lebihan_Terkumpul_Aset_Bersih(basePlaceholders);
                basePlaceholders = Cukai(basePlaceholders);
                basePlaceholders = NegatePlaceholders(basePlaceholders, new Dictionary<string, string>
                {
                    //5. Faedah Atas Pelaburan
                    { "{{Sum3_H75_CY}}", "{{5_CY}}" },
                    { "{{Sum3_H75_PY}}", "{{5_PY}}" },

                    //21. Akaun Khas Dilunaskan
                    { "{{H83303_CY}}", "{{21_CY}}" },
                    { "{{H83303_PY}}", "{{21_PY}}" },

                    //22. Keuntungan Pelupusan Loji dan Peralatan
                    { "{{H79101_CY}}", "{{22_CY}}" },
                    { "{{H79101_PY}}", "{{22_PY}}" },

                    //23. Jumlah Hasil
                    { "{{Sum1_H_CY}}", "{{23_CY}}" },
                    { "{{Sum1_H_PY}}", "{{23_PY}}" },

                    //25. Kumpulan Wang Komputer
                    { "{{E14102_CY}}", "{{25_CY}}" },
                    { "{{E14102_PY}}", "{{25_PY}}" }
                });
                return basePlaceholders;
            }

            public Dictionary<string, string> CalculateCompositePlaceholders(FinancialApiData cyData, FinancialApiData pyData, Dictionary<string, string> basePlaceholders)
            {
                PXTrace.WriteInformation("IYRES.CalculateCompositePlaceholders() - Handling composite logic.");

                basePlaceholders = XTest(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);
                return basePlaceholders;
            }


            #region Calculation Methods


            #region aa. Test

            public Dictionary<string, string> XTest(Dictionary<string, string> placeholders, Dictionary<string, FinancialPeriodData> cyComposite, Dictionary<string, FinancialPeriodData> pyComposite)
            {
                string accountId = "A11101".Trim();
                string subaccountId = "M-H-100-0".Trim();
                string branchId = "HQ".Trim();
                string orgId = "HQ".Trim();

                string month = placeholders.ContainsKey("{{MonthNumber}}") ? placeholders["{{MonthNumber}}"] : null;
                string yearCY = placeholders.ContainsKey("{{CY}}") ? placeholders["{{CY}}"] : null;
                string yearPY = placeholders.ContainsKey("{{PY}}") ? placeholders["{{PY}}"] : null;

                PXTrace.WriteInformation($"MonthNumber: {month}, CY: {yearCY}, PY: {yearPY}");

                //Later validation or handling if null
                if (string.IsNullOrWhiteSpace(month) || string.IsNullOrWhiteSpace(yearCY) || string.IsNullOrWhiteSpace(yearPY))
                    {
                        throw new PXException(Messages.NoValueMapping);
                    }

                string periodCY = month + yearCY;
                string periodPY = month + yearPY;

                string compositeKeyCY = $"{accountId}-{subaccountId}-{branchId}-{orgId}-{periodCY}";
                string compositeKeyPY = $"{accountId}-{subaccountId}-{branchId}-{orgId}-{periodPY}";

                // 🔍 Trace for debugging key mismatch
                PXTrace.WriteInformation($"[Debug] Expected CY key: {compositeKeyCY}");
                PXTrace.WriteInformation($"[Debug] Expected PY key: {compositeKeyPY}");

                PXTrace.WriteInformation($"cyComposite.Keys.Count: {cyComposite?.Count ?? 0}");
                foreach (var key in cyComposite.Keys)
                {
                    PXTrace.WriteInformation($"[Debug] CY Available key: {key}");
                }

                PXTrace.WriteInformation($"pyComposite.Keys.Count: {pyComposite?.Count ?? 0}");
                foreach (var key in pyComposite.Keys)
                {
                    PXTrace.WriteInformation($"[Debug] PY Available key: {key}");
                }

                PXTrace.WriteInformation($"Trying to match CY key: {compositeKeyCY}");
                PXTrace.WriteInformation($"Trying to match PY key: {compositeKeyPY}");

                PXTrace.WriteInformation($"cyComposite.Keys.Count: {cyComposite?.Count ?? 0}");
                PXTrace.WriteInformation($"pyComposite.Keys.Count: {pyComposite?.Count ?? 0}");

                foreach (var key in cyComposite.Keys)
                {
                    PXTrace.WriteInformation($"CY Key in Dictionary: {key}");
                }

                foreach (var key in pyComposite.Keys)
                {
                    PXTrace.WriteInformation($"PY Key in Dictionary: {key}");
                }

                if (cyComposite.TryGetValue(compositeKeyCY, out var cy))
                {
                    placeholders["{{X_A11101_CY}}"] = cy.EndingBalance.ToString("#,##0");
                    PXTrace.WriteInformation($"[CY] Matched Key: {compositeKeyCY}");
                    PXTrace.WriteInformation($"[CY] EndingBalance: {cy.EndingBalance}, Debit: {cy.Debit}, Credit: {cy.Credit}, Description: {cy.Description}");
                }
                else
                {
                    PXTrace.WriteWarning($"[CY] Key not found: {compositeKeyCY}");
                }

                if (pyComposite.TryGetValue(compositeKeyPY, out var py))
                {
                    placeholders["{{X_A11101_PY}}"] = py.EndingBalance.ToString("#,##0");
                    PXTrace.WriteInformation($"[PY] Matched Key: {compositeKeyPY}");
                    PXTrace.WriteInformation($"[PY] EndingBalance: {py.EndingBalance}, Debit: {py.Debit}, Credit: {py.Credit}, Description: {py.Description}");
                }
                else
                {
                    PXTrace.WriteWarning($"[PY] Key not found: {compositeKeyPY}");
                }

                return placeholders;
            }

            #endregion


            #region 1. Susutnilai Loji dan Peralatan
            public Dictionary<string, string> Susutnilai_Loji_dan_Peralatan(Dictionary<string, string> placeholders)
            {
                // 1. Parse placeholders
                decimal debitSumB53_CY = placeholders.ContainsKey("{{DebitSum3_B53_CY}}") ? decimal.Parse(placeholders["{{DebitSum3_B53_CY}}"]) : 0;
                decimal creditSumB53_CY = placeholders.ContainsKey("{{CreditSum3_B53_CY}}") ? decimal.Parse(placeholders["{{CreditSum3_B53_CY}}"]) : 0;
                decimal debitSumB539_CY = placeholders.ContainsKey("{{DebitSum4_B539_CY}}") ? decimal.Parse(placeholders["{{DebitSum4_B539_CY}}"]) : 0;
                decimal creditSumB539_CY = placeholders.ContainsKey("{{CreditSum4_B539_CY}}") ? decimal.Parse(placeholders["{{CreditSum4_B539_CY}}"]) : 0;
                decimal debitSumB53_PY = placeholders.ContainsKey("{{DebitSum3_B53_PY}}") ? decimal.Parse(placeholders["{{DebitSum3_B53_PY}}"]) : 0;
                decimal creditSumB53_PY = placeholders.ContainsKey("{{CreditSum3_B53_PY}}") ? decimal.Parse(placeholders["{{CreditSum3_B53_PY}}"]) : 0;
                decimal debitSumB539_PY = placeholders.ContainsKey("{{DebitSum4_B539_PY}}") ? decimal.Parse(placeholders["{{DebitSum4_B539_PY}}"]) : 0;
                decimal creditSumB539_PY = placeholders.ContainsKey("{{CreditSum4_B539_PY}}") ? decimal.Parse(placeholders["{{CreditSum4_B539_PY}}"]) : 0;


                // 2. Compute formula for CY
                decimal resultCY = (debitSumB53_CY - creditSumB53_CY) - (debitSumB539_CY - creditSumB539_CY);

                // Store in a new placeholder
                placeholders["{{1_CY}}"] = resultCY.ToString("#,##0");

                // 3. Compute formula for PY
                decimal resultPY = (debitSumB53_PY - creditSumB53_PY) - (debitSumB539_PY - creditSumB539_PY);

                // Store in a new placeholder
                placeholders["{{1_PY}}"] = resultPY.ToString("#,##0");

                // 5. Return the updated placeholders dictionary
                return placeholders;
            }
            #endregion

            #region 2. Lebihan/(Kurangan) Sebelum Cukai
            public Dictionary<string, string> Lebihan_Kurangan_Sebelum_Cukai(Dictionary<string, string> placeholders)
            {
                // 1. Parse placeholders for CY
                decimal sum1HCY = placeholders.ContainsKey("{{Sum1_H_CY}}") ? decimal.Parse(placeholders["{{Sum1_H_CY}}"]) : 0;
                decimal sum1BCY = placeholders.ContainsKey("{{Sum1_B_CY}}") ? decimal.Parse(placeholders["{{Sum1_B_CY}}"]) : 0;
                decimal sum3B50CY = placeholders.ContainsKey("{{Sum3_B50_CY}}") ? decimal.Parse(placeholders["{{Sum3_B50_CY}}"]) : 0;
                decimal sum3B59CY = placeholders.ContainsKey("{{Sum3_B59_CY}}") ? decimal.Parse(placeholders["{{Sum3_B59_CY}}"]) : 0;

                // 2. Compute formula for CY
                //    (Sum1_H_CY * -1) - Sum1_B_CY + Sum3_B50_CY + Sum3_B59_CY
                decimal resultCY = (sum1HCY * -1) - sum1BCY + sum3B50CY + sum3B59CY;

                // Store in a new placeholder
                placeholders["{{2_CY}}"] = resultCY.ToString("#,##0");


                // 3. Parse placeholders for PY
                decimal sum1HPY = placeholders.ContainsKey("{{Sum1_H_PY}}") ? decimal.Parse(placeholders["{{Sum1_H_PY}}"]) : 0;
                decimal sum1BPY = placeholders.ContainsKey("{{Sum1_B_PY}}") ? decimal.Parse(placeholders["{{Sum1_B_PY}}"]) : 0;
                decimal sum3B50PY = placeholders.ContainsKey("{{Sum3_B50_PY}}") ? decimal.Parse(placeholders["{{Sum3_B50_PY}}"]) : 0;
                decimal sum3B59PY = placeholders.ContainsKey("{{Sum3_B59_PY}}") ? decimal.Parse(placeholders["{{Sum3_B59_PY}}"]) : 0;


                // 4. Compute formula for PY
                //    (Sum1_H_PY * -1) - Sum1_B_PY + Sum3_B50_PY + Sum3_B59_PY
                decimal resultPY = (sum1HPY * -1) - sum1BPY + sum3B50PY + sum3B59PY;

                // Store in a new placeholder
                placeholders["{{2_PY}}"] = resultPY.ToString("#,##0");

                // 5. Return the updated placeholders dictionary
                return placeholders;
            }
            #endregion

            #region 3. Pelunasan Aset Tak Ketara
            public Dictionary<string, string> Pelunasan_Aset_Tak_Ketara(Dictionary<string, string> placeholders)
            {
                // 1. Parse the CY placeholders (if missing or invalid, default to 0)
                decimal debitSumB5390CY = placeholders.ContainsKey("{{DebitSum5_B5390_CY}}")
                    ? decimal.Parse(placeholders["{{DebitSum5_B5390_CY}}"])
                    : 0;
                decimal creditSumB5390CY = placeholders.ContainsKey("{{CreditSum5_B5390_CY}}")
                    ? decimal.Parse(placeholders["{{CreditSum5_B5390_CY}}"])
                    : 0;

                // 2. Compute CY difference
                decimal netB5390CY = debitSumB5390CY - creditSumB5390CY;
                placeholders["{{3_CY}}"] = netB5390CY.ToString("#,##0");


                // 3. Parse the PY placeholders
                decimal debitSumB5390PY = placeholders.ContainsKey("{{DebitSum5_B5390_PY}}")
                    ? decimal.Parse(placeholders["{{DebitSum5_B5390_PY}}"])
                    : 0;
                decimal creditSumB5390PY = placeholders.ContainsKey("{{CreditSum5_B5390_PY}}")
                    ? decimal.Parse(placeholders["{{CreditSum5_B5390_PY}}"])
                    : 0;

                // 4. Compute PY difference
                decimal netB5390PY = debitSumB5390PY - creditSumB5390PY;
                placeholders["{{3_PY}}"] = netB5390PY.ToString("#,##0");

                // 5. Return the updated placeholders
                return placeholders;
            }

            #endregion

            #region 4. Lebihan Terkumpul 
            public Dictionary<string, string> Lebihan_Terkumpul(Dictionary<string, string> placeholders)
            {
                // Define all the CY keys you want to sum
                var cyKeys = new[]
                {
                    "{{E13101_CY}}",
                    "{{E13102_CY}}",
                    "{{E13201_CY}}",
                    "{{E14101_CY}}",
                    "{{Sum1_B_CY}}",
                    "{{Sum1_H_CY}}"
                };

                decimal totalCY = 0m;
                foreach (var key in cyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalCY += value;
                    }
                }
                placeholders["{{4_CY}}"] = totalCY.ToString("#,##0");


                // Define all the PY keys (same pattern as above, just _PY)
                var pyKeys = new[]
                {
                    "{{E13101_PY}}",
                    "{{E13102_PY}}",
                    "{{E13201_PY}}",
                    "{{E14101_PY}}",
                    "{{Sum1_B_PY}}",
                    "{{Sum1_H_PY}}"
                };

                decimal totalPY = 0m;
                foreach (var key in pyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalPY += value;
                    }
                }
                placeholders["{{4_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region Commented out code
            //#region 5. Faedah Atas Pelaburan

            //public Dictionary<string, string> Faedah_Atas_Pelaburan(Dictionary<string, string> placeholders)
            //{
            //    // --- CURRENT YEAR (CY) ---
            //    if (placeholders.ContainsKey("{{Sum3_H75_CY}}") &&
            //        decimal.TryParse(placeholders["{{Sum3_H75_CY}}"], out decimal sumCY))
            //    {
            //        placeholders["{{5_CY}}"] = (sumCY * -1).ToString("#,##0");
            //    }
            //    else
            //    {
            //        placeholders["{{5_CY}}"] = "0";
            //    }

            //    // --- PREVIOUS YEAR (PY) ---
            //    if (placeholders.ContainsKey("{{Sum3_H75_PY}}") &&
            //        decimal.TryParse(placeholders["{{Sum3_H75_PY}}"], out decimal sumPY))
            //    {
            //        placeholders["{{5_PY}}"] = (sumPY * -1).ToString("#,##0");
            //    }
            //    else
            //    {
            //        placeholders["{{5_PY}}"] = "0";
            //    }

            //    return placeholders;
            //}


            //#endregion
            #endregion

            #region 6. Emolumen
            public Dictionary<string, string> Emolumen(Dictionary<string, string> placeholders)
            {
                // ----- CURRENT YEAR (CY) -----
                decimal sum2B1CY = placeholders.ContainsKey("{{Sum2_B1_CY}}") && decimal.TryParse(placeholders["{{Sum2_B1_CY}}"], out var val1CY)
                    ? val1CY : 0;

                decimal b13501CY = placeholders.ContainsKey("{{B13501_CY}}") && decimal.TryParse(placeholders["{{B13501_CY}}"], out var val2CY)
                    ? val2CY : 0;

                decimal netCY = sum2B1CY - b13501CY;
                placeholders["{{6_CY}}"] = netCY.ToString("#,##0");


                // ----- PREVIOUS YEAR (PY) -----
                decimal sum2B1PY = placeholders.ContainsKey("{{Sum2_B1_PY}}") && decimal.TryParse(placeholders["{{Sum2_B1_PY}}"], out var val1PY)
                    ? val1PY : 0;

                decimal b13501PY = placeholders.ContainsKey("{{B13501_PY}}") && decimal.TryParse(placeholders["{{B13501_PY}}"], out var val2PY)
                    ? val2PY : 0;

                decimal netPY = sum2B1PY - b13501PY;
                placeholders["{{6_PY}}"] = netPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 7. Manfaat Pekerja

            public Dictionary<string, string> Manfaat_Pekerja(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal debitCY = placeholders.ContainsKey("{{L18102_debit_CY}}") && decimal.TryParse(placeholders["{{L18102_debit_CY}}"], out var dCY)
                    ? dCY : 0;

                decimal creditCY = placeholders.ContainsKey("{{L18102_credit_CY}}") && decimal.TryParse(placeholders["{{L18102_credit_CY}}"], out var cCY)
                    ? cCY : 0;

                decimal netCY = (debitCY - creditCY) * -1;
                placeholders["{{7_CY}}"] = netCY.ToString("#,##0");


                // ---------- PY ----------
                decimal debitPY = placeholders.ContainsKey("{{L18102_debit_PY}}") && decimal.TryParse(placeholders["{{L18102_debit_PY}}"], out var dPY)
                    ? dPY : 0;

                decimal creditPY = placeholders.ContainsKey("{{L18102_credit_PY}}") && decimal.TryParse(placeholders["{{L18102_credit_PY}}"], out var cPY)
                    ? cPY : 0;

                decimal netPY = (debitPY - creditPY) * -1;
                placeholders["{{7_PY}}"] = netPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 8. Perkhidmatan Ikhtisas dan Lain – Lain
            public Dictionary<string, string> Perkhidmatan_Ikhtisas_DLL(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal b29CY = placeholders.ContainsKey("{{Sum3_B29_CY}}") && decimal.TryParse(placeholders["{{Sum3_B29_CY}}"], out var v1CY) ? v1CY : 0;
                decimal b62CY = placeholders.ContainsKey("{{Sum3_B62_CY}}") && decimal.TryParse(placeholders["{{Sum3_B62_CY}}"], out var v2CY) ? v2CY : 0;
                decimal b5399CY = placeholders.ContainsKey("{{Sum5_B5399_CY}}") && decimal.TryParse(placeholders["{{Sum5_B5399_CY}}"], out var v3CY) ? v3CY : 0;
                decimal b2940CY = placeholders.ContainsKey("{{Sum5_B2940_CY}}") && decimal.TryParse(placeholders["{{Sum5_B2940_CY}}"], out var v4CY) ? v4CY : 0;

                decimal netCY = b29CY + b62CY + b5399CY - b2940CY;
                placeholders["{{8_CY}}"] = netCY.ToString("#,##0");

                // ---------- PY ----------
                decimal b29PY = placeholders.ContainsKey("{{Sum3_B29_PY}}") && decimal.TryParse(placeholders["{{Sum3_B29_PY}}"], out var v1PY) ? v1PY : 0;
                decimal b62PY = placeholders.ContainsKey("{{Sum3_B62_PY}}") && decimal.TryParse(placeholders["{{Sum3_B62_PY}}"], out var v2PY) ? v2PY : 0;
                decimal b5399PY = placeholders.ContainsKey("{{Sum5_B5399_PY}}") && decimal.TryParse(placeholders["{{Sum5_B5399_PY}}"], out var v3PY) ? v3PY : 0;
                decimal b2940PY = placeholders.ContainsKey("{{Sum5_B2940_PY}}") && decimal.TryParse(placeholders["{{Sum5_B2940_PY}}"], out var v4PY) ? v4PY : 0;

                decimal netPY = b29PY + b62PY + b5399PY - b2940PY;
                placeholders["{{8_PY}}"] = netPY.ToString("#,##0");

                return placeholders;
            }
            #endregion

            #region 9. Perbelanjaan Kajian Dan Program

            public Dictionary<string, string> Perbelanjaan_Kajian_Dan_Program(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal b42CY = placeholders.ContainsKey("{{Sum3_B42_CY}}") && decimal.TryParse(placeholders["{{Sum3_B42_CY}}"], out var v1CY) ? v1CY : 0;
                decimal b2940CY = placeholders.ContainsKey("{{Sum5_B2940_CY}}") && decimal.TryParse(placeholders["{{Sum5_B2940_CY}}"], out var v2CY) ? v2CY : 0;
                decimal b41CY = placeholders.ContainsKey("{{Sum3_B41_CY}}") && decimal.TryParse(placeholders["{{Sum3_B41_CY}}"], out var v3CY) ? v3CY : 0;
                decimal b52100CY = placeholders.ContainsKey("{{B52100_CY}}") && decimal.TryParse(placeholders["{{B52100_CY}}"], out var v4CY) ? v4CY : 0;

                decimal netCY = b42CY + b2940CY + b41CY + b52100CY;
                placeholders["{{9_CY}}"] = netCY.ToString("#,##0");

                // ---------- PY ----------
                decimal b42PY = placeholders.ContainsKey("{{Sum3_B42_PY}}") && decimal.TryParse(placeholders["{{Sum3_B42_PY}}"], out var v1PY) ? v1PY : 0;
                decimal b2940PY = placeholders.ContainsKey("{{Sum5_B2940_PY}}") && decimal.TryParse(placeholders["{{Sum5_B2940_PY}}"], out var v2PY) ? v2PY : 0;
                decimal b41PY = placeholders.ContainsKey("{{Sum3_B41_PY}}") && decimal.TryParse(placeholders["{{Sum3_B41_PY}}"], out var v3PY) ? v3PY : 0;
                decimal b52100PY = placeholders.ContainsKey("{{B52100_PY}}") && decimal.TryParse(placeholders["{{B52100_PY}}"], out var v4PY) ? v4PY : 0;

                decimal netPY = b42PY + b2940PY + b41PY + b52100PY;
                placeholders["{{9_PY}}"] = netPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 10. Baki 1 Jan Penyata Perubahan Aset Bersih

            public Dictionary<string, string> Baki1JanPenyataPerubahanAsetBersih(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal e13000CY = placeholders.ContainsKey("{{E13000_Jan1_CY}}") && decimal.TryParse(placeholders["{{E13000_Jan1_CY}}"], out var v1CY) ? v1CY : 0;
                decimal e13101CY = placeholders.ContainsKey("{{E13101_Jan1_CY}}") && decimal.TryParse(placeholders["{{E13101_Jan1_CY}}"], out var v2CY) ? v2CY : 0;
                decimal e13201CY = placeholders.ContainsKey("{{E13201_Jan1_CY}}") && decimal.TryParse(placeholders["{{E13201_Jan1_CY}}"], out var v3CY) ? v3CY : 0;
                decimal e14101CY = placeholders.ContainsKey("{{E14101_Jan1_CY}}") && decimal.TryParse(placeholders["{{E14101_Jan1_CY}}"], out var v4CY) ? v4CY : 0;

                decimal totalCY = (e13000CY + e13101CY + e13201CY + e14101CY) * -1;
                placeholders["{{10_CY}}"] = totalCY.ToString("#,##0");

                // ---------- PY (optional) ----------
                decimal e13000PY = placeholders.ContainsKey("{{E13000_Jan1_PY}}") && decimal.TryParse(placeholders["{{E13000_Jan1_PY}}"], out var v1PY) ? v1PY : 0;
                decimal e13101PY = placeholders.ContainsKey("{{E13101_Jan1_PY}}") && decimal.TryParse(placeholders["{{E13101_Jan1_PY}}"], out var v2PY) ? v2PY : 0;
                decimal e13201PY = placeholders.ContainsKey("{{E13201_Jan1_PY}}") && decimal.TryParse(placeholders["{{E13201_Jan1_PY}}"], out var v3PY) ? v3PY : 0;
                decimal e14101PY = placeholders.ContainsKey("{{E14101_Jan1_PY}}") && decimal.TryParse(placeholders["{{E14101_Jan1_PY}}"], out var v4PY) ? v4PY : 0;

                decimal totalPY = (e13000PY + e13101PY + e13201PY + e14101PY) * -1;
                placeholders["{{10_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 11. Perubahan Bersih Pelbagai Penghutang dan Deposit

            public Dictionary<string, string> Perubahan_Bersih_Pelbagai_Penghutang_Deposit(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal d1CY = placeholders.ContainsKey("{{A73101_debit_CY}}") && decimal.TryParse(placeholders["{{A73101_debit_CY}}"], out var dv1CY) ? dv1CY : 0;
                decimal d2CY = placeholders.ContainsKey("{{A73102_debit_CY}}") && decimal.TryParse(placeholders["{{A73102_debit_CY}}"], out var dv2CY) ? dv2CY : 0;
                decimal d3CY = placeholders.ContainsKey("{{A73103_debit_CY}}") && decimal.TryParse(placeholders["{{A73103_debit_CY}}"], out var dv3CY) ? dv3CY : 0;

                decimal c1CY = placeholders.ContainsKey("{{A73101_credit_CY}}") && decimal.TryParse(placeholders["{{A73101_credit_CY}}"], out var cv1CY) ? cv1CY : 0;
                decimal c2CY = placeholders.ContainsKey("{{A73102_credit_CY}}") && decimal.TryParse(placeholders["{{A73102_credit_CY}}"], out var cv2CY) ? cv2CY : 0;
                decimal c3CY = placeholders.ContainsKey("{{A73103_credit_CY}}") && decimal.TryParse(placeholders["{{A73103_credit_CY}}"], out var cv3CY) ? cv3CY : 0;

                decimal resultCY = (d1CY + d2CY + d3CY) - (c1CY + c2CY + c3CY);
                placeholders["{{11_CY}}"] = resultCY.ToString("#,##0");

                // ---------- PY ----------
                decimal d1PY = placeholders.ContainsKey("{{A73101_debit_PY}}") && decimal.TryParse(placeholders["{{A73101_debit_PY}}"], out var dv1PY) ? dv1PY : 0;
                decimal d2PY = placeholders.ContainsKey("{{A73102_debit_PY}}") && decimal.TryParse(placeholders["{{A73102_debit_PY}}"], out var dv2PY) ? dv2PY : 0;
                decimal d3PY = placeholders.ContainsKey("{{A73103_debit_PY}}") && decimal.TryParse(placeholders["{{A73103_debit_PY}}"], out var dv3PY) ? dv3PY : 0;

                decimal c1PY = placeholders.ContainsKey("{{A73101_credit_PY}}") && decimal.TryParse(placeholders["{{A73101_credit_PY}}"], out var cv1PY) ? cv1PY : 0;
                decimal c2PY = placeholders.ContainsKey("{{A73102_credit_PY}}") && decimal.TryParse(placeholders["{{A73102_credit_PY}}"], out var cv2PY) ? cv2PY : 0;
                decimal c3PY = placeholders.ContainsKey("{{A73103_credit_PY}}") && decimal.TryParse(placeholders["{{A73103_credit_PY}}"], out var cv3PY) ? cv3PY : 0;

                decimal resultPY = (d1PY + d2PY + d3PY) - (c1PY + c2PY + c3PY);
                placeholders["{{11_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 12. Perubahan Bersih Pelbagai Pemiutang dan Akruan

            public Dictionary<string, string> Perubahan_Bersih_Pelbagai_Pemiutang_Akruan(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal begL13CY = placeholders.ContainsKey("{{BegSum3_L13_CY}}") && decimal.TryParse(placeholders["{{BegSum3_L13_CY}}"], out var b13CY) ? b13CY : 0;
                decimal begL14CY = placeholders.ContainsKey("{{BegSum3_L14_CY}}") && decimal.TryParse(placeholders["{{BegSum3_L14_CY}}"], out var b14CY) ? b14CY : 0;
                decimal sumL13CY = placeholders.ContainsKey("{{Sum3_L13_CY}}") && decimal.TryParse(placeholders["{{Sum3_L13_CY}}"], out var s13CY) ? s13CY : 0;
                decimal sumL14CY = placeholders.ContainsKey("{{Sum3_L14_CY}}") && decimal.TryParse(placeholders["{{Sum3_L14_CY}}"], out var s14CY) ? s14CY : 0;

                decimal resultCY = (begL13CY + begL14CY) - (sumL13CY + sumL14CY);
                placeholders["{{12_CY}}"] = resultCY.ToString("#,##0");

                // ---------- PY ----------
                decimal begL13PY = placeholders.ContainsKey("{{BegSum3_L13_PY}}") && decimal.TryParse(placeholders["{{BegSum3_L13_PY}}"], out var b13PY) ? b13PY : 0;
                decimal begL14PY = placeholders.ContainsKey("{{BegSum3_L14_PY}}") && decimal.TryParse(placeholders["{{BegSum3_L14_PY}}"], out var b14PY) ? b14PY : 0;
                decimal sumL13PY = placeholders.ContainsKey("{{Sum3_L13_PY}}") && decimal.TryParse(placeholders["{{Sum3_L13_PY}}"], out var s13PY) ? s13PY : 0;
                decimal sumL14PY = placeholders.ContainsKey("{{Sum3_L14_PY}}") && decimal.TryParse(placeholders["{{Sum3_L14_PY}}"], out var s14PY) ? s14PY : 0;

                decimal resultPY = (begL13PY + begL14PY) - (sumL13PY + sumL14PY);
                placeholders["{{12_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 13. Perubahan Bersih Akaun Khas

            public Dictionary<string, string> Perubahan_Bersih_Akaun_Khas(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal begCY = placeholders.ContainsKey("{{BegSum4_L181_CY}}") && decimal.TryParse(placeholders["{{BegSum4_L181_CY}}"], out var bCY) ? bCY : 0;
                decimal sumCY = placeholders.ContainsKey("{{Sum4_L181_CY}}") && decimal.TryParse(placeholders["{{Sum4_L181_CY}}"], out var sCY) ? sCY : 0;

                decimal resultCY = (begCY - sumCY);
                placeholders["{{13_CY}}"] = resultCY.ToString("#,##0");

                // ---------- PY ----------
                decimal begPY = placeholders.ContainsKey("{{BegSum4_L181_PY}}") && decimal.TryParse(placeholders["{{BegSum4_L181_PY}}"], out var bPY) ? bPY : 0;
                decimal sumPY = placeholders.ContainsKey("{{Sum4_L181_PY}}") && decimal.TryParse(placeholders["{{Sum4_L181_PY}}"], out var sPY) ? sPY : 0;

                decimal resultPY = (begPY - sumPY);
                placeholders["{{13_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }

            #endregion

            #region 14. Perubahan Bersih Geran Pembangunan

            public Dictionary<string, string> Perubahan_Bersih_Geran_Pembangunan(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal begCY = placeholders.ContainsKey("{{BegSum4_L182_CY}}") && decimal.TryParse(placeholders["{{BegSum4_L182_CY}}"], out var bCY) ? bCY : 0;
                decimal sumCY = placeholders.ContainsKey("{{Sum4_L182_CY}}") && decimal.TryParse(placeholders["{{Sum4_L182_CY}}"], out var sCY) ? sCY : 0;

                decimal resultCY = (begCY - sumCY);
                placeholders["{{14_CY}}"] = resultCY.ToString("#,##0");

                // ---------- PY ----------
                decimal begPY = placeholders.ContainsKey("{{BegSum4_L182_PY}}") && decimal.TryParse(placeholders["{{BegSum4_L182_PY}}"], out var bPY) ? bPY : 0;
                decimal sumPY = placeholders.ContainsKey("{{Sum4_L182_PY}}") && decimal.TryParse(placeholders["{{Sum4_L182_PY}}"], out var sPY) ? sPY : 0;

                decimal resultPY = (begPY - sumPY);
                placeholders["{{14_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 15. Penambahan Loji dan Peralatan

            public Dictionary<string, string> Penambahan_Loji_Peralatan(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal d34CY = placeholders.ContainsKey("{{DebitSum3_A34_CY}}") && decimal.TryParse(placeholders["{{DebitSum3_A34_CY}}"], out var v1CY) ? v1CY : 0;
                decimal d35CY = placeholders.ContainsKey("{{DebitSum3_A35_CY}}") && decimal.TryParse(placeholders["{{DebitSum3_A35_CY}}"], out var v2CY) ? v2CY : 0;
                decimal d36CY = placeholders.ContainsKey("{{DebitSum3_A36_CY}}") && decimal.TryParse(placeholders["{{DebitSum3_A36_CY}}"], out var v3CY) ? v3CY : 0;
                decimal d37CY = placeholders.ContainsKey("{{DebitSum3_A37_CY}}") && decimal.TryParse(placeholders["{{DebitSum3_A37_CY}}"], out var v4CY) ? v4CY : 0;
                decimal d38CY = placeholders.ContainsKey("{{DebitSum3_A38_CY}}") && decimal.TryParse(placeholders["{{DebitSum3_A38_CY}}"], out var v5CY) ? v5CY : 0;

                decimal b34CY = placeholders.ContainsKey("{{BegSum3_A34_CY}}") && decimal.TryParse(placeholders["{{BegSum3_A34_CY}}"], out var b1CY) ? b1CY : 0;
                decimal b35CY = placeholders.ContainsKey("{{BegSum3_A35_CY}}") && decimal.TryParse(placeholders["{{BegSum3_A35_CY}}"], out var b2CY) ? b2CY : 0;
                decimal b36CY = placeholders.ContainsKey("{{BegSum3_A36_CY}}") && decimal.TryParse(placeholders["{{BegSum3_A36_CY}}"], out var b3CY) ? b3CY : 0;
                decimal b37CY = placeholders.ContainsKey("{{BegSum3_A37_CY}}") && decimal.TryParse(placeholders["{{BegSum3_A37_CY}}"], out var b4CY) ? b4CY : 0;
                decimal b38CY = placeholders.ContainsKey("{{BegSum3_A38_CY}}") && decimal.TryParse(placeholders["{{BegSum3_A38_CY}}"], out var b5CY) ? b5CY : 0;

                decimal resultCY = ((d34CY + d35CY + d36CY + d37CY + d38CY) - (b34CY + b35CY + b36CY + b37CY + b38CY)) * -1;

                placeholders["{{15_CY}}"] = resultCY.ToString("#,##0");

                // ---------- PY ----------
                decimal d34PY = placeholders.ContainsKey("{{DebitSum3_A34_PY}}") && decimal.TryParse(placeholders["{{DebitSum3_A34_PY}}"], out var v1PY) ? v1PY : 0;
                decimal d35PY = placeholders.ContainsKey("{{DebitSum3_A35_PY}}") && decimal.TryParse(placeholders["{{DebitSum3_A35_PY}}"], out var v2PY) ? v2PY : 0;
                decimal d36PY = placeholders.ContainsKey("{{DebitSum3_A36_PY}}") && decimal.TryParse(placeholders["{{DebitSum3_A36_PY}}"], out var v3PY) ? v3PY : 0;
                decimal d37PY = placeholders.ContainsKey("{{DebitSum3_A37_PY}}") && decimal.TryParse(placeholders["{{DebitSum3_A37_PY}}"], out var v4PY) ? v4PY : 0;
                decimal d38PY = placeholders.ContainsKey("{{DebitSum3_A38_PY}}") && decimal.TryParse(placeholders["{{DebitSum3_A38_PY}}"], out var v5PY) ? v5PY : 0;

                decimal b34PY = placeholders.ContainsKey("{{BegSum3_A34_PY}}") && decimal.TryParse(placeholders["{{BegSum3_A34_PY}}"], out var b1PY) ? b1PY : 0;
                decimal b35PY = placeholders.ContainsKey("{{BegSum3_A35_PY}}") && decimal.TryParse(placeholders["{{BegSum3_A35_PY}}"], out var b2PY) ? b2PY : 0;
                decimal b36PY = placeholders.ContainsKey("{{BegSum3_A36_PY}}") && decimal.TryParse(placeholders["{{BegSum3_A36_PY}}"], out var b3PY) ? b3PY : 0;
                decimal b37PY = placeholders.ContainsKey("{{BegSum3_A37_PY}}") && decimal.TryParse(placeholders["{{BegSum3_A37_PY}}"], out var b4PY) ? b4PY : 0;
                decimal b38PY = placeholders.ContainsKey("{{BegSum3_A38_PY}}") && decimal.TryParse(placeholders["{{BegSum3_A38_PY}}"], out var b5PY) ? b5PY : 0;

                decimal resultPY = ((d34PY + d35PY + d36PY + d37PY + d38PY) - (b34PY + b35PY + b36PY + b37PY + b38PY)) * -1;

                placeholders["{{15_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 16. Penerimaan dari Pelupusan Loji dan Peralatan

            public Dictionary<string, string> Penerimaan_Pelupusan_Loji_Peralatan(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal c34CY = placeholders.ContainsKey("{{CreditSum3_A34_CY}}") && decimal.TryParse(placeholders["{{CreditSum3_A34_CY}}"], out var v1CY) ? v1CY : 0;
                decimal c35CY = placeholders.ContainsKey("{{CreditSum3_A35_CY}}") && decimal.TryParse(placeholders["{{CreditSum3_A35_CY}}"], out var v2CY) ? v2CY : 0;
                decimal c36CY = placeholders.ContainsKey("{{CreditSum3_A36_CY}}") && decimal.TryParse(placeholders["{{CreditSum3_A36_CY}}"], out var v3CY) ? v3CY : 0;
                decimal c37CY = placeholders.ContainsKey("{{CreditSum3_A37_CY}}") && decimal.TryParse(placeholders["{{CreditSum3_A37_CY}}"], out var v4CY) ? v4CY : 0;
                decimal c38CY = placeholders.ContainsKey("{{CreditSum3_A38_CY}}") && decimal.TryParse(placeholders["{{CreditSum3_A38_CY}}"], out var v5CY) ? v5CY : 0;

                decimal totalCY = c34CY + c35CY + c36CY + c37CY + c38CY;
                placeholders["{{16_CY}}"] = totalCY.ToString("#,##0");

                // ---------- PY ----------
                decimal c34PY = placeholders.ContainsKey("{{CreditSum3_A34_PY}}") && decimal.TryParse(placeholders["{{CreditSum3_A34_PY}}"], out var v1PY) ? v1PY : 0;
                decimal c35PY = placeholders.ContainsKey("{{CreditSum3_A35_PY}}") && decimal.TryParse(placeholders["{{CreditSum3_A35_PY}}"], out var v2PY) ? v2PY : 0;
                decimal c36PY = placeholders.ContainsKey("{{CreditSum3_A36_PY}}") && decimal.TryParse(placeholders["{{CreditSum3_A36_PY}}"], out var v3PY) ? v3PY : 0;
                decimal c37PY = placeholders.ContainsKey("{{CreditSum3_A37_PY}}") && decimal.TryParse(placeholders["{{CreditSum3_A37_PY}}"], out var v4PY) ? v4PY : 0;
                decimal c38PY = placeholders.ContainsKey("{{CreditSum3_A38_PY}}") && decimal.TryParse(placeholders["{{CreditSum3_A38_PY}}"], out var v5PY) ? v5PY : 0;

                decimal totalPY = c34PY + c35PY + c36PY + c37PY + c38PY;
                placeholders["{{16_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 17. Penerimaan Daripada Pengeluaran Simpanan Tetap

            public Dictionary<string, string> Penerimaan_Daripada_Pengeluaran_Simpanan_Tetap(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal debitCY = placeholders.ContainsKey("{{DebitSum3_A13_CY}}") && decimal.TryParse(placeholders["{{DebitSum3_A13_CY}}"], out var dCY) ? dCY : 0;
                decimal creditCY = placeholders.ContainsKey("{{CreditSum3_A13_CY}}") && decimal.TryParse(placeholders["{{CreditSum3_A13_CY}}"], out var cCY) ? cCY : 0;

                decimal resultCY = debitCY - creditCY;
                placeholders["{{17_CY}}"] = resultCY.ToString("#,##0");

                // ---------- PY ----------
                decimal debitPY = placeholders.ContainsKey("{{DebitSum3_A13_PY}}") && decimal.TryParse(placeholders["{{DebitSum3_A13_PY}}"], out var dPY) ? dPY : 0;
                decimal creditPY = placeholders.ContainsKey("{{CreditSum3_A13_PY}}") && decimal.TryParse(placeholders["{{CreditSum3_A13_PY}}"], out var cPY) ? cPY : 0;

                decimal resultPY = debitPY - creditPY;
                placeholders["{{17_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 18. Faedah Atas Pelaburan Diterima

            public Dictionary<string, string> Faedah_Atas_Pelaburan_Diterima(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal begA18CY = placeholders.ContainsKey("{{BegSum3_A18_CY}}") && decimal.TryParse(placeholders["{{BegSum3_A18_CY}}"], out var bCY) ? bCY : 0;
                decimal sumH75CY = placeholders.ContainsKey("{{Sum3_H75_CY}}") && decimal.TryParse(placeholders["{{Sum3_H75_CY}}"], out var h75CY) ? h75CY : 0;
                decimal sumA18CY = placeholders.ContainsKey("{{Sum3_A18_CY}}") && decimal.TryParse(placeholders["{{Sum3_A18_CY}}"], out var sCY) ? sCY : 0;

                decimal resultCY = begA18CY - sumH75CY - sumA18CY;
                placeholders["{{18_CY}}"] = resultCY.ToString("#,##0");

                // ---------- PY ----------
                decimal begA18PY = placeholders.ContainsKey("{{BegSum3_A18_PY}}") && decimal.TryParse(placeholders["{{BegSum3_A18_PY}}"], out var bPY) ? bPY : 0;
                decimal sumH75PY = placeholders.ContainsKey("{{Sum3_H75_PY}}") && decimal.TryParse(placeholders["{{Sum3_H75_PY}}"], out var h75PY) ? h75PY : 0;
                decimal sumA18PY = placeholders.ContainsKey("{{Sum3_A18_PY}}") && decimal.TryParse(placeholders["{{Sum3_A18_PY}}"], out var sPY) ? sPY : 0;

                decimal resultPY = begA18PY - sumH75PY - sumA18PY;
                placeholders["{{18_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 19. Geran Pembangunan Dilunaskan

            public Dictionary<string, string> Geran_Pembangunan_Dilunaskan(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal debitCY = placeholders.ContainsKey("{{H89101_debit_CY}}") && decimal.TryParse(placeholders["{{H89101_debit_CY}}"], out var dCY) ? dCY : 0;
                decimal creditCY = placeholders.ContainsKey("{{H89101_credit_CY}}") && decimal.TryParse(placeholders["{{H89101_credit_CY}}"], out var cCY) ? cCY : 0;

                decimal resultCY = (debitCY - creditCY) * -1;
                placeholders["{{19_CY}}"] = resultCY.ToString("#,##0");

                // ---------- PY ----------
                decimal debitPY = placeholders.ContainsKey("{{H89101_debit_PY}}") && decimal.TryParse(placeholders["{{H89101_debit_PY}}"], out var dPY) ? dPY : 0;
                decimal creditPY = placeholders.ContainsKey("{{H89101_credit_PY}}") && decimal.TryParse(placeholders["{{H89101_credit_PY}}"], out var cPY) ? cPY : 0;

                decimal resultPY = (debitPY - creditPY) * -1;
                placeholders["{{19_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 24. Lebihan Terkumpul (Aset Bersih)

            public Dictionary<string, string> Lebihan_Terkumpul_Aset_Bersih(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal d13000CY = placeholders.ContainsKey("{{E13000_debit_CY}}") && decimal.TryParse(placeholders["{{E13000_debit_CY}}"], out var dv1CY) ? dv1CY : 0;
                decimal d13101CY = placeholders.ContainsKey("{{E13101_debit_CY}}") && decimal.TryParse(placeholders["{{E13101_debit_CY}}"], out var dv2CY) ? dv2CY : 0;
                decimal d13102CY = placeholders.ContainsKey("{{E13102_debit_CY}}") && decimal.TryParse(placeholders["{{E13102_debit_CY}}"], out var dv3CY) ? dv3CY : 0;
                decimal d13201CY = placeholders.ContainsKey("{{E13201_debit_CY}}") && decimal.TryParse(placeholders["{{E13201_debit_CY}}"], out var dv4CY) ? dv4CY : 0;

                decimal c13000CY = placeholders.ContainsKey("{{E13000_credit_CY}}") && decimal.TryParse(placeholders["{{E13000_credit_CY}}"], out var cv1CY) ? cv1CY : 0;
                decimal c13101CY = placeholders.ContainsKey("{{E13101_credit_CY}}") && decimal.TryParse(placeholders["{{E13101_credit_CY}}"], out var cv2CY) ? cv2CY : 0;
                decimal c13102CY = placeholders.ContainsKey("{{E13102_credit_CY}}") && decimal.TryParse(placeholders["{{E13102_credit_CY}}"], out var cv3CY) ? cv3CY : 0;
                decimal c13201CY = placeholders.ContainsKey("{{E13201_credit_CY}}") && decimal.TryParse(placeholders["{{E13201_credit_CY}}"], out var cv4CY) ? cv4CY : 0;

                decimal resultCY = ((d13000CY + d13101CY + d13102CY + d13201CY) - (c13000CY + c13101CY + c13102CY + c13201CY)) * -1;

                placeholders["{{24_CY}}"] = resultCY.ToString("#,##0");


                // ---------- PY ----------
                decimal d13000PY = placeholders.ContainsKey("{{E13000_debit_PY}}") && decimal.TryParse(placeholders["{{E13000_debit_PY}}"], out var dv1PY) ? dv1PY : 0;
                decimal d13101PY = placeholders.ContainsKey("{{E13101_debit_PY}}") && decimal.TryParse(placeholders["{{E13101_debit_PY}}"], out var dv2PY) ? dv2PY : 0;
                decimal d13102PY = placeholders.ContainsKey("{{E13102_debit_PY}}") && decimal.TryParse(placeholders["{{E13102_debit_PY}}"], out var dv3PY) ? dv3PY : 0;
                decimal d13201PY = placeholders.ContainsKey("{{E13201_debit_PY}}") && decimal.TryParse(placeholders["{{E13201_debit_PY}}"], out var dv4PY) ? dv4PY : 0;

                decimal c13000PY = placeholders.ContainsKey("{{E13000_credit_PY}}") && decimal.TryParse(placeholders["{{E13000_credit_PY}}"], out var cv1PY) ? cv1PY : 0;
                decimal c13101PY = placeholders.ContainsKey("{{E13101_credit_PY}}") && decimal.TryParse(placeholders["{{E13101_credit_PY}}"], out var cv2PY) ? cv2PY : 0;
                decimal c13102PY = placeholders.ContainsKey("{{E13102_credit_PY}}") && decimal.TryParse(placeholders["{{E13102_credit_PY}}"], out var cv3PY) ? cv3PY : 0;
                decimal c13201PY = placeholders.ContainsKey("{{E13201_credit_PY}}") && decimal.TryParse(placeholders["{{E13201_credit_PY}}"], out var cv4PY) ? cv4PY : 0;

                decimal resultPY = ((d13000PY + d13101PY + d13102PY + d13201PY) - (c13000PY + c13101PY + c13102PY + c13201PY)) * -1;

                placeholders["{{24_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 26. Cukai
            public Dictionary<string, string> Cukai(Dictionary<string, string> placeholders)
            {
                // ---------- CY ----------
                decimal b50CY = placeholders.ContainsKey("{{Sum3_B50_CY}}") && decimal.TryParse(placeholders["{{Sum3_B50_CY}}"], out var v1CY) ? v1CY : 0;
                decimal b59CY = placeholders.ContainsKey("{{Sum3_B59_CY}}") && decimal.TryParse(placeholders["{{Sum3_B59_CY}}"], out var v2CY) ? v2CY : 0;

                decimal resultCY = (b50CY + b59CY) * -1;
                placeholders["{{26_CY}}"] = resultCY.ToString("#,##0");

                // ---------- PY ----------
                decimal b50PY = placeholders.ContainsKey("{{Sum3_B50_PY}}") && decimal.TryParse(placeholders["{{Sum3_B50_PY}}"], out var v1PY) ? v1PY : 0;
                decimal b59PY = placeholders.ContainsKey("{{Sum3_B59_PY}}") && decimal.TryParse(placeholders["{{Sum3_B59_PY}}"], out var v2PY) ? v2PY : 0;

                decimal resultPY = (b50PY + b59PY) * -1;
                placeholders["{{26_PY}}"] = resultPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region Negation
            public Dictionary<string, string> NegatePlaceholders(Dictionary<string, string> placeholders, Dictionary<string, string> keyMappings)
            {
                foreach (var kvp in keyMappings)
                {
                    string sourceKey = kvp.Key;
                    string targetKey = kvp.Value;

                    if (placeholders.ContainsKey(sourceKey) && decimal.TryParse(placeholders[sourceKey], out decimal value))
                    {
                        placeholders[targetKey] = (value * -1).ToString("#,##0");
                    }
                }
                return placeholders;
            }

            #endregion

            //Negated Values for Placeholder Calculation
            #region 21.Akaun Khas Dilunaskan (Negated)
            #endregion

            #region 22.Keuntungan Pelupusan Loji dan Peralatan (Negated)
            #endregion

            #region 23.Jumlah Hasil (Negated)
            #endregion

            #region 25.Kumpulan Wang Komputer (Negated)
            #endregion

            #endregion

        }

        public class LPKPlaceholderCalculator : IPlaceholderCalculator
        {
            public Dictionary<string, string> CalculatePlaceholders(FinancialApiData cyData,FinancialApiData pyData,Dictionary<string, string> basePlaceholders)
            {
                basePlaceholders = Tunai_Dan_Baki_Bank(basePlaceholders);
                basePlaceholders = Deposit_Institusi_Kewangan(basePlaceholders);
                basePlaceholders = Akaun_Belum_Terima(basePlaceholders);
                basePlaceholders = Pelbagai_Penghutang_DLL(basePlaceholders);
                basePlaceholders = Jumlah_Terhutang_Syarikat_Subsidiari(basePlaceholders);
                basePlaceholders = Pinjaman_Kakitangan_NCA(basePlaceholders);
                return basePlaceholders;
            }

            public Dictionary<string, string> CalculateCompositePlaceholders(FinancialApiData cyData, FinancialApiData pyData, Dictionary<string, string> basePlaceholders)
            {
                PXTrace.WriteInformation("LPK.CalculateCompositePlaceholders() - Handling composite logic.");
                return basePlaceholders;
            }

            #region 1. TUNAI DAN BAKI DI BANK 
            public Dictionary<string, string> Tunai_Dan_Baki_Bank(Dictionary<string, string> placeholders)
            {
                // Define all the CY keys you want to sum
                var cyKeys = new[]
                {
                "{{A11101_CY}}",
                "{{A11104_CY}}",
                "{{A11901_CY}}",
                "{{A11201_CY}}"
                };

                decimal totalCY = 0m;
                foreach (var key in cyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalCY += value;
                    }
                }
                placeholders["{{1_CY}}"] = totalCY.ToString("#,##0");


                // Define all the PY keys (same pattern as above, just _PY)
                var pyKeys = new[]
                {
                "{{A11101_PY}}",
                "{{A11104_PY}}",
                "{{A11901_PY}}",
                "{{A11201_PY}}"
                };

                decimal totalPY = 0m;
                foreach (var key in pyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalPY += value;
                    }
                }
                placeholders["{{1_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }
            #endregion

            #region 2. DEPOSIT DI INSTITUSI KEWANGAN 
            public Dictionary<string, string> Deposit_Institusi_Kewangan(Dictionary<string, string> placeholders)
            {
                // Define all the CY keys you want to sum
                var cyKeys = new[]
                {
                "{{A12101_CY}}",
                "{{A13101_CY}}"
                };

                decimal totalCY = 0m;
                foreach (var key in cyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalCY += value;
                    }
                }
                placeholders["{{2_CY}}"] = totalCY.ToString("#,##0");


                // Define all the PY keys (same pattern as above, just _PY)
                var pyKeys = new[]
                {
                "{{A12101_PY}}",
                "{{A13101_PY}}"
                };

                decimal totalPY = 0m;
                foreach (var key in pyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalPY += value;
                    }
                }
                placeholders["{{2_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }
            #endregion

            #region 3. AKAUN BELUM TERIMA
            public Dictionary<string, string> Akaun_Belum_Terima(Dictionary<string, string> placeholders)
            {
                // Helper method to calculate total for a suffix
                decimal CalculateTotal(string suffix)
                {
                    decimal total = 0m;

                    // A15103 to A15106
                    for (int x = 3; x <= 6; x++)
                    {
                        string key = $"{{A1510{x}_{suffix}}}";
                        if (placeholders.TryGetValue(key, out string value) && decimal.TryParse(value, out decimal dec))
                        {
                            total += dec;
                        }
                    }

                    // Static keys: A15201 and A15202
                    if (placeholders.TryGetValue($"{{A15201_{suffix}}}", out string value1) && decimal.TryParse(value1, out decimal dec1))
                    {
                        total += dec1;
                    }
                    if (placeholders.TryGetValue($"{{A15202_{suffix}}}", out string value2) && decimal.TryParse(value2, out decimal dec2))
                    {
                        total += dec2;
                    }

                    // A16100 to A16107
                    for (int x = 0; x < 8; x++)
                    {
                        string key = $"{{A1610{x}_{suffix}}}";
                        if (placeholders.TryGetValue(key, out string value) && decimal.TryParse(value, out decimal dec))
                        {
                            total += dec;
                        }
                    }

                    return total;
                }

                placeholders["{{3_CY}}"] = CalculateTotal("CY").ToString("#,##0");
                placeholders["{{3_PY}}"] = CalculateTotal("PY").ToString("#,##0");

                return placeholders;
            }
            #endregion

            #region 4. PELBAGAI PENGHUTANG, DEPOSIT DAN PRABAYAR
            public Dictionary<string, string> Pelbagai_Penghutang_DLL(Dictionary<string, string> placeholders)
            {
                // Helper method to calculate total for a suffix
                decimal CalculateTotal(string suffix)
                {
                    decimal total = 0m;

                    // A15115 to A15199
                    for (int x = 15; x <= 99; x++)
                    {
                        string key = $"{{A151{x}_{suffix}}}";
                        if (placeholders.TryGetValue(key, out string value) && decimal.TryParse(value, out decimal dec))
                        {
                            total += dec;
                        }
                    }

                    // A15300 to A15999
                    for (int x = 300; x <= 999; x++)
                    {
                        string key = $"{{A15{x}_{suffix}}}";
                        if (placeholders.TryGetValue(key, out string value) && decimal.TryParse(value, out decimal dec))
                        {
                            total += dec;
                        }
                    }

                    // Static keys (adding)
                    if (placeholders.TryGetValue($"{{A16108_{suffix}}}", out string value1) && decimal.TryParse(value1, out decimal dec1)) { total += dec1; }
                    if (placeholders.TryGetValue($"{{A16109_{suffix}}}", out string value2) && decimal.TryParse(value2, out decimal dec2)) { total += dec2; }
                    if (placeholders.TryGetValue($"{{A74101_{suffix}}}", out string value3) && decimal.TryParse(value3, out decimal dec3)) { total += dec3; }
                    if (placeholders.TryGetValue($"{{A74102_{suffix}}}", out string value4) && decimal.TryParse(value4, out decimal dec4)) { total += dec4; }
                    if (placeholders.TryGetValue($"{{A81101_{suffix}}}", out string value5) && decimal.TryParse(value5, out decimal dec5)) { total += dec5; }
                    if (placeholders.TryGetValue($"{{A21101_{suffix}}}", out string value6) && decimal.TryParse(value6, out decimal dec6)) { total += dec6; }
                    // Subtract L15100
                    if (placeholders.TryGetValue($"{{L15100_{suffix}}}", out string value7) && decimal.TryParse(value7, out decimal dec7)) { total -= dec7; }
                    return total;
                }

                placeholders["{{4_CY}}"] = CalculateTotal("CY").ToString("#,##0");
                placeholders["{{4_PY}}"] = CalculateTotal("PY").ToString("#,##0");

                return placeholders;
            }

            #endregion

            #region 5. PINJAMAN KAKITANGAN            
            #endregion

            #region 6. JUMLAH TERHUTANG OLEH SYARIKAT SUBSIDIARI
            public Dictionary<string, string> Jumlah_Terhutang_Syarikat_Subsidiari(Dictionary<string, string> placeholders)
            {
                // Define all the CY keys you want to sum
                var cyKeys = new[]
                {
                "{{A15101_CY}}",
                "{{A15107_CY}}",
                "{{A15113_CY}}"
                };

                decimal totalCY = 0m;
                foreach (var key in cyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalCY += value;
                    }
                }

                placeholders["{{6_CY}}"] = totalCY.ToString("#,##0");


                // Define all the PY keys (same pattern as above, just _PY)
                var pyKeys = new[]
                {
                "{{A15101_PY}}",
                "{{A15107_PY}}",
                "{{A15113_PY}}"
                };

                decimal totalPY = 0m;
                foreach (var key in pyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalPY += value;
                    }
                }

                placeholders["{{6_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }
            #endregion

            #region 7. PINJAMAN KAKITANGAN (NCA)    
            public Dictionary<string, string> Pinjaman_Kakitangan_NCA(Dictionary<string, string> placeholders)
            {
                // Define all the CY keys you want to sum
                var cyKeys = new[]
                {
                "{{A15101_CY}}",
                "{{A15107_CY}}",
                "{{A15113_CY}}"
                };

                decimal totalCY = 0m;
                foreach (var key in cyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalCY += value;
                    }
                }

                placeholders["{{7_CY}}"] = totalCY.ToString("#,##0");


                // Define all the PY keys (same pattern as above, just _PY)
                var pyKeys = new[]
                {
                "{{A15101_PY}}",
                "{{A15107_PY}}",
                "{{A15113_PY}}"
                };

                decimal totalPY = 0m;
                foreach (var key in pyKeys)
                {
                    if (placeholders.ContainsKey(key) && decimal.TryParse(placeholders[key], out decimal value))
                    {
                        totalPY += value;
                    }
                }

                placeholders["{{7_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }

            #endregion

        }

        public class IKMAPlaceholderCalculator : IPlaceholderCalculator
        {

            private readonly string _baseUrl;
            private readonly AuthService _authService;
            private readonly string _tenantName;

            public IKMAPlaceholderCalculator(string baseUrl, AuthService authService, string tenantName)
            {
                _baseUrl = baseUrl;
                _authService = authService;
                _tenantName = tenantName;
            }

            public Dictionary<string, string> CalculatePlaceholders(FinancialApiData cyData, FinancialApiData pyData, Dictionary<string, string> basePlaceholders)
            {
                PXTrace.WriteInformation("IKMAPlaceholderCalculator.CalculatePlaceholders() called.");

                //basePlaceholders = Penghutang(basePlaceholders);
                basePlaceholders = WangTunai_BakiBank(basePlaceholders, cyData, pyData);
                return basePlaceholders;
            }

            public Dictionary<string, string> CalculateCompositePlaceholders(FinancialApiData cyData, FinancialApiData pyData, Dictionary<string, string> basePlaceholders)
            {
                PXTrace.WriteInformation("IKMA.CalculateCompositePlaceholders() - Handling composite logic.");

                //basePlaceholders = TestExtraction(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);
                basePlaceholders = Penghutang(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);
                basePlaceholders = PenghutangPinjamanKomputer(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);
                basePlaceholders = PenghutangPinjamanKenderaan(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);
                basePlaceholders = PendahuluanKakitanganPerjalanan(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);
                basePlaceholders = PendahuluanKakitanganPelbagai(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);
                basePlaceholders = Pertaruhan(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);
                basePlaceholders = PendahuluanSubsidari(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);
                basePlaceholders = PendahuluanKakitanganPelbagai_SingleOData(basePlaceholders);
                basePlaceholders = SimpananTetap(basePlaceholders, cyData.CompositeKeyData, pyData.CompositeKeyData);

                

                return basePlaceholders;
            }

            #region Calculation Methods

            #region a. Penghutang
            //public Dictionary<string, string> Penghutang(Dictionary<string, string> placeholders)
            //{
            //    var cyKeys = new[]
            //    {
            //        "{{A15101_CY}}", "{{A15102_CY}}", "{{A15103_CY}}",
            //        "{{A15104_CY}}", "{{A15105_CY}}", "{{A15106_CY}}", "{{A76102_CY}}"
            //    };

            //    decimal totalCY = cyKeys.Sum(k => placeholders.ContainsKey(k) && decimal.TryParse(placeholders[k], out var v) ? v : 0);
            //    placeholders["{{1_CY}}"] = totalCY.ToString("#,##0");

            //    var pyKeys = new[]
            //    {
            //        "{{A15101_PY}}", "{{A15102_PY}}", "{{A15103_PY}}",
            //        "{{A15104_PY}}", "{{A15105_PY}}", "{{A15106_PY}}", "{{A76102_PY}}" // ← double check this line should be CY or PY
            //    };

            //    decimal totalPY = pyKeys.Sum(k => placeholders.ContainsKey(k) && decimal.TryParse(placeholders[k], out var v) ? v : 0);
            //    placeholders["{{1_PY}}"] = totalPY.ToString("#,##0");

            //    return placeholders;
            //}

            #endregion

            #region b. Test Extraction
            //public Dictionary<string, string> TestExtraction(
            //    Dictionary<string, string> placeholders,
            //    Dictionary<string, FinancialPeriodData> cyComposite,
            //    Dictionary<string, FinancialPeriodData> pyComposite)
            //{
            //    string accountId = "101000".Trim();
            //    string subaccountId = "0000000".Trim();
            //    string branchId = "SOFT".Trim();
            //    string orgId = "SOFT".Trim();

            //    string month = placeholders.ContainsKey("{{MonthNumber}}") ? placeholders["{{MonthNumber}}"] : null;
            //    string yearCY = placeholders.ContainsKey("{{CY}}") ? placeholders["{{CY}}"] : null;
            //    string yearPY = placeholders.ContainsKey("{{PY}}") ? placeholders["{{PY}}"] : null;

            //    // Later validation or handling if null
            //    if (string.IsNullOrWhiteSpace(month) || string.IsNullOrWhiteSpace(yearCY) || string.IsNullOrWhiteSpace(yearPY))
            //    {
            //        throw new PXException(Messages.NoValueMapping);
            //    }

            //    string periodCY = month + yearCY;
            //    string periodPY = month + yearPY;

            //    string compositeKeyCY = $"{accountId}-{subaccountId}-{branchId}-{orgId}-{periodCY}";
            //    string compositeKeyPY = $"{accountId}-{subaccountId}-{branchId}-{orgId}-{periodPY}";

            //    PXTrace.WriteInformation($"Trying to match CY key: {compositeKeyCY}");
            //    PXTrace.WriteInformation($"Trying to match PY key: {compositeKeyPY}");

            //    PXTrace.WriteInformation($"cyComposite.Keys.Count: {cyComposite?.Count ?? 0}");
            //    PXTrace.WriteInformation($"pyComposite.Keys.Count: {pyComposite?.Count ?? 0}");

            //    foreach (var key in cyComposite.Keys)
            //    {
            //        PXTrace.WriteInformation($"CY Key in Dictionary: {key}");
            //    }

            //    foreach (var key in pyComposite.Keys)
            //    {
            //        PXTrace.WriteInformation($"PY Key in Dictionary: {key}");
            //    }

            //    if (cyComposite.TryGetValue(compositeKeyCY, out var cy))
            //    {
            //        placeholders["{{Composite_A15101_CY}}"] = cy.EndingBalance.ToString("#,##0");
            //        PXTrace.WriteInformation($"[CY] Matched Key: {compositeKeyCY}");
            //        PXTrace.WriteInformation($"[CY] EndingBalance: {cy.EndingBalance}, Debit: {cy.Debit}, Credit: {cy.Credit}, Description: {cy.Description}");
            //    }
            //    else
            //    {
            //        PXTrace.WriteWarning($"[CY] Key not found: {compositeKeyCY}");
            //    }

            //    if (pyComposite.TryGetValue(compositeKeyPY, out var py))
            //    {
            //        placeholders["{{Composite_A15101_PY}}"] = py.EndingBalance.ToString("#,##0");
            //        PXTrace.WriteInformation($"[PY] Matched Key: {compositeKeyPY}");
            //        PXTrace.WriteInformation($"[PY] EndingBalance: {py.EndingBalance}, Debit: {py.Debit}, Credit: {py.Credit}, Description: {py.Description}");
            //    }
            //    else
            //    {
            //        PXTrace.WriteWarning($"[PY] Key not found: {compositeKeyPY}");
            //    }

            //    return placeholders;
            //}


            #endregion

            #region 3: Penghutang, Pendahuluan dan Deposit

            #region 3.1 Penghutang
            public Dictionary<string, string> Penghutang(Dictionary<string, string> placeholders, Dictionary<string, FinancialPeriodData> cyComposite, Dictionary<string, FinancialPeriodData> pyComposite)
            {
                var targetAccounts = new[] { "A15101", "A15102", "A15103", "A15104", "A15105", "A15106" };

                // Sum accounts A15101–A15106 (all branches/orgs/subaccounts)
                decimal sumCY = cyComposite
                    .Where(kvp => targetAccounts.Any(acct => kvp.Key.StartsWith(acct + "-", StringComparison.OrdinalIgnoreCase)))
                    .Sum(kvp => kvp.Value.EndingBalance);

                decimal sumPY = pyComposite
                    .Where(kvp => targetAccounts.Any(acct => kvp.Key.StartsWith(acct + "-", StringComparison.OrdinalIgnoreCase)))
                    .Sum(kvp => kvp.Value.EndingBalance);

                // Subtract the specific A76102 value (MIP branch, XXXX-XXX subaccount)
                string specificCYKey = cyComposite.Keys.FirstOrDefault(key =>
                    key.StartsWith("A76102-", StringComparison.OrdinalIgnoreCase) &&
                    key.Contains("-XXXXXXX-", StringComparison.OrdinalIgnoreCase) &&
                    key.Contains("-MIP-", StringComparison.OrdinalIgnoreCase)
                );

                string specificPYKey = pyComposite.Keys.FirstOrDefault(key =>
                    key.StartsWith("A76102-", StringComparison.OrdinalIgnoreCase) &&
                    key.Contains("-XXXXXXX-", StringComparison.OrdinalIgnoreCase) &&
                    key.Contains("-MIP-", StringComparison.OrdinalIgnoreCase)
                );

                decimal adjustmentCY = 0m;
                decimal adjustmentPY = 0m;

                if (specificCYKey != null && cyComposite.TryGetValue(specificCYKey, out var cy))
                {
                    adjustmentCY = cy.EndingBalance;
                    PXTrace.WriteInformation($"3.1 Penghutang CY: {specificCYKey}, Value: {adjustmentCY}");
                }
                else
                {
                    PXTrace.WriteWarning($"3.1 Penghutang CY not found: {specificCYKey}");
                }

                if (specificPYKey != null && pyComposite.TryGetValue(specificPYKey, out var py))
                {
                    adjustmentPY = py.EndingBalance;
                    PXTrace.WriteInformation($"3.1 Penghutang PY: {specificPYKey}, Value: {adjustmentPY}");
                }
                else
                {
                    PXTrace.WriteWarning($"3.1 Penghutang PY not found: {specificPYKey}");
                }

                decimal totalCY = sumCY + adjustmentCY;
                decimal totalPY = sumPY + adjustmentPY;

                PXTrace.WriteInformation($"Total CY Value for 3.1 Penghutang: {totalCY}");
                PXTrace.WriteInformation($"Total PY Value for 3.1 Penghutang: {totalPY}");

                placeholders["{{3.1_CY}}"] = totalCY.ToString("#,##0");
                placeholders["{{3.1_PY}}"] = totalPY.ToString("#,##0");

                PXTrace.WriteInformation($"[Final] Composite_1_CY: {totalCY}, Composite_1_PY: {totalPY}");

                return placeholders;
            }
            #endregion

            #region 3.2 Penghutang Pinjaman Komputer
            public Dictionary<string, string> PenghutangPinjamanKomputer(Dictionary<string, string> placeholders, Dictionary<string, FinancialPeriodData> cyComposite, Dictionary<string, FinancialPeriodData> pyComposite)
            {
                string accountId = "A75101";
                string subaccountId = "XXXXXXX";
                string branchId = "KIP";

                string cyKey = cyComposite.Keys.FirstOrDefault(key =>
                    key.StartsWith(accountId + "-", StringComparison.OrdinalIgnoreCase) &&
                    key.Contains($"-{subaccountId}-", StringComparison.OrdinalIgnoreCase) &&
                    key.Contains($"-{branchId}-", StringComparison.OrdinalIgnoreCase)
                );

                string pyKey = pyComposite.Keys.FirstOrDefault(key =>
                    key.StartsWith(accountId + "-", StringComparison.OrdinalIgnoreCase) &&
                    key.Contains($"-{subaccountId}-", StringComparison.OrdinalIgnoreCase) &&
                    key.Contains($"-{branchId}-", StringComparison.OrdinalIgnoreCase)
                );

                if (cyKey != null && cyComposite.TryGetValue(cyKey, out var cy))
                {
                    placeholders["{{3.2_CY}}"] = cy.EndingBalance.ToString("#,##0");
                    PXTrace.WriteInformation($"3.2 Penghutang Pinjaman Komputer CY: {cyKey}, EndingBalance: {cy.EndingBalance}");
                }
                else
                {
                    PXTrace.WriteWarning($"[CY] A75101 not found for {subaccountId} / {branchId}");
                }

                if (pyKey != null && pyComposite.TryGetValue(pyKey, out var py))
                {
                    placeholders["{{3.2_PY}}"] = py.EndingBalance.ToString("#,##0");
                    PXTrace.WriteInformation($"3.2 Penghutang Pinjaman Komputer PY: {pyKey}, EndingBalance: {py.EndingBalance}");
                }
                else
                {
                    PXTrace.WriteWarning($"[PY] A75101 not found for {subaccountId} / {branchId}");
                }

                return placeholders;
            }

            #endregion

            #region 3.3 Penghutang Pinjaman Kenderaan
            public Dictionary<string, string> PenghutangPinjamanKenderaan(Dictionary<string, string> placeholders, Dictionary<string, FinancialPeriodData> cyComposite, Dictionary<string, FinancialPeriodData> pyComposite)
            {
                string[] accountIds = { "A74101", "A74201" };
                string subaccountId = "XXXXXXX";
                string branchId = "CIP";

                decimal totalCY = 0;
                decimal totalPY = 0;

                foreach (var accountId in accountIds)
                {
                    string cyKey = cyComposite.Keys.FirstOrDefault(key =>
                        key.StartsWith(accountId + "-", StringComparison.OrdinalIgnoreCase) &&
                        key.Contains($"-{subaccountId}-", StringComparison.OrdinalIgnoreCase) &&
                        key.Contains($"-{branchId}-", StringComparison.OrdinalIgnoreCase)
                    );

                    if (cyKey != null && cyComposite.TryGetValue(cyKey, out var cy))
                    {
                        PXTrace.WriteInformation($"3.3 Penghutang Pinjaman Kenderaan CY: {cyKey}, Value: {cy.EndingBalance}");
                        totalCY += cy.EndingBalance;
                    }
                    else
                    {
                        PXTrace.WriteWarning($"3.3 Penghutang Pinjaman Kenderaan CY not found for Account: {accountId}, Sub: {subaccountId}, Branch: {branchId}");
                    }

                    string pyKey = pyComposite.Keys.FirstOrDefault(key =>
                        key.StartsWith(accountId + "-", StringComparison.OrdinalIgnoreCase) &&
                        key.Contains($"-{subaccountId}-", StringComparison.OrdinalIgnoreCase) &&
                        key.Contains($"-{branchId}-", StringComparison.OrdinalIgnoreCase)
                    );

                    if (pyKey != null && pyComposite.TryGetValue(pyKey, out var py))
                    {
                        PXTrace.WriteInformation($"3.3 Penghutang Pinjaman Kenderaan PY: {pyKey}, Value: {py.EndingBalance}");
                        totalPY += py.EndingBalance;
                    }
                    else
                    {
                        PXTrace.WriteWarning($"3.3 Penghutang Pinjaman Kenderaan CY not found for Account: {accountId}, Sub: {subaccountId}, Branch: {branchId}");
                    }
                }

                placeholders["{{3.3_CY}}"] = totalCY.ToString("#,##0");
                placeholders["{{3.3_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #region 3.4 Pendaluluan

            #region 3.4.1 Kakitangan (Perjalanan)
            public Dictionary<string, string> PendahuluanKakitanganPerjalanan(Dictionary<string, string> placeholders, Dictionary<string, FinancialPeriodData> cyComposite, Dictionary<string, FinancialPeriodData> pyComposite)
            {
                string accountPrefix = "A73101";
                string subaccountId = "XXXXXXX";

                decimal totalCY = 0;
                decimal totalPY = 0;

                // Loop over all CY entries and match prefix + subaccount
                foreach (var kvp in cyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 6) continue;

                    string accountId = parts[0];
                    string subId = parts[1];

                    if (accountId.StartsWith(accountPrefix) && subId == subaccountId)
                    {
                        totalCY += kvp.Value.EndingBalance;
                        if (kvp.Value.EndingBalance != 0)
                        {
                            PXTrace.WriteInformation($"3.4.1 Kakitangan (Perjalanan) Matched CY: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                        }
                            
                    }
                }

                // Loop over all PY entries and match prefix + subaccount
                foreach (var kvp in pyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 6) continue;

                    string accountId = parts[0];
                    string subId = parts[1];

                    if (accountId.StartsWith(accountPrefix) && subId == subaccountId)
                    {
                        totalPY += kvp.Value.EndingBalance;
                        if (kvp.Value.EndingBalance != 0)
                        {
                            PXTrace.WriteInformation($"3.4.1 Kakitangan (Perjalanan) Matched PY: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                        }
                    }
                }

                placeholders["{{3.4.1_CY}}"] = totalCY.ToString("#,##0");
                placeholders["{{3.4.1_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }
            #endregion

            #region 3.4.2 Kakitangan (Pelbagai)
            public Dictionary<string, string> PendahuluanKakitanganPelbagai(Dictionary<string, string> placeholders,Dictionary<string, FinancialPeriodData> cyComposite,Dictionary<string, FinancialPeriodData> pyComposite)
            {
                string accountPrefix = "A73102";
                string subaccountId = "XXXXXXX";
                string ledgerid = "ACTUAL";
                string branchid = "MIP";
                string orgid = "M";

                decimal totalCY = 0;
                decimal totalPY = 0;
                // Loop over all CY entries and match prefix + subaccount
                foreach (var kvp in cyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 6) continue;

                    string accountId = parts[0];
                    string subId = parts[1];
                    string branchId = parts[2];
                    string orgId = parts[3];
                    string ledgerID = parts[5];

                    if (accountId.StartsWith(accountPrefix) && subId == subaccountId && branchId == branchid && orgId == orgid && ledgerID == ledgerid)
                    {

                        PXTrace.WriteInformation($"Found Value for A73102: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");

                    }
                    else
                    {
                        PXTrace.WriteInformation($"Not Found Value for A73102-XXXXXXX-MIP-M-{ledgerid}");
                    }


                    if (accountId.StartsWith(accountPrefix) && subId == subaccountId && ledgerID == ledgerid)
                    {
                        totalCY += kvp.Value.EndingBalance;
                        if (kvp.Value.EndingBalance != 0)
                        {
                            PXTrace.WriteInformation($"[CY] Matched: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                        }

                    }
                }

                // Loop over all PY entries and match prefix + subaccount
                foreach (var kvp in pyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 6) continue;

                    string accountId = parts[0];
                    string subId = parts[1];
                    string ledgerID = parts[5];

                    if (accountId.StartsWith(accountPrefix) && subId == subaccountId && ledgerID == ledgerid)
                    {
                        totalPY += kvp.Value.EndingBalance;
                        if (kvp.Value.EndingBalance != 0)
                        {
                            PXTrace.WriteInformation($"3.4.1 Kakitangan (Perjalanan) Matched PY: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                        }
                    }
                }

                placeholders["{{3.4.2_CY}}"] = totalCY.ToString("#,##0");
                placeholders["{{3.4.2_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }



            #endregion

            #region 3.4.2.2 Kakitangan (Pelbagai) - Single Data

            public Dictionary<string, string> PendahuluanKakitanganPelbagai_SingleOData(Dictionary<string, string> placeholders)
            {
                // 1) Extract period from placeholders
                string month = placeholders.ContainsKey("{{MonthNumber}}") ? placeholders["{{MonthNumber}}"] : "12";
                string yearCY = placeholders.ContainsKey("{{CY}}") ? placeholders["{{CY}}"] : DateTime.Now.Year.ToString();
                string yearPY = placeholders.ContainsKey("{{PY}}") ? placeholders["{{PY}}"] : (DateTime.Now.Year - 1).ToString();

                string periodCY = month + yearCY;
                string periodPY = month + yearPY;

                string ledger = "ACTUAL";
                string organization = "M"; // 🔒 Organization remains the same

                // 2) Define account-subaccount-branch combinations
                var combos = new List<(string Account, string Subaccount, string Branch)>
                {
                    ("A73102", "XXXXXXX", "MIP"),
                    ("A73102", "XXXXXXX", "MS1")
                };

                var dataService = new FinancialDataService(_baseUrl, _authService, _tenantName);

                decimal totalCY = 0m;
                decimal totalPY = 0m;

                foreach (var combo in combos)
                {
                    decimal cyEnding = dataService.FetchEndingBalance(periodCY, combo.Branch, organization, ledger, combo.Account, combo.Subaccount);
                    decimal pyEnding = dataService.FetchEndingBalance(periodPY, combo.Branch, organization, ledger, combo.Account, combo.Subaccount);

                    PXTrace.WriteInformation($"[CY] {combo.Account}-{combo.Subaccount}-{combo.Branch}: {cyEnding}");
                    PXTrace.WriteInformation($"[PY] {combo.Account}-{combo.Subaccount}-{combo.Branch}: {pyEnding}");

                    totalCY += cyEnding;
                    totalPY += pyEnding;
                }

                placeholders["{{3.4.2.2_CY}}"] = totalCY.ToString("#,##0");
                placeholders["{{3.4.2.2_PY}}"] = totalPY.ToString("#,##0");

                PXTrace.WriteInformation($"✅ Final 3.4.2.2_CY Total: {totalCY}");
                PXTrace.WriteInformation($"✅ Final 3.4.2.2_PY Total: {totalPY}");

                return placeholders;
            }





            #endregion

            #endregion

            #region 3.5 Pertaruhan
            public Dictionary<string, string> Pertaruhan(Dictionary<string, string> placeholders, Dictionary<string, FinancialPeriodData> cyComposite, Dictionary<string, FinancialPeriodData> pyComposite)
            {
                string orgFilter = "M";
                var validAccounts = new HashSet<string> { "A15107", "A14901", "A14902", "A14903", "A14999" };

                decimal sumCY = 0;
                decimal sumPY = 0;

                PXTrace.WriteInformation("🔍 Calculating 3.5 Pertaruhan - CY");
                foreach (var kvp in cyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 5) continue;

                    string accId = parts[0];
                    string orgId = parts[3]?.Trim();

                    if (validAccounts.Contains(accId) && orgId == orgFilter)
                    {
                        sumCY += kvp.Value.EndingBalance;
                        //PXTrace.WriteInformation($"✔️ [CY] Matched: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                    }
                }

                PXTrace.WriteInformation("🔍 Calculating 3.5 Pertaruhan - PY");
                foreach (var kvp in pyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 5) continue;

                    string accId = parts[0];
                    string orgId = parts[3]?.Trim();

                    if (validAccounts.Contains(accId) && orgId == orgFilter)
                    {
                        sumPY += kvp.Value.EndingBalance;
                        //PXTrace.WriteInformation($"✔️ [PY] Matched: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                    }
                }

                placeholders["{{3.5_CY}}"] = sumCY.ToString("#,##0");
                placeholders["{{3.5_PY}}"] = sumPY.ToString("#,##0");

                PXTrace.WriteInformation($"✅ Total 3.5 CY: {sumCY}");
                PXTrace.WriteInformation($"✅ Total 3.5 PY: {sumPY}");

                return placeholders;
            }
            #endregion

            #endregion

            #region 4. Simpanan Tetap

            public Dictionary<string, string> SimpananTetap(Dictionary<string, string> placeholders, Dictionary<string, FinancialPeriodData> cyComposite, Dictionary<string, FinancialPeriodData> pyComposite)
            {
                string accountPrefix = "A13101";
                string subaccountId = "XXXXXXX";

                decimal totalCY = 0;
                decimal totalPY = 0;

                // Loop over all CY entries and match prefix + subaccount
                foreach (var kvp in cyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 6) continue;

                    string accountId = parts[0];
                    string subId = parts[1];

                    if (accountId.StartsWith(accountPrefix) && subId == subaccountId)
                    {
                        totalCY += kvp.Value.EndingBalance;
                        if (kvp.Value.EndingBalance != 0)
                        {
                            PXTrace.WriteInformation($"3.4.1 Kakitangan (Perjalanan) Matched CY: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                        }

                    }
                }

                // Loop over all PY entries and match prefix + subaccount
                foreach (var kvp in pyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 6) continue;

                    string accountId = parts[0];
                    string subId = parts[1];

                    if (accountId.StartsWith(accountPrefix) && subId == subaccountId)
                    {
                        totalPY += kvp.Value.EndingBalance;
                        if (kvp.Value.EndingBalance != 0)
                        {
                            PXTrace.WriteInformation($"3.4.1 Kakitangan (Perjalanan) Matched PY: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                        }
                    }
                }

                placeholders["{{4_CY}}"] = totalCY.ToString("#,##0");
                placeholders["{{4_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }

            #endregion


            #region 5. Wang Tunai dan Baki di Bank

            private Dictionary<string, string> WangTunai_BakiBank(Dictionary<string, string> placeholders, FinancialApiData cyData, FinancialApiData pyData)
            {
                decimal cyTotal = 0;
                decimal pyTotal = 0;

                // 1) Sum A11101 → A11204
                for (int i = 11101; i <= 11204; i++)
                {
                    string accountId = $"A{i}";

                    if (cyData.AccountData.TryGetValue(accountId, out var cy))
                        cyTotal += cy.EndingBalance;

                    if (pyData.AccountData.TryGetValue(accountId, out var py))
                        pyTotal += py.EndingBalance;
                }

                // 2) Add A13102 → A13106
                for (int i = 13102; i <= 13106; i++)
                {
                    string accountId = $"A{i}";

                    if (cyData.AccountData.TryGetValue(accountId, out var cy))
                        cyTotal += cy.EndingBalance;

                    if (pyData.AccountData.TryGetValue(accountId, out var py))
                        pyTotal += py.EndingBalance;
                }

                // 3) Add to placeholders
                placeholders["{{5_CY}}"] = cyTotal.ToString("#,##0");
                placeholders["{{5_PY}}"] = pyTotal.ToString("#,##0");

                PXTrace.WriteInformation($"Wang Tunai dan Baki di Bank CY Total: {cyTotal}");
                PXTrace.WriteInformation($"Wang Tunai dan Baki di Bank PY Total: {pyTotal}");

                return placeholders;
            }


            #endregion

            #region XX: Pendahuluan Kepada Subsidari

            public Dictionary<string, string> PendahuluanSubsidari(Dictionary<string, string> placeholders,Dictionary<string, FinancialPeriodData> cyComposite,Dictionary<string, FinancialPeriodData> pyComposite)
            {
                // Define specific keys to include (account-subaccount-branch)
                var targetKeys = new List<(string Account, string Subaccount, string Branch)>
                {
                    ("A73102", "MKMG-XXX", "MIP"),
                    ("A73102", "MS2X-XXX", "MW8"),
                    ("A73102", "UKKM-X23", "MIP")
                };

                decimal totalCY = 0;
                decimal totalPY = 0;

                // CY Loop
                foreach (var kvp in cyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 5) continue;

                    string accountId = parts[0];
                    string subId = parts[1];
                    string branchId = parts[2];

                    if (targetKeys.Any(t => t.Account == accountId && t.Subaccount == subId && t.Branch == branchId))
                    {
                        totalCY += kvp.Value.EndingBalance;
                        PXTrace.WriteInformation($"[CY] Matched: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                    }
                }

                // PY Loop
                foreach (var kvp in pyComposite)
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length < 5) continue;

                    string accountId = parts[0];
                    string subId = parts[1];
                    string branchId = parts[2];

                    if (targetKeys.Any(t => t.Account == accountId && t.Subaccount == subId && t.Branch == branchId))
                    {
                        totalPY += kvp.Value.EndingBalance;
                        PXTrace.WriteInformation($"[PY] Matched: {kvp.Key}, Balance: {kvp.Value.EndingBalance}");
                    }
                }

                PXTrace.WriteInformation($"Pendahuluan Subsidari CY: {totalCY}");
                PXTrace.WriteInformation($"Pendahuluan Subsidari PY: {totalPY}");

                placeholders["{{XX_detail_CY}}"] = totalCY.ToString("#,##0");
                placeholders["{{XX_detail_PY}}"] = totalPY.ToString("#,##0");

                return placeholders;
            }


            #endregion

            #endregion


        }

        public class TESTPlaceholderCalculator : IPlaceholderCalculator
        {
            public Dictionary<string, string> CalculatePlaceholders(FinancialApiData cyData, FinancialApiData pyData, Dictionary<string, string> basePlaceholders)
            {
                basePlaceholders = Penghutang(basePlaceholders);
                return basePlaceholders;
            }

            public Dictionary<string, string> CalculateCompositePlaceholders(FinancialApiData cyData, FinancialApiData pyData, Dictionary<string, string> basePlaceholders)
            {
                PXTrace.WriteInformation("TEST.CalculateCompositePlaceholders() - Handling composite logic.");
                return basePlaceholders;
            }

            #region Calculation Methods

            #region 1. Penghutang
            public Dictionary<string, string> Penghutang(Dictionary<string, string> placeholders)
            {

                return placeholders;
            }



            #endregion


            #endregion

        }

    }
}



        

       
