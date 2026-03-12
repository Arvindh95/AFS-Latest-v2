using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FinancialReport.Helper;

namespace FinancialReport.Services
{
    /// <summary>
    /// Builds a structured markdown string from calculated financial report data.
    /// The markdown is used as the input_text payload for the Alai presentation API.
    ///
    /// Output format:
    ///   # [Title]
    ///   [Description]
    ///   Organization | Branch | Ledger | Period context
    ///   ---
    ///   ## [Line Description]
    ///   - CY value, PM value, MoM change, PY value, YoY change
    ///   (repeated per visible line item across all definitions)
    /// </summary>
    public class MarkdownBuilderService
    {
        private static readonly string[] MonthNames =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };

        /// <summary>
        /// Builds the complete markdown string from the report header, line items, and calculated results.
        /// </summary>
        /// <param name="report">The FLRTFinancialReport record (provides period, org, branch, ledger, title, description).</param>
        /// <param name="definitions">Each definition paired with its ordered line items.</param>
        /// <param name="results">Dictionary from ReportCalculationEngine.CalculateAll() — keyed PREFIX_LINECODE_CY/PM/PY.</param>
        public string Build(
            FLRTFinancialReport report,
            List<(ReportCalculationEngine.DefinitionLink DefLink, List<FLRTReportLineItem> Items)> definitions,
            Dictionary<string, string> results)
        {
            // ── Derive period labels ───────────────────────────────────────────────
            int month = int.TryParse(report.FinancialMonth, out int m) ? m : 12;
            int year  = int.TryParse(report.CurrYear,       out int y) ? y : DateTime.Now.Year;

            int prevMonth     = month == 1 ? 12 : month - 1;
            int prevMonthYear = month == 1 ? year - 1 : year;

            string cyLabel = $"{MonthNames[month - 1]} {year}";
            string pmLabel = $"{MonthNames[prevMonth - 1]} {prevMonthYear}";
            string pyLabel = $"{MonthNames[month - 1]} {year - 1}";

            var sb = new StringBuilder();

            // ── Header ─────────────────────────────────────────────────────────────
            string title = !string.IsNullOrWhiteSpace(report.PresentationTitle)
                ? report.PresentationTitle
                : $"{cyLabel} Financial Report";

            sb.AppendLine($"# {title}");

            if (!string.IsNullOrWhiteSpace(report.PresentationDescription))
                sb.AppendLine(report.PresentationDescription);

            sb.AppendLine();
            sb.AppendLine($"Organization: {report.Organization ?? "N/A"} | Branch: {report.Branch ?? "N/A"} | Ledger: {report.Ledger ?? "N/A"}");
            sb.AppendLine($"Reporting Period: {cyLabel}");
            sb.AppendLine($"Month-over-Month: {cyLabel} vs {pmLabel}");
            sb.AppendLine($"Year-over-Year: {cyLabel} vs {pyLabel}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // ── Line items ─────────────────────────────────────────────────────────
            foreach (var (defLink, items) in definitions)
            {
                var visibleItems = items.Where(l => l.IsVisible == true).ToList();
                if (!visibleItems.Any()) continue;

                foreach (var line in visibleItems)
                {
                    string keyBase = $"{defLink.Prefix}_{line.LineCode}";
                    string cyVal   = GetValue(results, keyBase + "_" + Constants.CurrentYearSuffix);
                    string pmVal   = GetValue(results, keyBase + "_" + Constants.PreviousMonthSuffix);
                    string pyVal   = GetValue(results, keyBase + "_" + Constants.PreviousYearSuffix);

                    string label = !string.IsNullOrWhiteSpace(line.Description)
                        ? line.Description
                        : line.LineCode;

                    decimal cyDec = ParseDecimal(cyVal);
                    decimal pmDec = ParseDecimal(pmVal);
                    decimal pyDec = ParseDecimal(pyVal);

                    decimal momChange = cyDec - pmDec;
                    decimal yoyChange = cyDec - pyDec;

                    // Credit-balance accounts (equity, liability, expenses) are stored as negative.
                    // For those, a positive raw change means the balance shrank — which is a decrease
                    // in real terms. Negate the displayed change so the narrative reads correctly:
                    // e.g. Equity (9.5M) vs (10.2M): raw change = +641K → displayed as -641K (-6.3%).
                    bool isCredit = cyDec < 0;
                    decimal displayMom = isCredit ? -momChange : momChange;
                    decimal displayYoy = isCredit ? -yoyChange : yoyChange;

                    decimal momBase = pmDec != 0 ? Math.Abs(pmDec) : 0;
                    decimal yoyBase = pyDec != 0 ? Math.Abs(pyDec) : 0;

                    string momPct = momBase != 0 ? $" ({displayMom / momBase * 100:N1}%)" : "";
                    string yoyPct = yoyBase != 0 ? $" ({displayYoy / yoyBase * 100:N1}%)" : "";

                    sb.AppendLine($"## {label}");
                    sb.AppendLine($"- {cyLabel}: {cyVal}");
                    sb.AppendLine($"- {pmLabel}: {pmVal}");
                    sb.AppendLine($"- Month-on-Month Change: {displayMom:N0}{momPct}");
                    sb.AppendLine($"- {pyLabel}: {pyVal}");
                    sb.AppendLine($"- Year-on-Year Change: {displayYoy:N0}{yoyPct}");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private string GetValue(Dictionary<string, string> results, string key)
        {
            if (results != null && results.TryGetValue(key, out string val) && !string.IsNullOrEmpty(val))
                return val;
            return "0";
        }

        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0m;
            // Strip formatting characters that may come from the engine (commas, spaces)
            string cleaned = value.Replace(",", "").Replace(" ", "").Trim();
            // Handle accounting format: (1234) means -1234
            if (cleaned.StartsWith("(") && cleaned.EndsWith(")"))
                cleaned = "-" + cleaned.Substring(1, cleaned.Length - 2);
            return decimal.TryParse(cleaned, out decimal result) ? result : 0m;
        }
    }
}
