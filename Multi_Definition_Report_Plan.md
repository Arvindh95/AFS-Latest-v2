# Multi-Definition Report Plan

**Branch:** Enhancement
**Date:** 2026-03-10
**Goal:** Allow a single Financial Report to link multiple Report Definitions, resolve all placeholders (including cross-definition formulas) via topological sort, and generate one unified Word document.

---

## Overview

### Current State
```
FLRTFinancialReport
  └── DefinitionID (single FK → FLRTReportDefinition)
        └── Line items processed in SortOrder
              └── Produces LINECODE_CY / LINECODE_PY placeholders
```

### Target State
```
FLRTFinancialReport
  └── FLRTReportDefinitionLink (child grid)
        ├── Link → BS Definition (prefix: BS) → BS_LINECODE_CY / BS_LINECODE_PY
        ├── Link → PL Definition (prefix: PL) → PL_LINECODE_CY / PL_LINECODE_PY
        └── Link → CF Definition (prefix: CF) → CF_LINECODE_CY / CF_LINECODE_PY

All line items across all definitions → topological sort → single global dictionary
→ One Word template, one generation run, one output file
```

---

## Step 1 — Add `Prefix` Field to `FLRTReportDefinition`

**File:** `FinancialReport/DAC/FLRTReportDefinition.cs`

Add a new field `DefinitionPrefix` — a short 2–10 character code that identifies this definition in cross-definition formula references and in Word placeholders.

```csharp
#region DefinitionPrefix
[PXDBString(10, IsUnicode = true)]
[PXUIField(DisplayName = "Prefix")]
[PXDefault]
public virtual string DefinitionPrefix { get; set; }
public abstract class definitionPrefix : PX.Data.BQL.BqlString.Field<definitionPrefix> { }
#endregion
```

**Rules:**
- Alphanumeric only, no underscores (validated on save)
- Must be unique across all definitions
- Examples: `BS`, `PL`, `CF`, `EQ`, `NOTE1`

**UI:** Add this field to `FLRTReportDefinitionMaint.cs` — place it prominently near `DefinitionCD`.

---

## Step 2 — New DAC: `FLRTReportDefinitionLink`

**File:** `FinancialReport/DAC/FLRTReportDefinitionLink.cs` *(new file)*

This is the child table that links multiple definitions to a single report. It replaces the single `DefinitionID` field on `FLRTFinancialReport`.

```csharp
[Serializable]
[PXCacheName("FLRT Report Definition Link")]
public class FLRTReportDefinitionLink : PXBqlTable, IBqlTable
{
    #region LinkID
    [PXDBIdentity(IsKey = true)]
    public virtual int? LinkID { get; set; }
    public abstract class linkID : PX.Data.BQL.BqlInt.Field<linkID> { }
    #endregion

    #region ReportID
    [PXDBInt]
    [PXDBDefault(typeof(FLRTFinancialReport.reportID))]
    [PXParent(typeof(SelectFrom<FLRTFinancialReport>
        .Where<FLRTFinancialReport.reportID.IsEqual<reportID.FromCurrent>>))]
    public virtual int? ReportID { get; set; }
    public abstract class reportID : PX.Data.BQL.BqlInt.Field<reportID> { }
    #endregion

    #region DefinitionID
    [PXDBInt]
    [PXUIField(DisplayName = "Definition")]
    [PXSelector(
        typeof(Search<FLRTReportDefinition.definitionID>),
        typeof(FLRTReportDefinition.definitionCD),
        typeof(FLRTReportDefinition.definitionPrefix),
        typeof(FLRTReportDefinition.description),
        typeof(FLRTReportDefinition.reportType),
        SubstituteKey = typeof(FLRTReportDefinition.definitionCD),
        DescriptionField = typeof(FLRTReportDefinition.description))]
    public virtual int? DefinitionID { get; set; }
    public abstract class definitionID : PX.Data.BQL.BqlInt.Field<definitionID> { }
    #endregion

    #region DisplayOrder
    // Controls display order in the grid only. Has NO effect on calculation order.
    // Calculation order is determined entirely by topological sort of dependencies.
    [PXDBInt]
    [PXUIField(DisplayName = "Display Order")]
    [PXDefault(0)]
    public virtual int? DisplayOrder { get; set; }
    public abstract class displayOrder : PX.Data.BQL.BqlInt.Field<displayOrder> { }
    #endregion

    // Standard audit fields (CreatedDateTime, LastModifiedDateTime, Tstamp, etc.)
}
```

**Key point:** `DisplayOrder` is purely cosmetic — it determines how rows appear in the grid. The actual calculation order is resolved automatically by topological sort.

---

