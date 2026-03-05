using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FinancialReport.Helper;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;

namespace FinancialReport.Services
{
    /// <summary>
    /// The core financial report calculation engine.
    ///
    /// Given a Report Definition (set of line items with account ranges, sign rules,
    /// subtotal groupings, and formulas) and pre-fetched GL data, this engine:
    ///
    ///   1. Iterates line items in SortOrder sequence
    ///   2. For ACCOUNT lines  → sums GL accounts in the range, applies sign rule
    ///   3. For SUBTOTAL lines → sums all child lines (those pointing to this LineCode as parent)
    ///   4. For CALCULATED lines → evaluates a formula expression referencing other LineCodes
    ///   5. Produces a flat Dictionary of {{LINECODE_CY}} / {{LINECODE_PY}} placeholder values
    ///      ready to be inserted directly into the Word template.
    ///
    /// The engine is stateless — create a new instance per report generation.
    /// </summary>
    public class ReportCalculationEngine
    {
        private readonly PXGraph _graph;
        private readonly RoundingSettings _rounding;

        // Holds calculated values as the engine processes lines in order.
        // Key = LineCode (uppercase), Value = calculated decimal
        private readonly Dictionary<string, decimal> _cyValues  = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, decimal> _pyValues  = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        public ReportCalculationEngine(PXGraph graph, RoundingSettings rounding = null)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            _rounding = rounding ?? new RoundingSettings();
        }

