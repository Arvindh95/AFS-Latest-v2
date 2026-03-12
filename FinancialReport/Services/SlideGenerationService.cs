using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FinancialReport.Helper;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;

namespace FinancialReport.Services
{
    /// <summary>
    /// Fetches GL data, runs the calculation engine, and builds a markdown string
    /// that can be passed to a presentation generation API (e.g. Gamma).
    ///
    /// BuildMarkdownPreview() runs the full data pipeline and saves the markdown
    /// as a .txt file attachment so the user can review it before generating slides.
    /// </summary>
    public class SlideGenerationService
    {
        private readonly FLRTFinancialReportMaint _graph;
        private readonly FLRTFinancialReport _currentRecord;
        private readonly AuthService _authService;
        private readonly string _tenantName;
        private readonly FileService _fileService;

        public SlideGenerationService(
            FLRTFinancialReportMaint graph,
            FLRTFinancialReport record,
            AuthService authService,
            string tenantName)
        {
            _graph         = graph       ?? throw new ArgumentNullException(nameof(graph));
            _currentRecord = record      ?? throw new ArgumentNullException(nameof(record));
            _authService   = authService ?? throw new ArgumentNullException(nameof(authService));
            _tenantName    = tenantName  ?? throw new ArgumentNullException(nameof(tenantName));
            _fileService   = new FileService(_graph);
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC PROPERTIES
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Set after BuildMarkdownPreview() completes. Callers can read this to
        /// persist the markdown text to the record's PresentationMarkdown field.
        /// </summary>
        public string LastGeneratedMarkdown { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        // PUBLIC ENTRY POINTS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        public Guid BuildMarkdownPreview()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var (markdown, _) = PrepareMarkdown(stopwatch);
            LastGeneratedMarkdown = markdown;

            // Write full markdown to trace for additional visibility
            PXTrace.WriteInformation($"[Slide] Markdown preview:\n{markdown}");

            // Save as downloadable .txt attachment
            byte[] txtBytes = Encoding.UTF8.GetBytes(markdown);
            string fileName = $"{_currentRecord.ReportCD}_MarkdownPreview_{DateTime.Now:yyyyMMdd_HHmm}.txt";
            Guid fileID = _fileService.SaveGeneratedDocument(fileName, txtBytes, _currentRecord);

            stopwatch.Stop();
            PXTrace.WriteInformation($"[Slide] Markdown preview saved with FileID {fileID} — total {stopwatch.ElapsedMilliseconds}ms");

            return fileID;
        }

        // ─────────────────────────────────────────────────────────────────────
        // SHARED PIPELINE (steps 1–7)
        // ─────────────────────────────────────────────────────────────────────

        private (string Markdown, List<(ReportCalculationEngine.DefinitionLink DefLink, List<FLRTReportLineItem> Items)> Definitions) PrepareMarkdown(
            System.Diagnostics.Stopwatch stopwatch)
        {
            // ── 1. Load definition links ───────────────────────────────────────────
            GIColumnMapping columnMapping = null;
            var definitionLinks = new List<ReportCalculationEngine.DefinitionLink>();

            var linkedDefs = SelectFrom<FLRTReportDefinitionLink>
                .InnerJoin<FLRTReportDefinition>
                    .On<FLRTReportDefinition.definitionID.IsEqual<FLRTReportDefinitionLink.definitionID>>
                .Where<FLRTReportDefinitionLink.reportID.IsEqual<@P.AsInt>>
                .OrderBy<FLRTReportDefinitionLink.displayOrder.Asc>
                .View.Select(_graph, _currentRecord.ReportID)
                .Cast<PXResult<FLRTReportDefinitionLink, FLRTReportDefinition>>()
                .ToList();

            if (linkedDefs.Any())
            {
                var firstDef = linkedDefs.First().GetItem<FLRTReportDefinition>();
                columnMapping = GIColumnMapping.FromDefinition(firstDef);

                foreach (var result in linkedDefs)
                {
                    var def = result.GetItem<FLRTReportDefinition>();
                    definitionLinks.Add(new ReportCalculationEngine.DefinitionLink
                    {
                        DefinitionID = def.DefinitionID!.Value,
                        Prefix       = def.DefinitionPrefix,
                        Rounding     = RoundingSettings.FromDefinition(def)
                    });
                }
            }
            else if (_currentRecord.DefinitionID != null)
            {
                var reportDef = SelectFrom<FLRTReportDefinition>
                    .Where<FLRTReportDefinition.definitionID.IsEqual<@P.AsInt>>
                    .View.Select(_graph, _currentRecord.DefinitionID)
                    .TopFirst;

                if (reportDef != null)
                {
                    columnMapping = GIColumnMapping.FromDefinition(reportDef);
                    definitionLinks.Add(new ReportCalculationEngine.DefinitionLink
                    {
                        DefinitionID = reportDef.DefinitionID!.Value,
                        Prefix       = reportDef.DefinitionPrefix,
                        Rounding     = RoundingSettings.FromDefinition(reportDef)
                    });
                }
            }

            if (!definitionLinks.Any())
                throw new PXException(Messages.NoDefinitionsLinked);

            // ── 2. Collect line items per definition ───────────────────────────────
            var definitionsWithItems = new List<(ReportCalculationEngine.DefinitionLink DefLink, List<FLRTReportLineItem> Items)>();

            foreach (var defLink in definitionLinks)
            {
                var items = SelectFrom<FLRTReportLineItem>
                    .Where<FLRTReportLineItem.definitionID.IsEqual<@P.AsInt>>
                    .OrderBy<FLRTReportLineItem.sortOrder.Asc>
                    .View.Select(_graph, defLink.DefinitionID)
                    .RowCast<FLRTReportLineItem>()
                    .ToList();

                definitionsWithItems.Add((defLink, items));
            }

            // ── 3. Validate descriptions ───────────────────────────────────────────
            var missingDesc = definitionsWithItems
                .SelectMany(d => d.Items)
                .Where(li => li.IsVisible == true && string.IsNullOrWhiteSpace(li.Description))
                .Select(li => li.LineCode)
                .ToList();

            if (missingDesc.Any())
                throw new PXException(Messages.VisibleLineItemsMissingDescriptions, string.Join(", ", missingDesc));

            // ── 4. Set up periods ──────────────────────────────────────────────────
            string currYear      = _currentRecord.CurrYear ?? DateTime.Now.ToString("yyyy");
            string selectedMonth = _currentRecord.FinancialMonth ?? "12";
            int currYearInt      = int.TryParse(currYear, out int py) ? py : DateTime.Now.Year;
            int selectedMonthInt = int.TryParse(selectedMonth, out int pm) ? pm : 12;

            string prevYear            = (currYearInt - 1).ToString();
            string prevYearPrior       = (currYearInt - 2).ToString();
            string selectedPeriod      = $"{selectedMonth}{currYear}";
            string prevYearPeriod      = $"{selectedMonth}{prevYear}";
            string prevYearPriorPeriod = $"{selectedMonth}{prevYearPrior}";

            int prevMonthInt   = selectedMonthInt == 1 ? 12 : selectedMonthInt - 1;
            int prevMonthYear  = selectedMonthInt == 1 ? currYearInt - 1 : currYearInt;
            string prevMonthPeriod = $"{prevMonthInt:D2}{prevMonthYear}";

            PXTrace.WriteInformation($"[Slide] Periods — CY:{selectedPeriod}, PY:{prevYearPeriod}, PM:{prevMonthPeriod}");

            // ── 5. Fetch GL data ───────────────────────────────────────────────────
            var dataService = new FinancialDataService(_authService, _tenantName, columnMapping);

            var taskCY    = Task.Run(() => dataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, selectedPeriod,      false));
            var taskPY    = Task.Run(() => dataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPeriod,      false));
            var taskPrior = Task.Run(() => dataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYearPriorPeriod, false));
            var taskPM    = Task.Run(() => dataService.FetchAllApiData(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevMonthPeriod,     false));
            var taskJanCY = Task.Run(() => dataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, currYear));
            var taskJanPY = Task.Run(() => dataService.FetchJanuaryBeginningBalance(_currentRecord.Branch, _currentRecord.Organization, _currentRecord.Ledger, prevYear));

            Task.WhenAll(taskCY, taskPY, taskPrior, taskPM, taskJanCY, taskJanPY).Wait();
            PXTrace.WriteInformation($"[Slide] GL data fetched in {stopwatch.ElapsedMilliseconds}ms");

            // ── 6. Run calculation engine ──────────────────────────────────────────
            var engine  = new ReportCalculationEngine(_graph);
            var results = engine.CalculateAll(
                definitionLinks,
                taskCY.Result,
                taskPY.Result,
                cyOpeningData:    taskPY.Result,
                pyOpeningData:    taskPrior.Result,
                cyJanOpeningData: taskJanCY.Result,
                pyJanOpeningData: taskJanPY.Result,
                pmData:           taskPM.Result);

            PXTrace.WriteInformation($"[Slide] Calculation engine produced {results.Count} values in {stopwatch.ElapsedMilliseconds}ms");

            // ── 7. Build markdown ──────────────────────────────────────────────────
            var markdownBuilder = new MarkdownBuilderService();
            string markdown = markdownBuilder.Build(_currentRecord, definitionsWithItems, results);
            PXTrace.WriteInformation($"[Slide] Markdown built — {markdown.Length} chars");

            return (markdown, definitionsWithItems);
        }
    }
}