## Step 3 — Modify `FLRTFinancialReport`

**File:** `FinancialReport/DAC/FLRTFinancialReport.cs`

Keep the existing `DefinitionID` field but mark it obsolete for backward compatibility. The generation service will check for `FLRTReportDefinitionLink` rows first; if none exist, it falls back to the single `DefinitionID`.

```csharp
#region DefinitionID
/// <summary>
/// Legacy single-definition link. Superseded by FLRTReportDefinitionLink.
/// Kept for backward compatibility. If FLRTReportDefinitionLink rows exist
/// for this report, this field is ignored.
/// </summary>
[PXDBInt]
[PXUIField(DisplayName = "Report Definition (Legacy)", Visible = false)]
...
#endregion
```

---

## Step 4 — Modify `FLRTFinancialReportMaint`

**File:** `FinancialReport/Graph/FLRTFinancialReportMaint.cs`

Add a `PXSelectBase` view for the child `FLRTReportDefinitionLink` grid:

```csharp
public SelectFrom<FLRTReportDefinitionLink>
    .Where<FLRTReportDefinitionLink.reportID
        .IsEqual<FLRTFinancialReport.reportID.FromCurrent>>
    .OrderBy<FLRTReportDefinitionLink.displayOrder.Asc>
    .View DefinitionLinks;
```

**UI changes:**
- Add a `PXGrid` tab "Definitions" showing: Definition selector, Prefix (read-only from definition), Display Order
- Hide the legacy `DefinitionID` field (or remove from layout)
- The prefix column in the grid is read-only — it comes from the selected definition's `DefinitionPrefix`

---

## Step 5 — Overhaul `ReportCalculationEngine`

**File:** `FinancialReport/Services/ReportCalculationEngine.cs`

This is the core change. The engine needs three new capabilities:
1. Accept multiple definitions + their prefixes
2. Resolve formula tokens to global `PREFIX_LINECODE` keys (explicit and implicit)
3. Topological sort to determine calculation order with cycle detection

### 5.1 — New Entry Point

Replace the current `Calculate(int definitionID, ...)` with:

```csharp
/// <summary>
/// Multi-definition entry point.
/// Loads all line items from all linked definitions, resolves cross-definition
/// formula references, sorts by dependency order (topological sort), and
/// calculates all values into a single unified placeholder dictionary.
/// </summary>
public Dictionary<string, string> CalculateAll(
    IEnumerable<DefinitionLink> definitionLinks,  // each has DefinitionID + Prefix
    FinancialApiData cyData,
    FinancialApiData pyData)
```

Where `DefinitionLink` is a simple struct:
```csharp
public struct DefinitionLink
{
    public int DefinitionID;
    public string Prefix;         // e.g. "BS", "PL", "CF"
    public RoundingSettings Rounding;
}
```

### 5.2 — Global Dictionary Structure

Replace the two per-definition dictionaries with global ones keyed by `PREFIX_LINECODE`:

```csharp
// Before (single-definition):
Dictionary<string, decimal> _cyValues  // key = "TOTAL_ASSETS"
Dictionary<string, decimal> _pyValues  // key = "TOTAL_ASSETS"

// After (multi-definition):
Dictionary<string, decimal> _cyValues  // key = "BS_TOTAL_ASSETS"
Dictionary<string, decimal> _pyValues  // key = "BS_TOTAL_ASSETS"
```

Every line item, regardless of which definition it belongs to, is stored in the global dictionary with its prefixed key.

### 5.3 — Formula Token Resolution

When evaluating a formula token, the engine resolves it as follows:

```
Given: current definition prefix = "CF"
Known prefixes across all linked definitions = { "BS", "PL", "CF" }

Token "DISPOSED_ASSETS"
  → Does not start with any known prefix + "_"
  → Implicit: own-definition reference
  → Resolved key = "CF_DISPOSED_ASSETS"

Token "BS_RETAINED_ASSETS"
  → Starts with "BS_" and "BS" is a known prefix
  → Explicit: cross-definition reference
  → Resolved key = "BS_RETAINED_ASSETS"

Token "REVENUE"
  → Does not start with any known prefix + "_"
  → Implicit: own-definition reference
  → Resolved key = "CF_REVENUE"
```

**Prefix detection logic:**
```
For each known prefix P:
    if token.StartsWith(P + "_", OrdinalIgnoreCase):
        → cross-definition reference, resolved key = token (uppercased)
        break
If no prefix matched:
    → own-definition reference, resolved key = currentPrefix + "_" + token
```

### 5.4 — Topological Sort (Kahn's Algorithm)