        /// <summary>
        /// Main entry point. Calculates all line values for a report definition
        /// and returns a placeholder dictionary ready for Word template population.
        /// </summary>
        /// <param name="definitionID">The Report Definition to use.</param>
        /// <param name="cyData">GL data for the current year period.</param>
        /// <param name="pyData">GL data for the prior year period.</param>
        /// <returns>
        /// Dictionary keyed by placeholder name (e.g. "CASH_CY", "NET_INCOME_PY").
        /// Values are formatted strings (e.g. "50,000" or "(30,000)" for negatives).
        /// </returns>
        public Dictionary<string, string> Calculate(
            int definitionID,
            FinancialApiData cyData,
            FinancialApiData pyData)
        {
            _cyValues.Clear();
            _pyValues.Clear();

            // Load all line items for this definition, ordered by SortOrder
            var lineItems = SelectFrom<FLRTReportLineItem>
                .Where<FLRTReportLineItem.definitionID.IsEqual<@P.AsInt>>
                .OrderBy<FLRTReportLineItem.sortOrder.Asc>
                .View.Select(_graph, definitionID)
                .RowCast<FLRTReportLineItem>()
                .ToList();

            if (!lineItems.Any())
            {
                PXTrace.WriteWarning($"ReportCalculationEngine: No line items found for DefinitionID {definitionID}.");
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            PXTrace.WriteInformation($"ReportCalculationEngine: Processing {lineItems.Count} line items for DefinitionID {definitionID}.");

            // Process each line in SortOrder — order matters because subtotals depend on account lines
            foreach (var line in lineItems)
            {
                if (string.IsNullOrWhiteSpace(line.LineCode)) continue;
                if (line.LineType == FLRTReportLineItem.LineItemType.Heading) continue;

                decimal cyValue = 0m;
                decimal pyValue = 0m;

                switch (line.LineType)
                {
                    case FLRTReportLineItem.LineItemType.Account:
                        cyValue = CalculateAccountLine(line, cyData);
                        pyValue = CalculateAccountLine(line, pyData);
                        break;

                    case FLRTReportLineItem.LineItemType.Subtotal:
                        cyValue = CalculateSubtotal(line.LineCode, _cyValues);
                        pyValue = CalculateSubtotal(line.LineCode, _pyValues);
                        break;

                    case FLRTReportLineItem.LineItemType.Calculated:
                        cyValue = EvaluateFormula(line.Formula, _cyValues);
                        pyValue = EvaluateFormula(line.Formula, _pyValues);
                        break;
                }

                _cyValues[line.LineCode] = cyValue;
                _pyValues[line.LineCode] = pyValue;

                PXTrace.WriteInformation($"  [{line.SortOrder:D4}] {line.LineCode,-30} CY={cyValue,15:#,##0.##}  PY={pyValue,15:#,##0.##}");
            }

            // Build the final placeholder dictionary
            return BuildPlaceholderMap(lineItems);
        }

        // ─────────────────────────────────────────────────────────────────
        // ACCOUNT LINE CALCULATION
        // ─────────────────────────────────────────────────────────────────

        private decimal CalculateAccountLine(FLRTReportLineItem line, FinancialApiData data)
        {
            if (data == null) return 0m;
            if (string.IsNullOrWhiteSpace(line.AccountFrom) || string.IsNullOrWhiteSpace(line.AccountTo))
                return 0m;

            // When any per-line dimension filter is set, use the raw DetailRows so we can
            // match on Subaccount / BranchID / OrganizationID per row.
            bool hasFilter = !string.IsNullOrWhiteSpace(line.SubaccountFilter)
                          || !string.IsNullOrWhiteSpace(line.BranchFilter)
                          || !string.IsNullOrWhiteSpace(line.OrganizationFilter)
                          || !string.IsNullOrWhiteSpace(line.LedgerFilter);

            if (hasFilter && data.DetailRows != null && data.DetailRows.Count > 0)
                return CalculateAccountLineFromDetail(line, data.DetailRows);

            // ── Default path: aggregated AccountData (same as before) ──
            if (data.AccountData == null) return 0m;

            decimal total = 0m;
            int matched = 0;

            foreach (var kvp in data.AccountData)
            {
                string accountCode = kvp.Key;
                FinancialPeriodData periodData = kvp.Value;

                // Filter by account range
                if (!IsAccountInRange(accountCode, line.AccountFrom, line.AccountTo))
                    continue;

                // Filter by account type if specified (uses the Type field from the GI)
                if (!string.IsNullOrWhiteSpace(line.AccountTypeFilter)
                    && !string.Equals(periodData.AccountType, line.AccountTypeFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Get the raw balance for the specified balance type
                decimal rawValue = GetBalanceByType(periodData, line.BalanceType);

                // Apply sign normalization based on account type from GI
                decimal signCorrected = ApplyAccountTypeSign(rawValue, periodData.AccountType);

                // Apply the line-level sign rule (FLIP allows accountant to override)
                decimal finalValue = line.SignRule == FLRTReportLineItem.SignRuleValue.Flip
                    ? signCorrected * -1
                    : signCorrected;

                total += finalValue;
                matched++;
            }

            if (matched > 0)
                PXTrace.WriteInformation($"    Account range {line.AccountFrom}:{line.AccountTo} matched {matched} accounts → {total:#,##0.##}");

            return total;
        }

        /// <summary>
        /// Filtered variant of account calculation — iterates the per-row DetailRows
        /// and applies optional Subaccount / Branch / Organization filters.
        /// </summary>
        private decimal CalculateAccountLineFromDetail(FLRTReportLineItem line, List<FinancialPeriodData> detailRows)
        {
            decimal total = 0m;
            int matched = 0;

            foreach (var row in detailRows)
            {
                if (!IsAccountInRange(row.Account, line.AccountFrom, line.AccountTo))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.AccountTypeFilter)
                    && !string.Equals(row.AccountType, line.AccountTypeFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.SubaccountFilter)
                    && !string.Equals(row.Subaccount, line.SubaccountFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.BranchFilter)
                    && !string.Equals(row.BranchID, line.BranchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.OrganizationFilter)
                    && !string.Equals(row.OrganizationID, line.OrganizationFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(line.LedgerFilter)
                    && !string.Equals(row.Ledger, line.LedgerFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                decimal rawValue      = GetBalanceByType(row, line.BalanceType);
                decimal signCorrected = ApplyAccountTypeSign(rawValue, row.AccountType);
                decimal finalValue    = line.SignRule == FLRTReportLineItem.SignRuleValue.Flip
                                        ? signCorrected * -1
                                        : signCorrected;

                total += finalValue;
                matched++;
            }

            PXTrace.WriteInformation($"    Account range {line.AccountFrom}:{line.AccountTo} [filtered] matched {matched} of {detailRows.Count} detail rows → {total:#,##0.##}");
            if (matched == 0)
            {
                // Log filter values and a sample of what's in the data for debugging
                PXTrace.WriteWarning($"    Filters: Sub='{line.SubaccountFilter}' Branch='{line.BranchFilter}' Org='{line.OrganizationFilter}' Ledger='{line.LedgerFilter}'");
                var inRange = detailRows.Where(r => IsAccountInRange(r.Account, line.AccountFrom, line.AccountTo)).Take(3).ToList();
                foreach (var r in inRange)
                    PXTrace.WriteWarning($"    Sample row: Acct={r.Account} Sub='{r.Subaccount}' Branch='{r.BranchID}' Org='{r.OrganizationID}' Ledger='{r.Ledger}' EndBal={r.EndingBalance}");
            }

            return total;
        }

        // ─────────────────────────────────────────────────────────────────
        // SIGN NORMALIZATION
        // Converts raw GL credit-normal balances to presentation-positive values
        // based on the AccountType returned by the TrialBalance GI.
        // ─────────────────────────────────────────────────────────────────

        private decimal ApplyAccountTypeSign(decimal rawValue, string accountType)
        {
            // Credit-normal accounts store natural balances as negatives in the GL.
            // Flip them so they appear positive on the financial statement.
            switch (accountType?.Trim())
            {
                case FLRTReportLineItem.AccountTypeValue.Liability: // L
                case FLRTReportLineItem.AccountTypeValue.Income:    // I
                case FLRTReportLineItem.AccountTypeValue.Equity:    // Q
                    return rawValue * -1;

                case FLRTReportLineItem.AccountTypeValue.Asset:     // A
                case FLRTReportLineItem.AccountTypeValue.Expense:   // E
                default:
                    return rawValue;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // BALANCE TYPE SELECTOR
        // ─────────────────────────────────────────────────────────────────

        private decimal GetBalanceByType(FinancialPeriodData data, string balanceType)
        {
            switch (balanceType?.ToUpper())
            {
                case FLRTReportLineItem.BalanceTypeValue.Beginning: return data.BeginningBalance;
                case FLRTReportLineItem.BalanceTypeValue.Debit:     return data.Debit;
                case FLRTReportLineItem.BalanceTypeValue.Credit:    return data.Credit;
                case FLRTReportLineItem.BalanceTypeValue.Movement:  return data.Debit - data.Credit;
                case FLRTReportLineItem.BalanceTypeValue.Ending:
                default:
                    return data.EndingBalance;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // SUBTOTAL CALCULATION
        // Sums all lines that declared this LineCode as their ParentLineCode
        // ─────────────────────────────────────────────────────────────────

        private decimal CalculateSubtotal(string subtotalLineCode, Dictionary<string, decimal> values)
        {
            // Find all line items in this definition that belong to this subtotal group
            var childLines = SelectFrom<FLRTReportLineItem>
                .Where<FLRTReportLineItem.parentLineCode.IsEqual<@P.AsString>>
                .View.Select(_graph, subtotalLineCode)
                .RowCast<FLRTReportLineItem>()
                .ToList();

            decimal total = 0m;
            foreach (var child in childLines)
            {
                if (values.TryGetValue(child.LineCode, out decimal childValue))
                    total += childValue;
            }

            return total;
        }

        // ─────────────────────────────────────────────────────────────────
        // FORMULA EVALUATOR
        // Handles expressions like: REVENUE - TOTAL_EXPENSES + OTHER_INCOME
        // Supports: +, -, *, / operators and parentheses
        // Operands must be LineCodes already calculated (earlier SortOrder)
        // ─────────────────────────────────────────────────────────────────

        private decimal EvaluateFormula(string formula, Dictionary<string, decimal> values)
        {
            if (string.IsNullOrWhiteSpace(formula)) return 0m;

            try
            {
                // Tokenize: split on operators while keeping them
                // Handles: REVENUE - TOTAL_EXPENSES * 1.5 + (OTHER_INCOME - ADJUSTMENTS)
                var tokens = TokenizeFormula(formula);
                return EvaluateTokens(tokens, values);
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"ReportCalculationEngine: Formula evaluation failed for '{formula}': {ex.Message}");
                return 0m;
            }
        }

        private List<string> TokenizeFormula(string formula)
        {
            // Matches line codes (alphanumeric + underscore), numbers, operators, and parentheses
            var tokenRegex = new Regex(@"([A-Z][A-Z0-9_]*)|([\d]+\.?[\d]*)|([\+\-\*\/\(\)])", RegexOptions.IgnoreCase);
            var tokens = new List<string>();

            foreach (Match m in tokenRegex.Matches(formula.Trim()))
                tokens.Add(m.Value);

            return tokens;
        }

        // Recursive descent parser: handles operator precedence and parentheses
        private decimal EvaluateTokens(List<string> tokens, Dictionary<string, decimal> values)
        {
            int pos = 0;
            return ParseExpression(tokens, ref pos, values);
        }

        private decimal ParseExpression(List<string> tokens, ref int pos, Dictionary<string, decimal> values)
        {
            decimal result = ParseTerm(tokens, ref pos, values);

            while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
            {
                string op = tokens[pos++];
                decimal right = ParseTerm(tokens, ref pos, values);
                result = op == "+" ? result + right : result - right;
            }

            return result;
        }

        private decimal ParseTerm(List<string> tokens, ref int pos, Dictionary<string, decimal> values)
        {
            decimal result = ParseFactor(tokens, ref pos, values);

            while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
            {
                string op = tokens[pos++];
                decimal right = ParseFactor(tokens, ref pos, values);
                if (op == "/" && right != 0)
                    result = result / right;
                else if (op == "*")
                    result = result * right;
            }

            return result;
        }

        private decimal ParseFactor(List<string> tokens, ref int pos, Dictionary<string, decimal> values)
        {
            if (pos >= tokens.Count) return 0m;

            string token = tokens[pos];

            // Parenthesized sub-expression
            if (token == "(")
            {
                pos++; // consume "("
                decimal inner = ParseExpression(tokens, ref pos, values);
                if (pos < tokens.Count && tokens[pos] == ")")
                    pos++; // consume ")"
                return inner;
            }

            // Unary minus
            if (token == "-")
            {
                pos++;
                return -ParseFactor(tokens, ref pos, values);
            }

            pos++;

            // Numeric literal
            if (decimal.TryParse(token, out decimal numericValue))
                return numericValue;

            // LineCode reference — look up already-calculated value
            string lineCode = token.ToUpper();
            if (values.TryGetValue(lineCode, out decimal lineValue))
                return lineValue;

            PXTrace.WriteWarning($"ReportCalculationEngine: Formula references unknown LineCode '{token}'. " +
                                  "Ensure it is defined earlier in SortOrder. Defaulting to 0.");
            return 0m;
        }

        // ─────────────────────────────────────────────────────────────────
        // ACCOUNT RANGE COMPARISON
        // Reuses the same smart alphanumeric comparison from FinancialDataService
        // ─────────────────────────────────────────────────────────────────

        private bool IsAccountInRange(string account, string from, string to)
        {
            return CompareAccountCodes(account, from) >= 0
                && CompareAccountCodes(account, to)   <= 0;
        }

        private int CompareAccountCodes(string a, string b)
        {
            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 0;
            if (string.IsNullOrEmpty(a)) return -1;
            if (string.IsNullOrEmpty(b)) return 1;

            bool segA = a.Contains('-');
            bool segB = b.Contains('-');

            if (segA && segB)
            {
                var segsA = a.Split('-');
                var segsB = b.Split('-');
                int min = Math.Min(segsA.Length, segsB.Length);
                for (int s = 0; s < min; s++)
                {
                    bool numA = int.TryParse(segsA[s], out int nA);
                    bool numB = int.TryParse(segsB[s], out int nB);
                    int cmp = (numA && numB)
                        ? nA.CompareTo(nB)
                        : string.Compare(segsA[s], segsB[s], StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                }
                return segsA.Length.CompareTo(segsB.Length);
            }

            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    long n1 = 0; while (i < a.Length && char.IsDigit(a[i])) n1 = n1 * 10 + (a[i++] - '0');
                    long n2 = 0; while (j < b.Length && char.IsDigit(b[j])) n2 = n2 * 10 + (b[j++] - '0');
                    if (n1 != n2) return n1.CompareTo(n2);
                }
                else
                {
                    int cmp = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                    if (cmp != 0) return cmp;
                    i++; j++;
                }
            }

            return a.Length.CompareTo(b.Length);
        }

        // ─────────────────────────────────────────────────────────────────
        // BUILD PLACEHOLDER MAP
        // Converts the internal decimal values into formatted strings
        // keyed by {{LINECODE_CY}} / {{LINECODE_PY}} placeholder names
        // ─────────────────────────────────────────────────────────────────

        private Dictionary<string, string> BuildPlaceholderMap(List<FLRTReportLineItem> lineItems)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lineItems)
            {
                if (string.IsNullOrWhiteSpace(line.LineCode)) continue;

                string cyKey = $"{line.LineCode}_{Constants.CurrentYearSuffix}";
                string pyKey = $"{line.LineCode}_{Constants.PreviousYearSuffix}";

                // Heading lines and non-visible lines get empty string (not "0")
                if (line.LineType == FLRTReportLineItem.LineItemType.Heading || line.IsVisible == false)
                {
                    map[cyKey] = string.Empty;
                    map[pyKey] = string.Empty;
                    continue;
                }

                decimal cyVal = _cyValues.TryGetValue(line.LineCode, out decimal cy) ? cy : 0m;
                decimal pyVal = _pyValues.TryGetValue(line.LineCode, out decimal py) ? py : 0m;

                map[cyKey] = FormatFinancialValue(cyVal);
                map[pyKey] = FormatFinancialValue(pyVal);
            }

            return map;
        }

        /// <summary>
        /// Formats a decimal for financial report presentation.
        /// Applies rounding level (Units/Thousands/Millions) and decimal places.
        /// Positive values: "1,234,567"
        /// Negative values: "(1,234,567)"  — accounting bracket notation
        /// Zero: "-"  (dash, standard financial statement practice)
        /// </summary>
        private string FormatFinancialValue(decimal value)
        {
            decimal scaled = ApplyRounding(value);

            if (scaled == 0m) return "-";

            string format = BuildFormatString();
            if (scaled < 0m) return $"({Math.Abs(scaled).ToString(format)})";
            return scaled.ToString(format);
        }

        private decimal ApplyRounding(decimal value)
        {
            switch (_rounding.RoundingLevel)
            {
                case FLRTReportDefinition.RoundingLevelType.Thousands:
                    value = value / 1000m;
                    break;
                case FLRTReportDefinition.RoundingLevelType.Millions:
                    value = value / 1000000m;
                    break;
            }

            return Math.Round(value, _rounding.DecimalPlaces, MidpointRounding.AwayFromZero);
        }

        private string BuildFormatString()
        {
            if (_rounding.DecimalPlaces <= 0)
                return "#,##0";
            return "#,##0." + new string('0', _rounding.DecimalPlaces);
        }
    }
}