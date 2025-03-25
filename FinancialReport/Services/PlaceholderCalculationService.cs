using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinancialReport.Services
{
    public class PlaceholderCalculationService
    {
        #region 1. Susutnilai Loji dan Peralatan

        /// <summary>
        /// Calculates:
        ///   ((debits B53 - credits B53) - (debits B539 - credits B539))
        /// for both CY and PY.
        /// </summary>
        public Dictionary<string, string> Susutnilai_Loji_dan_Peralatan(FinancialApiData cyData, FinancialApiData pyData)
        {
            var placeholders = new Dictionary<string, string>();

            // -------------------------------------------------------
            // CURRENT YEAR (CY)
            // -------------------------------------------------------
            decimal debitSumB53_CY = 0m;
            decimal creditSumB53_CY = 0m;
            decimal debitSumB539_CY = 0m;
            decimal creditSumB539_CY = 0m;

            foreach (var kvp in cyData.AccountData)
            {
                string acctId = kvp.Key;
                FinancialPeriodData data = kvp.Value;

                // If it starts with B53 => also accumulates B539
                if (acctId.StartsWith("B53"))
                {
                    debitSumB53_CY += data.Debit;
                    creditSumB53_CY += data.Credit;
                }

                // If it starts with B539 => also accumulates in B539
                if (acctId.StartsWith("B539"))
                {
                    debitSumB539_CY += data.Debit;
                    creditSumB539_CY += data.Credit;
                }
            }

            // So an account "B539" is included in both B53 sums and B539 sums.
            // Now compute your final formula:
            decimal resultCY = (debitSumB53_CY - creditSumB53_CY)
                             - (debitSumB539_CY - creditSumB539_CY);

            placeholders["{{1_CY}}"] = resultCY.ToString("#,##0");


            // -------------------------------------------------------
            // PREVIOUS YEAR (PY)
            // -------------------------------------------------------
            decimal debitSumB53_PY = 0m;
            decimal creditSumB53_PY = 0m;
            decimal debitSumB539_PY = 0m;
            decimal creditSumB539_PY = 0m;

            foreach (var kvp in pyData.AccountData)
            {
                string acctId = kvp.Key;
                FinancialPeriodData data = kvp.Value;

                if (acctId.StartsWith("B53"))
                {
                    debitSumB53_PY += data.Debit;
                    creditSumB53_PY += data.Credit;
                }

                if (acctId.StartsWith("B539"))
                {
                    debitSumB539_PY += data.Debit;
                    creditSumB539_PY += data.Credit;
                }
            }

            decimal resultPY = (debitSumB53_PY - creditSumB53_PY)
                             - (debitSumB539_PY - creditSumB539_PY);

            placeholders["{{1_PY}}"] = resultPY.ToString("#,##0");

            return placeholders;
        }

        #endregion

        #region 2. Lebihan/(Kurangan) Sebelum Cukai
        /// <summary>
        /// Calculates Lebihan/(Kurangan) Sebelum Cukai
        /// ({{Sum1_H_CY}}*-1) - {{Sum1_B_CY}} + {{Sum3_B50_CY}} + {{Sum3_B59_CY}}
        /// for both CY and PY.
        /// </summary>

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
        /// <summary>
        /// Calculates Pelunasan Aset Tak Ketara
        /// </summary>
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

            decimal totalCY = (e13000CY + e13101CY + e13201CY + e14101CY)* -1;
            placeholders["{{10_CY}}"] = totalCY.ToString("#,##0");

            // ---------- PY (optional) ----------
            decimal e13000PY = placeholders.ContainsKey("{{E13000_Jan1_PY}}") && decimal.TryParse(placeholders["{{E13000_Jan1_PY}}"], out var v1PY) ? v1PY : 0;
            decimal e13101PY = placeholders.ContainsKey("{{E13101_Jan1_PY}}") && decimal.TryParse(placeholders["{{E13101_Jan1_PY}}"], out var v2PY) ? v2PY : 0;
            decimal e13201PY = placeholders.ContainsKey("{{E13201_Jan1_PY}}") && decimal.TryParse(placeholders["{{E13201_Jan1_PY}}"], out var v3PY) ? v3PY : 0;
            decimal e14101PY = placeholders.ContainsKey("{{E14101_Jan1_PY}}") && decimal.TryParse(placeholders["{{E14101_Jan1_PY}}"], out var v4PY) ? v4PY : 0;

            decimal totalPY = (e13000PY + e13101PY + e13201PY + e14101PY)* -1;
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

            decimal resultCY = ((d34CY + d35CY + d36CY + d37CY + d38CY) - (b34CY + b35CY + b36CY + b37CY + b38CY))* -1;

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

            decimal resultPY = ((d34PY + d35PY + d36PY + d37PY + d38PY) - (b34PY + b35PY + b36PY + b37PY + b38PY))* -1;

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

            decimal resultCY = ((d13000CY + d13101CY + d13102CY + d13201CY) - (c13000CY + c13101CY + c13102CY + c13201CY))* -1;

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

            decimal resultPY = ((d13000PY + d13101PY + d13102PY + d13201PY) - (c13000PY + c13101PY + c13102PY + c13201PY))* -1;

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

            decimal resultCY = (b50CY + b59CY)* -1;
            placeholders["{{26_CY}}"] = resultCY.ToString("#,##0");

            // ---------- PY ----------
            decimal b50PY = placeholders.ContainsKey("{{Sum3_B50_PY}}") && decimal.TryParse(placeholders["{{Sum3_B50_PY}}"], out var v1PY) ? v1PY : 0;
            decimal b59PY = placeholders.ContainsKey("{{Sum3_B59_PY}}") && decimal.TryParse(placeholders["{{Sum3_B59_PY}}"], out var v2PY) ? v2PY : 0;

            decimal resultPY = (b50PY + b59PY)* -1;
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

    }
}