```
STEP 1: Load all line items from all definitions
        Build node list: each node = (PREFIX_LINECODE, lineItem, definitionPrefix)

STEP 2: Build dependency edges
        For ACCOUNT lines    → no dependencies
        For SUBTOTAL lines   → depends on all child lines (same definition, where
                               childLine.ParentLineCode = this.LineCode)
                               → edges: PREFIX_CHILDLINECODE → PREFIX_THIS
        For CALCULATED lines → parse formula, resolve each token to PREFIX_LINECODE
                               → edges: RESOLVED_TOKEN → PREFIX_THIS

STEP 3: Kahn's algorithm
        - Compute in-degree for each node
        - Initialize queue with all zero-in-degree nodes
        - While queue not empty:
            - Dequeue node N
            - Add N to sorted processing order
            - For each node M that depends on N:
                - Decrement M's in-degree
                - If M's in-degree == 0: enqueue M

STEP 4: Cycle detection
        If sorted order count < total nodes:
            → Remaining nodes form a cycle
            → Identify and report: "Circular dependency: BS_EQUITY → PL_NET_INCOME → BS_EQUITY"
            → Throw PXException with clear message

STEP 5: Process nodes in sorted order
        For each node in sorted order:
            - Calculate CY and PY values
            - Store in global _cyValues[PREFIX_LINECODE] and _pyValues[PREFIX_LINECODE]
```

### 5.5 — Updated `CalculateSubtotal`

Currently queries all line items where `parentLineCode = subtotalLineCode` without definitionID filter. With multi-definition, scope this to within the same definition:

```csharp
// Query child lines within the same definition only
WHERE parentLineCode = subtotalLineCode AND definitionID = currentDefinitionID
```

Then look up child values using prefixed keys: `_cyValues[prefix + "_" + child.LineCode]`

### 5.6 — Updated `BuildPlaceholderMap`

Keys are now prefixed:

```csharp
// Before:
string cyKey = $"{line.LineCode}_{CY}";          // "TOTAL_ASSETS_CY"

// After:
string cyKey = $"{prefix}_{line.LineCode}_{CY}"; // "BS_TOTAL_ASSETS_CY"
```

This directly maps to `{{BS_TOTAL_ASSETS_CY}}` in the Word template.

### 5.7 — Backward Compatibility

Keep the existing `Calculate(int definitionID, ...)` method working. Internally it calls the new `CalculateAll` with a single-item definition list. The output placeholder keys will now be `PREFIX_LINECODE_CY` instead of `LINECODE_CY`.

> **Migration note:** Existing Word templates using `{{TOTAL_ASSETS_CY}}` (no prefix) will break. Templates must be updated to use `{{BS_TOTAL_ASSETS_CY}}` after the definition prefix is assigned. This is a one-time migration.

---

## Step 6 — Modify `ReportGenerationService`

**File:** `FinancialReport/Services/ReportGenerationService.cs`

### 6.1 — Load Linked Definitions

```csharp
// Load all linked definitions for this report
var links = SelectFrom<FLRTReportDefinitionLink>
    .InnerJoin<FLRTReportDefinition>
        .On<FLRTReportDefinition.definitionID.IsEqual<FLRTReportDefinitionLink.definitionID>>
    .Where<FLRTReportDefinitionLink.reportID.IsEqual<@P.AsInt>>
    .OrderBy<FLRTReportDefinitionLink.displayOrder.Asc>
    .View.Select(_graph, report.ReportID);

// Fall back to legacy single DefinitionID if no links found
if (!links.Any() && report.DefinitionID != null)
{
    var def = ... // load single definition
    links = new[] { (def, singleLink) };
}
```

### 6.2 — GL Data Fetching (No Change)

GL data is already fetched once and shared. This remains the same — all definitions share the same 6 API calls. Each definition's GI name could differ, so fetch per distinct GI name if definitions use different GIs.

> **Consideration:** If BS uses `TrialBalance` GI and CF uses a different GI, the fetch needs to be per-GI-name, then the correct data set is passed to each definition's line items. The `DefinitionLink` struct carries the `GIColumnMapping` per definition.

### 6.3 — Run Unified Calculation Engine

```csharp
var definitionLinkArgs = links.Select(l => new DefinitionLink
{
    DefinitionID = l.DefinitionID,
    Prefix       = l.Definition.DefinitionPrefix,
    Rounding     = RoundingSettings.From(l.Definition)
}).ToList();

var engine = new ReportCalculationEngine(_graph);
var definitionPlaceholders = engine.CalculateAll(definitionLinkArgs, cyData, pyData);

// Merge into the master placeholder dictionary
// Definition values take priority over raw account placeholders
foreach (var kvp in definitionPlaceholders)
    masterPlaceholders[kvp.Key] = kvp.Value;
```

