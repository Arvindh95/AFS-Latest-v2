# Financial Report Module - User Manual v2

**Version:** 2.1
**Last Updated:** March 2026
**Module:** FinancialReport (Acumatica ERP Customization)

---

## Table of Contents

1. [Overview](#1-overview)
2. [Module Architecture](#2-module-architecture)
3. [Screens Reference](#3-screens-reference)
4. [Setting Up API Credentials](#4-setting-up-api-credentials)
5. [Creating a Report Definition](#5-creating-a-report-definition)
6. [Configuring Line Items](#6-configuring-line-items)
7. [Line Types Explained](#7-line-types-explained)
8. [Balance Types Explained](#8-balance-types-explained)
9. [Dimension Filters (Per-Line)](#9-dimension-filters-per-line)
10. [Rounding & Formatting](#10-rounding--formatting)
11. [Multi-Definition Reports](#11-multi-definition-reports)
12. [Creating a Word Template](#12-creating-a-word-template)
13. [Generating a Report](#13-generating-a-report)
14. [Legacy Mode vs Definition Mode](#14-legacy-mode-vs-definition-mode)
15. [Copy Definition](#15-copy-definition)
16. [Reset Status](#16-reset-status)
17. [Worked Examples](#17-worked-examples)
18. [Troubleshooting](#18-troubleshooting)
19. [Technical Reference](#19-technical-reference)

---

## 1. Overview

The Financial Report Module is a customization for Acumatica ERP that automates the generation of financial statements (Balance Sheet, Profit & Loss, Cash Flow, Changes in Equity, and custom reports) by:

1. Fetching GL data from a configurable Generic Inquiry (GI) via OData API
2. Calculating line values using one or more configurable report definitions
3. Populating a Microsoft Word template with calculated values
4. Saving the generated document back to Acumatica

The module supports multi-company, multi-branch, multi-period, and **multi-definition** reporting. A single Word template can include placeholders from multiple definitions (e.g., Balance Sheet and P&L in one document), with full cross-definition formula support.

---

## 2. Module Architecture

```
+---------------------------+
|  FR101000                 |   Main screen: upload template, select year/period/
|  Financial Report         |   branch/org/ledger, link definitions (tab), generate
+---------------------------+
            |
            v
+---------------------------+
|  ReportGenerationService  |   Orchestrator: pre-scans template, fetches data
|  (Services/)              |   conditionally, runs engine, fills Word template
+---------------------------+
     |              |
     v              v
+-----------+  +-----------------------+
| Financial |  | ReportCalculation     |
| DataSvc   |  | Engine                |
| (OData)   |  | (Multi-Definition)    |
+-----------+  +-----------------------+
     |              |
     v              v
+-----------+  +-----------------------+
| GI via    |  | FLRTReportDefinition  |
| OData API |  | FLRTReportLineItem    |
+-----------+  | FLRTReportDefLink     |
               | (FR101002 + FR101000) |
               +-----------------------+
```

### Key Components

| Component | Purpose |
|---|---|
| **FR101000** | Main screen — upload template, link definitions, generate, download, reset status |
| **FR101002** | Report Definition maintenance — GI mapping, prefix, line items, rounding, copy |
| **ReportGenerationService** | Orchestrates end-to-end report generation |
| **FinancialDataService** | Fetches GL data from GI via OData API with conditional parallel calls |
| **ReportCalculationEngine** | Multi-definition engine — topological sort across all definitions |
| **WordTemplateService** | Extracts placeholders from and fills Word templates |
| **GIColumnMapping** | Maps GI column names to expected data fields |
| **RoundingSettings** | Carries rounding configuration to the engine |
| **AuthService** | Thread-safe OAuth2 authentication with token caching and refresh |
| **CredentialProvider** | Retrieves and caches RSA-decrypted tenant credentials |

---

## 3. Screens Reference

### FR101000 - Financial Report Generation

The main screen where you:
- Attach a Word template (`.docx` with `FRTemplate` in the filename)
- Select the reporting year, month, branch, organization, and ledger
- Link one or more Report Definitions via the **Report Definitions** tab
- Click **Generate Report** to produce the document
- Click **Download Report** to retrieve the generated file
- Click **Reset Status** to recover a stuck or failed report to Pending

#### Report Definitions Tab

The tab at the bottom of FR101000 shows the grid of definitions linked to this report:

| Column | Description |
|---|---|
| **Definition** | Selector to choose a Report Definition (by Definition Code) |
| **Prefix** | Read-only — shows the prefix of the selected definition (e.g., `BS`) |
| **Display Order** | Controls grid row order only — does not affect calculation order |

You can link multiple definitions to a single report. Each definition's placeholders will be prefixed in the Word template (e.g., `{{BS_CASH_CY}}`, `{{PL_REVENUE_CY}}`).

### FR101002 - Report Definition

The configuration screen where you define:
- The **Definition Prefix** (2–10 alphanumeric characters, globally unique)
- The GI data source and column mapping
- Report line items (account ranges, subtotals, formulas)
- Rounding and formatting settings
- Use **Detect Columns** to auto-map GI columns
- Use **Copy Definition** to duplicate an existing definition with all its line items

---

## 4. Setting Up API Credentials

Before the module can fetch GL data, configure credentials in the **Tenant Credentials** screen (FR101001 or accessible via the setup menu).

### Required Fields

| Field | Description | Example |
|---|---|---|
| **Company Number** | Integer linking reports to credentials | `1` |
| **Tenant Name** | Acumatica tenant/company name (must be unique) | `MyCompany` |
| **Base URL** | Acumatica instance URL (no trailing slash) | `https://mycompany.acumatica.com` |
| **Client ID** | OAuth2 client ID (RSA-encrypted at rest) | `xxxxxxxx-xxxx-xxxx-xxxx` |
| **Client Secret** | OAuth2 client secret (RSA-encrypted at rest) | `(your secret)` |
| **Username** | API user account (RSA-encrypted at rest) | `admin@MyCompany` |
| **Password** | API user password (RSA-encrypted at rest) | `(your password)` |

### Important Notes

- All sensitive fields are RSA-encrypted when saved — never stored in plaintext
- The API user must have OData access to the Generic Inquiry
- The GI must be published and accessible via the OData endpoint
- Use a dedicated API user (not a personal account) for reliability
- Company Number and Tenant Name must each be unique across all credential records
- In production, the Base URL must use **HTTPS** — HTTP sends credentials unencrypted

---

## 5. Creating a Report Definition

Navigate to **FR101002 - Report Definition**.

### Step 1: Create the Definition Header

| Field | Description | Example |
|---|---|---|
| **Definition Code** | Unique identifier (up to 30 chars, immutable after first save) | `BS_2024` |
| **Definition Prefix** | 2–10 alphanumeric characters, globally unique — used in template placeholders | `BS` |
| **Report Type** | Type of financial statement | Balance Sheet, Profit & Loss, Cash Flow, Changes in Equity, Custom |
| **Description** | Friendly description (up to 255 characters) | `Balance Sheet FY2024` |
| **Active** | Whether this definition is available for use | Checked |

> **Definition Prefix is critical.** Every Word template placeholder for this definition will start with this prefix: `{{BS_LINECODE_CY}}`. Choose short, meaningful prefixes. Prefixes are validated to be alphanumeric only (no underscores or spaces).

### Step 2: Configure the Data Source

| Field | Default | Description |
|---|---|---|
| **Generic Inquiry Name** | `TrialBalance` | Name of the GI to query via OData |
| **Account Column** | `Account` | Column containing the GL account code |
| **Account Type Column** | `Type` | Column containing the account type (A/L/E/I/Q) |
| **Beginning Balance Column** | `BeginningBalance` | Column for beginning balance |
| **Ending Balance Column** | `EndingBalance` | Column for ending balance |
| **Debit Column** | `Debit` | Column for debit amounts |
| **Credit Column** | `Credit` | Column for credit amounts |

#### Using Detect Columns

1. Enter the **Generic Inquiry Name** (e.g. `TrialBalance`)
2. Click **Detect Columns** in the toolbar
3. The module connects to the API, fetches column metadata from the GI, and auto-maps column names using case-insensitive matching
4. Review and adjust the mapped columns if needed
5. Click **Save**

Auto-mapping rules:
- **Account**: looks for "Account" (excludes "Sub" matches)
- **Type**: looks for "Type" or "AccountType"
- **Beginning Balance**: looks for "BeginningBalance", "Beginning", or "BegBal"
- **Ending Balance**: looks for "EndingBalance", "Ending", or "YtdBalance"
- **Debit / Credit**: looks for "Debit" and "Credit"

> Detect Columns requires Tenant Credentials to be configured first.

### Step 3: Configure Formatting

| Field | Options | Description |
|---|---|---|
| **Rounding Level** | Units, Thousands, Millions | Scale factor for displayed values |
| **Decimal Places** | 0, 1, 2 | Number of decimal places after rounding |

---

## 6. Configuring Line Items

Line items define what the report calculates. Each line produces up to three Word template placeholders per linked report:
- `{{PREFIX_LINECODE_CY}}` — Current Year value
- `{{PREFIX_LINECODE_PY}}` — Prior Year value
- `{{PREFIX_LINECODE_PM}}` — Previous Month value (only if the template contains `_PM` placeholders)

### Line Item Fields

| Field | Description |
|---|---|
| **Sort Order** | Processing sequence — lower numbers are processed first. Critical because subtotals and formulas depend on earlier lines. |
| **Line Code** | Unique identifier within the definition, used in Word template placeholders (up to 100 chars). Convention: `UPPER_CASE_WITH_UNDERSCORES`. |
| **Description** | Human-readable label (up to 255 chars) — for reference only, not in output |
| **Line Type** | How the value is calculated (Account Range, Subtotal, Calculated, Heading) |
| **Account From** | Start of GL account range (inclusive, up to 50 chars) |
| **Account To** | End of GL account range (inclusive, up to 50 chars) |
| **Account Type Filter** | Restrict to account type: Asset (A), Liability (L), Expense (E), Income (I), Equity (Q), or All |
| **Balance Type** | Which balance figure to use (see Section 8) |
| **Sign Rule** | As-Is or Flip Sign (multiply by −1) |
| **Parent Line Code** | Links this line as a child of a Subtotal line |
| **Formula** | Mathematical expression for Calculated lines (up to 500 chars) |
| **Visible in Report** | If unchecked, value is calculated but placeholder renders as empty string |
| **Subaccount Filter** | Optional exact-match filter on subaccount code (up to 30 chars) |
| **Branch Filter** | Optional exact-match filter on branch |
| **Organization Filter** | Optional exact-match filter on organization |
| **Ledger Filter** | Optional exact-match filter on ledger |

### Field Availability by Line Type

| Field | Account Range | Subtotal | Calculated | Heading |
|---|---|---|---|---|
| Account From / To | Yes | — | — | — |
| Account Type Filter | Yes | — | — | — |
| Balance Type | Yes | — | — | — |
| Sign Rule | Yes | — | — | — |
| Parent Line Code | Yes | Yes | — | — |
| Formula | — | — | Yes | — |
| Visible | Yes | Yes | Yes | — |
| Dimension Filters | Yes | — | — | — |

Changing the Line Type automatically clears and disables irrelevant fields.

### Validation Rules

- **Line Code** is required and must be unique within the same definition
- **Account From** and **Account To** are required for Account Range lines
- **Formula** is required for Calculated lines
- Validation runs on save with field-level error messages

---

## 7. Line Types Explained

### Account Range

Sums GL account balances within the `AccountFrom` to `AccountTo` range.

**Processing:**
1. Iterates all accounts returned by the GI for the selected period
2. Includes only accounts within the range (smart alphanumeric comparison supporting segmented codes like `1000-00`)
3. Optionally filters by Account Type
4. Reads the specified Balance Type (see Section 8)
5. Applies automatic sign normalization (credit-normal accounts — Liability, Income, Equity — are flipped to positive)
6. Applies Sign Rule if set to Flip
7. Sums all matching values

**Example:**
```
Line Code:    CASH
Account From: 10100
Account To:   10199
Balance Type: Ending
Sign Rule:    As-Is
```

### Subtotal

Sums all lines that have this line's code as their **Parent Line Code**.

**Example:**
```
Sort  Line Code       Type           Parent
10    CASH            Account Range  CURR_ASSETS
20    AR              Account Range  CURR_ASSETS
30    INVENTORY       Account Range  CURR_ASSETS
40    CURR_ASSETS     Subtotal       (blank)
```
`CURR_ASSETS` = `CASH` + `AR` + `INVENTORY`

> Child lines must have a lower Sort Order than the Subtotal line.

### Calculated

Evaluates a mathematical formula referencing other Line Codes by their code alone (within the same definition) or by `PREFIX_LINECODE` (cross-definition).

**Supported operators:** `+  −  *  /  ( )`

**Behaviour:**
- Operator precedence respected (`*` and `/` before `+` and `−`)
- Division by zero returns 0 (no error)
- Unknown line codes default to 0 (trace warning logged)
- Supports unary minus: `−ADJUSTMENTS`
- Supports numeric literals: `GROSS_PROFIT / TOTAL_REVENUE * 100`

**Examples:**
```
NET_INCOME          = TOTAL_REVENUE - TOTAL_EXPENSES
WORKING_CAPITAL     = CURR_ASSETS - CURR_LIAB
GROSS_MARGIN_PCT    = GROSS_PROFIT / TOTAL_REVENUE * 100
CHECK               = BS_TOTAL_ASSETS - BS_LIAB_EQUITY    ← cross-definition
```

### Heading

Display-only. No value calculated. Placeholder resolves to empty string. Visible flag is automatically false.

---

## 8. Balance Types Explained

| Balance Type | Description | Data Source |
|---|---|---|
| **Ending** | Period ending balance | Selected period data |
| **Beginning** | Period opening balance (prior period's ending) | Prior-period data (opening rows) |
| **January Beginning** | Balance as at January 1 of the reporting year | January beginning balance fetch |
| **Debit** | Year-to-date cumulative debit activity (Jan → selected month) | Range data (cumulative) |
| **Credit** | Year-to-date cumulative credit activity (Jan → selected month) | Range data (cumulative) |
| **Movement** | Net period activity = Debit − Credit | Selected period data |

> **Performance note:** Reports that use only Ending balance type will skip the cumulative API calls entirely, making generation faster. Reports with Debit, Credit, or Movement balance types require the full cumulative range fetch.

---

## 9. Dimension Filters (Per-Line)

Each Account Range line can be restricted to specific GL dimensions.

### Available Filters

| Filter | Description |
|---|---|
| **Subaccount Filter** | Exact subaccount code (e.g. `000-000`) |
| **Branch Filter** | Selected from Acumatica branches |
| **Organization Filter** | Selected from Acumatica organizations |
| **Ledger Filter** | Selected from Acumatica ledgers |

### How Filters Work

**No filters set (default):**
Uses pre-aggregated data (all dimensions summed per account). Fastest path.

**Any filter set:**
Switches to per-row detail data. Each GI row is checked against all set filters (AND logic — all must match). When zero rows match, the trace log shows filter values and sample data rows for debugging.

### Important Notes

- Only applies to **Account Range** lines
- All filters use **exact, case-insensitive match**
- Multiple filters combine with **AND** logic
- The report header Branch/Organization/Ledger fields filter what data is fetched. Per-line filters filter within what was fetched. If the report header limits to Branch A, setting a per-line filter to Branch B will return 0.
- **Best practice:** Leave report header branch/organization blank, use per-line filters to scope individual lines

---

## 10. Rounding & Formatting

### Rounding Levels

| Level | Divisor | Raw Value | Result (0 dp) |
|---|---|---|---|
| **Units** | 1 | 1,808,344 | 1,808,344 |
| **Thousands** | 1,000 | 1,808,344 | 1,808 |
| **Millions** | 1,000,000 | 1,808,344 | 2 |

Rounding uses `MidpointRounding.AwayFromZero`.

### Number Display Format (Definition Mode)

| Value | Display |
|---|---|
| Positive | `1,234,567` |
| Negative | `(1,234,567)` — accounting bracket notation |
| Zero | `−` — dash (standard financial statement practice) |

### Number Display Format (Legacy Mode)

| Value | Display |
|---|---|
| Positive | `1,234,567` |
| Negative | `−1,234,567` — minus sign |
| Zero | `0` |

---

## 11. Multi-Definition Reports

A single financial report can combine multiple definitions — for example, a Balance Sheet definition (`BS`) and a P&L definition (`PL`) in one Word document.

### How It Works

1. On the **FR101000** screen, open the **Report Definitions** tab
2. Add one row per definition, selecting the Definition Code from the selector
3. The **Prefix** column shows the prefix automatically (read-only)
4. Set Display Order if you want to control grid row sequence (no effect on calculation)
5. Save the report record
6. When you click **Generate**, the engine processes all linked definitions together using topological sort

### Definition Prefix

Every Report Definition has a mandatory **Definition Prefix** (2–10 alphanumeric characters, globally unique):

| Definition | Prefix | Example Placeholder |
|---|---|---|
| Balance Sheet | `BS` | `{{BS_TOTAL_ASSETS_CY}}` |
| Profit & Loss | `PL` | `{{PL_NET_INCOME_CY}}` |
| Cash Flow | `CF` | `{{CF_NET_CASHFLOW_CY}}` |

### Placeholder Format with Prefix

```
{{PREFIX_LINECODE_CY}}   ← Current Year
{{PREFIX_LINECODE_PY}}   ← Prior Year
{{PREFIX_LINECODE_PM}}   ← Previous Month
```

**Examples:**
```
{{BS_CASH_CY}}            Balance Sheet — Cash — Current Year
{{BS_TOTAL_ASSETS_PY}}    Balance Sheet — Total Assets — Prior Year
{{PL_NET_INCOME_CY}}      P&L — Net Income — Current Year
{{PL_REVENUE_PM}}         P&L — Revenue — Previous Month
{{CY}}                    Current year number (e.g. 2024)
{{PY}}                    Prior year number (e.g. 2023)
```

### Cross-Definition Formulas

A Calculated line in one definition can reference lines from another definition using the explicit `PREFIX_LINECODE` syntax:

```
Definition: PL
Line Code:  RETAINED_EARNINGS_CHECK
Formula:    BS_TOTAL_EQUITY - PL_NET_INCOME
```

This references `TOTAL_EQUITY` from the `BS` definition and `NET_INCOME` from the current `PL` definition. The engine resolves all dependencies via topological sort (Kahn's algorithm) so definitions are calculated in the correct order regardless of which one was added first.

### Calculation Order

Calculation order is determined entirely by dependency analysis — not by Display Order in the grid. If definition B has a formula referencing a line in definition A, then definition A's lines are always calculated first, automatically.

### Previous Month Placeholders

The `_PM` suffix gives the value for the period immediately before the selected financial month:

```
{{BS_CASH_PM}}     ← Cash balance as at the month before the selected period
{{PL_REVENUE_PM}}  ← Revenue for the previous month
```

The system only fetches previous-month data if the template actually contains `_PM` placeholders. If no `_PM` placeholders exist, the API call is skipped entirely.

---

## 12. Creating a Word Template

The Word template is a standard `.docx` file with placeholders that get replaced with calculated values.

### File Naming

The filename **must** contain `FRTemplate` (case-sensitive). Examples:
- `FinancialStatements_FRTemplate_2024.docx`
- `BS_FRTemplate.docx`

### Placeholder Format

```
{{PLACEHOLDER_NAME}}
```
Double curly braces, no spaces inside.

### Definition-Mode Placeholders (Recommended)

| Placeholder | Description |
|---|---|
| `{{BS_CASH_CY}}` | BS definition — Cash line — Current Year |
| `{{BS_CASH_PY}}` | BS definition — Cash line — Prior Year |
| `{{BS_CASH_PM}}` | BS definition — Cash line — Previous Month |
| `{{PL_NET_INCOME_CY}}` | P&L definition — Net Income — Current Year |
| `{{CF_NET_CASHFLOW_PY}}` | Cash Flow — Net Cash Flow — Prior Year |
| `{{CY}}` | Current year number (e.g. `2024`) |
| `{{PY}}` | Prior year number (e.g. `2023`) |

### Legacy-Mode Placeholders

When no definition is linked, placeholders use raw account codes:

| Placeholder | Description |
|---|---|
| `{{A10100_CY}}` | Account A10100, Current Year ending balance |
| `{{A10100_PY}}` | Account A10100, Prior Year ending balance |
| `{{A10100:A10199_e_CY}}` | Sum of accounts A10100 to A10199, ending balance, CY |
| `{{A1???:A2???_e_CY}}` | Wildcard range: sum A1000–A2999, ending balance, CY |
| `{{Sum1_A_CY}}` | Sum all accounts starting with "A", CY |
| `{{DebitSum3_B53_CY}}` | Sum debits for accounts starting with "B53", CY |
| `{{A12345_sb000123_br001_CY}}` | Account with subaccount and branch filters |
| `{{A12345_btcredit_CY}}` | Account with specific balance type (credit) |

### Template Design Rules

1. Type placeholders directly in Word — do NOT paste from other sources (hidden formatting breaks matching)
2. Use exact casing for Line Codes (matching is case-insensitive but consistency avoids mistakes)
3. Do not split a placeholder across a line break in Word
4. Do not apply mixed formatting (bold + normal) within one placeholder
5. Placeholders work in body text, tables, headers, and footers
6. Maximum **1,000 placeholders** per template — split into multiple reports if needed
7. Test with a minimal template first before building the full document

### Example Template Structure

```
              FINANCIAL STATEMENTS
         For the Year Ended December 31, {{CY}}

                                        {{CY}}          {{PY}}

BALANCE SHEET
  Cash and Cash Equivalents     {{BS_CASH_CY}}      {{BS_CASH_PY}}
  Accounts Receivable           {{BS_AR_CY}}        {{BS_AR_PY}}
  Total Current Assets          {{BS_CURR_ASSETS_CY}} {{BS_CURR_ASSETS_PY}}
  Total Assets                  {{BS_TOTAL_ASSETS_CY}} {{BS_TOTAL_ASSETS_PY}}

PROFIT & LOSS
  Revenue                       {{PL_REVENUE_CY}}   {{PL_REVENUE_PY}}
  Net Income                    {{PL_NET_INCOME_CY}} {{PL_NET_INCOME_PY}}
```

---

## 13. Generating a Report

### Step-by-Step

1. Navigate to **FR101000 - Financial Report**
2. Create a new report record (or open an existing one)
3. Attach a Word template (`.docx` with `FRTemplate` in the filename) via the Files panel
4. Fill in the report header:

   | Field | Description |
   |---|---|
   | **Report Name** | Display name (up to 225 chars) |
   | **Company Number** | Links to tenant credentials |
   | **Current Year** | The reporting year |
   | **Financial Month** | The period month (defaults to December) |
   | **Branch** | Optional: pre-filter all data to a specific branch |
   | **Organization** | Optional: pre-filter all data to a specific organization |
   | **Ledger** | Optional: filter to a specific ledger (e.g. `ACTUAL`) |

5. Open the **Report Definitions** tab and add definitions:
   - Click the **+** button in the grid
   - Select the Definition Code from the selector
   - Set Display Order if needed
   - Repeat for each definition
6. Click **Save**
7. Click **Generate Report**
8. Wait for status to change to **Ready to Download**
9. Click **Download Report** to retrieve the `.docx` file

### Timeout Protection

Report generation has a built-in **15-minute timeout**. If exceeded, the status is set to Failed and temporary files are cleaned up.

### What Happens During Generation

1. Template is extracted and all placeholders are identified and categorised:
   - Wildcard range (`A????:B????_e_CY`)
   - Exact range (`A74101:A75101_e_CY`)
   - Regular (`BS_CASH_CY`, `A74101_CY`)
2. **Pre-scan:** The system examines linked line items to determine which API calls are actually needed:
   - Debit/Credit/Movement lines → cumulative range calls required
   - `_PM` placeholders in template → previous-month call required
   - No filters → detail rows not fetched (faster)
3. **Parallel API calls** (up to 8, conditionally skipped):
   - Current year period data
   - Prior year period data
   - January beginning balance — current year
   - January beginning balance — prior year
   - Cumulative CY range (Jan → selected month) — *skipped if not needed*
   - Cumulative PY range (Jan → Dec prior year) — *skipped if not needed*
   - Previous month data — *skipped if no `_PM` placeholders*
4. **ReportCalculationEngine** processes all linked definitions in dependency order:
   - Builds a dependency graph across all definitions
   - Topological sort ensures correct calculation order for cross-definition formulas
   - Account Range lines sum GL data with sign normalisation
   - Subtotal and Calculated lines derive values from earlier lines
   - Rounding and formatting applied
5. Legacy placeholders fill any remaining gaps
6. Year constants (`{{CY}}`, `{{PY}}`) are injected
7. All placeholders are replaced in the Word document
8. Generated file is saved to Acumatica and attached to the report record
9. Temporary files are cleaned up

---

## 14. Legacy Mode vs Definition Mode

### Legacy Mode (no definitions linked)

- Placeholders use raw account codes: `{{A10100_CY}}`
- Returns the balance of that exact account or range
- No sign correction, no rounding, no subtotals
- Supports Sum, DebitSum, CreditSum, BegSum prefix placeholders
- Supports exact and wildcard range placeholders
- Supports dimensional filtering via placeholder syntax
- Best for quick, ad-hoc reports

### Definition Mode (one or more definitions linked)

- Placeholders use `{{PREFIX_LINECODE_CY/PY/PM}}`
- Full multi-definition engine with cross-definition formulas
- Automatic sign normalisation
- Configurable rounding
- Per-line dimension filters
- Accounting bracket notation for negatives, dash for zeros
- Best for formal financial statements

### Both Modes Together

When definitions are linked, **both systems run**. Definition placeholders take priority. Legacy placeholders fill anything not covered. This allows using definitions for main financial statement lines while referencing individual accounts for notes or schedules.

---

## 15. Copy Definition

Duplicates an existing Report Definition with all its line items.

### How to Use

1. Open the definition you want to copy in FR101002
2. Click **Copy Definition** in the toolbar
3. A new definition is created with:
   - Definition Code = original code + `_COPY`
   - Description = original description + ` (Copy)`
   - All header settings (prefix, GI name, column mapping, rounding) are copied
   - All line items duplicated with same settings
4. Rename the Definition Code, Prefix, and Description
5. Modify line items as needed

---

## 16. Reset Status

Recovers a report that is stuck in "In Progress" or "Failed" status.

### How to Use

1. Open the report in FR101000
2. Click **Reset Status** in the toolbar
3. Confirm the prompt
4. Status resets to "Pending" and the previously generated file reference is cleared

### When to Use

- Report stuck "In Progress" after a server restart
- Report failed and you want to retry after fixing the issue
- You want to clear a completed report and regenerate

> While any report has status "In Progress", Generate and Download buttons are disabled system-wide. Resetting the stuck report re-enables them for all reports.

---

## 17. Worked Examples

### Example 1: Single Balance Sheet

**Definition:** `BS` prefix, Balance Sheet

| Sort | Line Code | Type | Account From | Account To | Balance Type | Parent |
|---|---|---|---|---|---|---|
| 10 | CASH | Account Range | 10100 | 10199 | Ending | CURR_ASSETS |
| 20 | AR | Account Range | 11000 | 11999 | Ending | CURR_ASSETS |
| 30 | INVENTORY | Account Range | 12000 | 12999 | Ending | CURR_ASSETS |
| 40 | CURR_ASSETS | Subtotal | | | | TOTAL_ASSETS |
| 50 | PPE | Account Range | 15000 | 15999 | Ending | NONCURR_ASSETS |
| 60 | NONCURR_ASSETS | Subtotal | | | | TOTAL_ASSETS |
| 70 | TOTAL_ASSETS | Subtotal | | | | |
| 80 | AP | Account Range | 20000 | 20999 | Ending | CURR_LIAB |
| 90 | CURR_LIAB | Subtotal | | | | TOTAL_LIAB |
| 100 | LOANS | Account Range | 25000 | 25999 | Ending | NONCURR_LIAB |
| 110 | NONCURR_LIAB | Subtotal | | | | TOTAL_LIAB |
| 120 | TOTAL_LIAB | Subtotal | | | | |
| 130 | SHARE_CAPITAL | Account Range | 30000 | 30999 | Ending | TOTAL_EQUITY |
| 140 | RET_EARNINGS | Account Range | 32000 | 32999 | Ending | TOTAL_EQUITY |
| 150 | TOTAL_EQUITY | Subtotal | | | | |
| 160 | LIAB_EQUITY | Calculated | | | | |

Line 160 Formula: `TOTAL_LIAB + TOTAL_EQUITY`

Template placeholders: `{{BS_TOTAL_ASSETS_CY}}`, `{{BS_LIAB_EQUITY_CY}}` (should balance)

---

### Example 2: Combined BS + P&L in One Document

Link two definitions to one report:

| Definition Code | Prefix | Report Type |
|---|---|---|
| BS_2024 | BS | Balance Sheet |
| PL_2024 | PL | Profit & Loss |

Template uses both sets of placeholders:
```
BALANCE SHEET
  Total Assets        {{BS_TOTAL_ASSETS_CY}}   {{BS_TOTAL_ASSETS_PY}}

PROFIT & LOSS
  Revenue             {{PL_REVENUE_CY}}         {{PL_REVENUE_PY}}
  Net Income          {{PL_NET_INCOME_CY}}       {{PL_NET_INCOME_PY}}
```

---

### Example 3: Cross-Definition Formula

The P&L definition calculates retained earnings by referencing the BS definition:

```
Definition: PL
Line Code:  RETAINED_CHECK
Formula:    BS_TOTAL_EQUITY - PL_NET_INCOME
```

The engine automatically ensures BS lines are calculated before PL lines that reference them.

---

### Example 4: Previous Month Comparison

Track month-over-month movement in the template:

```
               This Month        Prior Month       Change
Cash           {{BS_CASH_CY}}   {{BS_CASH_PM}}    ...
Revenue        {{PL_REVENUE_CY}} {{PL_REVENUE_PM}} ...
```

The `_PM` data fetches the period immediately before the selected Financial Month.

---

### Example 5: Branch-Specific Revenue Breakdown

Leave report header Branch blank. Use per-line branch filters:

| Sort | Line Code | Type | Account From | Account To | Branch Filter |
|---|---|---|---|---|---|
| 10 | REV_HQ | Account Range | 40000 | 49999 | HQ |
| 20 | REV_RETAIL | Account Range | 40000 | 49999 | RETAIL |
| 30 | REV_WAREHOUSE | Account Range | 40000 | 49999 | WAREHOUSE |
| 40 | TOTAL_REVENUE | Calculated | | | |

Formula for line 40: `REV_HQ + REV_RETAIL + REV_WAREHOUSE`

---

## 18. Troubleshooting

### Common Issues

| Problem | Cause | Solution |
|---|---|---|
| Placeholder shows `{{BS_CASH_CY}}` literally | Line Code or prefix mismatch | Check Definition Prefix and Line Code match exactly |
| Value shows `0` or `−` unexpectedly | Account range doesn't match chart of accounts | Verify AccountFrom/AccountTo |
| Subtotal shows `0` | Children missing Parent Line Code | Set Parent Line Code on each child |
| Formula shows `0` | Referenced line not yet calculated | Lower the Sort Order of referenced lines |
| Cross-definition formula returns `0` | Wrong prefix in formula | Use `PREFIX_LINECODE` syntax exactly as defined |
| Detect Columns fails | Credentials not configured | Set up Tenant Credentials first |
| All values are `0` | API returns no data | Check GI name, period, and that GI has data |
| Generate button disabled | Another report is In Progress | Wait or use Reset Status on the stuck report |
| Report stuck In Progress | Server restart or timeout | Use **Reset Status** |
| Previous Month values all `0` | `_PM` placeholders in template but wrong period | Check Financial Month — PM is the month before |
| Too many placeholders error | Over 1,000 placeholders | Split into multiple reports |

### Sign Normalisation

| Account Type | GL Natural Sign | Engine Treatment |
|---|---|---|
| Asset (A) | Debit positive | Kept as-is |
| Expense (E) | Debit positive | Kept as-is |
| Liability (L) | Credit (negative in GL) | Flipped to positive |
| Income (I) | Credit (negative in GL) | Flipped to positive |
| Equity (Q) | Credit (negative in GL) | Flipped to positive |

The **Sign Rule** on the line item is an additional override applied AFTER auto-normalisation.

### Checking Trace Logs

Check **System > Management > Trace** for:
- `Multi-definition report: 2 definition(s) linked — prefixes: [BS, PL]`
- `ReportCalculationEngine: 45 total line items loaded`
- `API fetch flags — needsDetail=False, needsCumulative=True, needsPM=False`
- `8 API calls completed (skipped: cumulative=False, PM=True)`
- `ReportCalculationEngine produced 90 placeholders`
- `Total report generation completed in 12,450 ms`
- Authentication and API call status
- Filter values and sample rows when dimension filter returns 0

---

## 19. Technical Reference

### Database Tables

| Table | Purpose |
|---|---|
| `FLRTFinancialReport` | Main report records (template, year, period, branch, status, file IDs) |
| `FLRTReportDefinitionLink` | Links one or more definitions to a report (with DisplayOrder) |
| `FLRTReportDefinition` | Definition header (prefix, GI name, column mapping, rounding, report type) |
| `FLRTReportLineItem` | Line items within a definition (account ranges, formulas, dimension filters) |
| `FLRTTenantCredentials` | API credentials per tenant/company (RSA-encrypted sensitive fields) |

### Screen IDs

| Screen ID | Name | Purpose |
|---|---|---|
| FR101000 | Financial Report | Main generation screen |
| FR101002 | Report Definition | Definition & line item configuration |

### Report Status Lifecycle

| Status | Display | Description |
|---|---|---|
| `File not Generated` | Pending | Initial state — can be edited and generated |
| `File Generation In Progress` | In Progress | Background generation running |
| `Ready to Download` | Ready | Generation succeeded, file available |
| `Failed to Generate File` | Failed | Error occurred during generation |

### Placeholder Format Summary

| Mode | CY | PY | PM |
|---|---|---|---|
| Definition | `{{PREFIX_LINECODE_CY}}` | `{{PREFIX_LINECODE_PY}}` | `{{PREFIX_LINECODE_PM}}` |
| Legacy | `{{ACCOUNT_CY}}` | `{{ACCOUNT_PY}}` | — |
| Year constants | `{{CY}}` | `{{PY}}` | — |

### Calculation Engine Processing Order

1. All line items across all linked definitions are loaded
2. A dependency graph is built (cross-definition references detected)
3. Topological sort (Kahn's algorithm) determines calculation order
4. Heading lines are skipped
5. Account Range lines sum GL data (with sign normalisation)
6. Subtotal lines sum their children (children always processed first due to sort)
7. Calculated lines evaluate formulas (dependencies always processed first)
8. Values stored for CY, PY, and PM separately
9. Non-visible lines get empty-string placeholders
10. Rounding applied during placeholder formatting

### Conditional API Calls

The engine pre-scans the report before making API calls:

| Condition | API calls skipped |
|---|---|
| No Debit/Credit/Movement balance types | Cumulative range calls (CY and PY) skipped |
| No `_PM` placeholders in template | Previous month call skipped |
| No dimension filters on any line | Detail rows not fetched (memory saving) |

### API Data Flow

```
1. Read FLRTFinancialReport + linked FLRTReportDefinitionLinks
2. Pre-scan line items: determine needsCumulative, needsPM, needsDetail
3. Create FinancialDataService with GIColumnMapping
4. Parallel API calls (conditionally):
   a. Current year data (selected period)
   b. Prior year data (same month, prior year)
   c. January beginning balance — current year
   d. January beginning balance — prior year
   e. Cumulative CY (Jan → selected month)    [if needsCumulative]
   f. Cumulative PY (Jan → Dec prior year)    [if needsCumulative]
   g. Previous month data                      [if needsPM]
5. ReportCalculationEngine.CalculateAll() across all definitions
6. Legacy placeholder processing (fills remaining gaps)
7. Year constants injected
8. WordTemplateService fills the .docx
9. File saved to Acumatica, temp files cleaned up
```

### Error Messages Reference

| Message | Cause |
|---|---|
| `Please select a template to generate the report.` | No record selected |
| `The selected template does not have any attached files.` | No file or filename missing `FRTemplate` |
| `A report generation process is already running.` | Another report In Progress |
| `Failed to authenticate. Please check credentials.` | Invalid API credentials |
| `Current Year is not specified.` | Missing Current Year field |
| `No generated file is available for download.` | Not yet generated or generation failed |
| `No API credentials found for company X.` | No tenant credentials for this Company Number |
| `Definition Prefix is required.` | Prefix field empty on definition |
| `Definition Prefix must be alphanumeric only.` | Special characters in prefix |
| `Definition Prefix must be unique.` | Another definition already uses this prefix |
| `Template contains X placeholders. Maximum is 1,000.` | Too many placeholders |
| `Report generation timed out after 15 minutes.` | Exceeded timeout |
| `Definition Code must be unique.` | Duplicate Definition Code |
| `Line Code must be unique within the same definition.` | Duplicate Line Code in same definition |

### Customization Deployment

1. Export the customization package from source instance
2. Import into the target instance via Customization Projects
3. Publish the customization
4. Run SQL scripts from the `/SQL/` folder in order (`01_`, `02_`, `03_`, `04_`, `05_`, `06_`)
5. Configure API credentials for the tenant
6. Create or import Report Definitions

---

*Financial Report Module v2.1 — March 2026*