---

## Step 7 — Validation on Save

**File:** `FLRTReportDefinitionMaint.cs` and `FLRTFinancialReportMaint.cs`

### On Definition Save
- `DefinitionPrefix` must not be empty
- `DefinitionPrefix` must be alphanumeric only (no underscores, no spaces)
- `DefinitionPrefix` must be unique across all definitions (warn if duplicate exists)

### On Report Save / Before Generation
- Check that no two linked definitions share the same prefix
- Check that no LineCode in any linked definition starts with another linked definition's `Prefix + "_"` (would cause ambiguity in implicit token resolution)
- Surface clear validation errors before generation starts

---

## Step 8 — Circular Dependency Error Reporting

When a cycle is detected during topological sort, the error message must be actionable:

```
Circular dependency detected in report definitions.
The following line codes form a cycle:

  BS_EQUITY (BalanceSheet)
    → required by: PL_NET_INCOME (ProfitLoss)  [formula: REVENUE - BS_EQUITY]
    → required by: BS_EQUITY (BalanceSheet)     [formula: PL_NET_INCOME + RESERVES]

Please revise the formula in BS_EQUITY or PL_NET_INCOME to break the cycle.
```

---

## Data Flow After Changes

```
FLRTFinancialReportMaint (UI)
  → User selects report, clicks Generate
      ↓
ReportGenerationService.Execute()
  ├─ Load FLRTReportDefinitionLink rows for this report
  ├─ Extract Word template → get all {{...}} placeholders
  ├─ Fetch GL data (6 parallel API calls, shared across all definitions)
  ├─ Process raw account placeholders ({{A10100_CY}}, ranges, etc.) — unchanged
  ├─ ReportCalculationEngine.CalculateAll(definitionLinks, cyData, pyData)
  │     ├─ Load ALL line items from ALL definitions
  │     ├─ Build dependency graph (ACCOUNT→SUBTOTAL→CALCULATED + cross-def)
  │     ├─ Topological sort (Kahn's algorithm)
  │     ├─ Detect cycles → error if found
  │     ├─ Process in resolved order → global _cyValues / _pyValues
  │     └─ BuildPlaceholderMap → { "BS_TOTAL_ASSETS_CY": "1,250,000", ... }
  ├─ Merge all placeholder sources
  │     Priority: definition engine values > raw account placeholders
  └─ WordTemplateService.PopulateTemplate()
        → replaces {{BS_TOTAL_ASSETS_CY}}, {{PL_NET_INCOME_PY}}, etc.
        → one output Word document
```

---

## Word Template Placeholder Format (Summary)

| Source | Old Format | New Format |
|---|---|---|
| Definition line item | `{{TOTAL_ASSETS_CY}}` | `{{BS_TOTAL_ASSETS_CY}}` |
| Definition line item (PY) | `{{TOTAL_ASSETS_PY}}` | `{{BS_TOTAL_ASSETS_PY}}` |
| Raw account code | `{{A10100_CY}}` | `{{A10100_CY}}` (unchanged) |
| Account range | `{{A10100:A19999_e_CY}}` | `{{A10100:A19999_e_CY}}` (unchanged) |
| Cross-def in formula | N/A | `BS_RETAINED_ASSETS` in CF formula |

---

## Files to Create / Modify

| File | Action |
|---|---|
| `DAC/FLRTReportDefinitionLink.cs` | **Create** — new link DAC |
| `DAC/FLRTReportDefinition.cs` | **Modify** — add `DefinitionPrefix` field |
| `DAC/FLRTFinancialReport.cs` | **Modify** — mark `DefinitionID` as legacy |
| `Graph/FLRTFinancialReportMaint.cs` | **Modify** — add `DefinitionLinks` view + grid |
| `Graph/FLRTReportDefinitionMaint.cs` | **Modify** — add `DefinitionPrefix` to UI |
| `Services/ReportCalculationEngine.cs` | **Modify** — topological sort, global dict, cross-def resolution |
| `Services/ReportGenerationService.cs` | **Modify** — load links, call `CalculateAll` |

---

## Implementation Order

1. `FLRTReportDefinition.cs` — add Prefix field (small, no dependencies)
2. `FLRTReportDefinitionLink.cs` — create new DAC
3. `FLRTFinancialReportMaint.cs` — add grid (UI wiring)
4. `FLRTReportDefinitionMaint.cs` — add Prefix to UI + validation
5. `ReportCalculationEngine.cs` — core overhaul (topological sort + cross-def)
6. `ReportGenerationService.cs` — wire up multi-definition flow
7. End-to-end test with a two-definition report (BS + CF with cross-formula)