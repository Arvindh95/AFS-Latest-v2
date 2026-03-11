# FINANCIAL REPORT APPLICATION
## Comprehensive User Guide v2

---

**Version:** 2.0
**Last Updated:** March 2026
**For:** Acumatica ERP Users

---

## Table of Contents

1. [Introduction & Overview](#1-introduction--overview)
2. [Getting Started](#2-getting-started)
3. [Setting Up Tenant Credentials](#3-setting-up-tenant-credentials)
4. [Creating a Report Definition](#4-creating-a-report-definition)
5. [Configuring Line Items](#5-configuring-line-items)
6. [The Calculation Engine](#6-the-calculation-engine)
7. [Creating a Word Template](#7-creating-a-word-template)
8. [Generating a Report](#8-generating-a-report)
9. [Report Status & Workflow](#9-report-status--workflow)
10. [Worked Examples](#10-worked-examples)
11. [Troubleshooting Guide](#11-troubleshooting-guide)
12. [Best Practices & Tips](#12-best-practices--tips)
13. [Appendix A: Legacy Placeholder Reference](#13-appendix-a-legacy-placeholder-reference)
14. [Appendix B: Error Message Reference](#14-appendix-b-error-message-reference)
15. [Appendix C: Field Reference](#15-appendix-c-field-reference)
16. [Appendix D: Glossary](#16-appendix-d-glossary)

---

## 1. Introduction & Overview

### 1.1 What is the Financial Report Application?

The Financial Report Application is an Acumatica ERP customization that automates the
generation of financial statements — Balance Sheet, Profit & Loss, Cash Flow, Changes
in Equity, and custom reports.

The core workflow:

1. You define a **Report Definition** that describes the structure of your financial
   statement — which GL account ranges map to which report lines, how subtotals roll
   up, and what formulas to apply.
2. You create a **Word template** with placeholders like `{{CASH_CY}}` and
   `{{TOTAL_ASSETS_PY}}`.
3. The **ReportCalculationEngine** fetches GL data via OData, processes every line
   item (account ranges, subtotals, formulas), applies sign normalization and
   rounding, and populates the template.
4. You download a finished `.docx` report.

No code changes are needed when your chart of accounts evolves — just update the
Report Definition.

### 1.2 Key Capabilities

| Capability | Description |
|---|---|
| **Report Definitions** | Configure account ranges, subtotals, calculated formulas, sign rules, and rounding — all without code |
| **Calculation Engine** | Processes line items in sequence: Account Range → Subtotal → Calculated, with automatic sign normalization |
| **Per-Line Dimension Filters** | Filter individual lines by Subaccount, Branch, Organization, or Ledger |
| **Accounting Formatting** | Bracket notation for negatives `(1,234)`, dash for zeros `-`, configurable rounding (Units / Thousands / Millions) |
| **Year-over-Year** | Every line produces both `{{CODE_CY}}` and `{{CODE_PY}}` automatically |
| **Detect Columns** | Auto-map GI column names to definition fields with one click |
| **Copy Definition** | Duplicate an existing definition with all line items to create variants |
| **Parallel Data Fetching** | 6 concurrent API calls for CY, PY, beginning balances, and cumulative data |
| **Timeout Protection** | 15-minute automatic timeout prevents stuck reports |
| **Reset Status** | Recover stuck or failed reports back to Pending |
| **Legacy Compatibility** | Raw account-code placeholders (`{{A10100_CY}}`) still work alongside definitions |

### 1.3 Who Should Use This Guide?

- **Finance Team Members** generating regular financial reports
- **Accountants** preparing month-end and year-end statements
- **Report Designers** creating definitions and Word templates
- **Administrators** setting up credentials and the system

### 1.4 Prerequisites

- Acumatica ERP installed with the Financial Report customization published
- API credentials configured (see Section 3)
- Microsoft Word for template creation and report viewing
- Basic understanding of your Chart of Accounts
- Financial period data posted in General Ledger

---

## 2. Getting Started

### 2.1 Screens

| Screen | ID | Purpose |
|---|---|---|
| **Financial Report** | FR101000 | Create reports, upload templates, generate, download, reset status |
| **Report Definition** | FR101002 | Define report structure, line items, GI mapping, rounding |
| **Tenant Credentials** | — | Configure API credentials (admin only) |

### 2.2 Recommended Workflow

```
Administrator: Configure Tenant Credentials (one-time)
                          │
                          ▼
Report Designer: Create Report Definition in FR101002
                 ├── Set GI name & column mapping
                 ├── Add line items (Account Range, Subtotal, Calculated, Heading)
                 ├── Configure rounding & formatting
                 └── Save
                          │
                          ▼
Report Designer: Create Word Template (.docx)
                 ├── Use {{LINECODE_CY}} / {{LINECODE_PY}} placeholders
                 ├── Include {{CY}} / {{PY}} for year labels
                 └── Filename must contain "FRTemplate"
                          │
                          ▼
Report User:     Create Report Record in FR101000
                 ├── Set Company Number, Year, Month, Branch/Org/Ledger
                 ├── Link the Report Definition
                 ├── Upload the Word template
                 └── Click Generate Report → Download Report
```

---

## 3. Setting Up Tenant Credentials

> This section is for **Administrators**. Regular users should have credentials configured before creating reports.

### 3.1 What are Tenant Credentials?

Authentication details for the Acumatica OData API. Each company/tenant needs its own record. All sensitive fields (Username, Password, Client ID, Client Secret) are RSA-encrypted at rest.

### 3.2 Configuration

1. Navigate to **Tenant Credentials Maintenance**
2. Click **Add New**
3. Fill in:

| Field | Description | Example |
|---|---|---|
| **Company Number** | Unique integer (links reports to credentials) | `1` |
| **Tenant Name** | Unique descriptive name | `MainCompany` |
| **Base URL** | Acumatica instance URL, no trailing slash | `https://server/AcumaticaERP` |
| **Username** | API user account (encrypted) | `apiuser@company.com` |
| **Password** | API user password (encrypted) | ••••••• |
| **Client ID** | OAuth2 Client ID (encrypted) | `abc123xyz` |
| **Client Secret** | OAuth2 Client Secret (encrypted) | ••••••• |

4. Click **Save**

### 3.3 Important Notes

- Company Number and Tenant Name must each be unique
- The API user must have OData access to the Generic Inquiry used by your definitions
- Use a dedicated service account, not a personal user
- Rotate credentials periodically

---

## 4. Creating a Report Definition

Report Definitions are the heart of the new engine. They describe the structure of your financial statement — which GL accounts feed into which report lines, how lines roll up into subtotals, and what formulas to apply.

Navigate to **FR101002 - Report Definition**.

### 4.1 Definition Header

| Field | Description | Example |
|---|---|---|
| **Definition Code** | Unique identifier (immutable after first save) | `BS_2024` |
| **Report Type** | Balance Sheet, Profit & Loss, Cash Flow, Changes in Equity, or Custom | `Balance Sheet` |
| **Description** | Friendly description (up to 255 characters) | `Balance Sheet FY2024` |
| **Active** | Whether this definition can be linked to reports | Checked |

### 4.2 Data Source — GI & Column Mapping

The definition tells the engine which Generic Inquiry to query and which columns contain the data.

| Field | Default | Description |
|---|---|---|
| **Generic Inquiry Name** | `TrialBalance` | The GI to query via OData (selectable from published GIs) |
| **Account Column** | `Account` | GL account code |
| **Account Type Column** | `Type` | Account type: A (Asset), L (Liability), E (Expense), I (Income), Q (Equity) |
| **Beginning Balance Column** | `BeginningBalance` | Beginning balance |
| **Ending Balance Column** | `EndingBalance` | Ending balance |
| **Debit Column** | `Debit` | Debit amounts |
| **Credit Column** | `Credit` | Credit amounts |

#### Detect Columns (Auto-Mapping)

Instead of manually entering column names:

1. Enter the **Generic Inquiry Name**
2. Click **Detect Columns** in the toolbar
3. The system connects to the API, reads column metadata, and auto-maps using case-insensitive name matching:
   - "Account" → Account Column (excludes "Subaccount" matches)
   - "Type" or "AccountType" → Type Column
   - "BeginningBalance", "Beginning", or "BegBal" → Beginning Balance Column
   - "EndingBalance", "Ending", or "YtdBalance" → Ending Balance Column
   - "Debit" → Debit Column
   - "Credit" → Credit Column
4. Review and adjust if needed, then **Save**

> Detect Columns requires Tenant Credentials to be configured. The button is only enabled when a GI Name is entered.

### 4.3 Rounding & Formatting

| Field | Options | Description |
|---|---|---|
| **Rounding Level** | Units, Thousands, Millions | Divides values by 1 / 1,000 / 1,000,000 |
| **Decimal Places** | 0, 1, 2 | Decimal places after rounding |

**Examples with raw value 1,808,344:**

| Rounding | Decimals | Output |
|---|---|---|
| Units | 0 | `1,808,344` |
| Thousands | 0 | `1,808` |
| Thousands | 1 | `1,808.3` |
| Millions | 2 | `1.81` |

Rounding uses `MidpointRounding.AwayFromZero`.

**Number display:**

| Value | Display |
|---|---|
| Positive | `1,234,567` |
| Negative | `(1,234,567)` — accounting bracket notation |
| Zero | `-` — dash |

### 4.4 Copy Definition

To create a variant of an existing definition:

1. Open the source definition in FR101002
2. Click **Copy Definition** in the toolbar
3. Confirm the dialog
4. A new definition is created:
   - Code = original + `_COPY`
   - Description = original + ` (Copy)`
   - All header settings (GI, columns, rounding) are copied
   - All line items are duplicated with the same sort order, codes, formulas, filters
5. Rename the code and description, then modify line items as needed

---

## 5. Configuring Line Items

Line items are the rows of your report definition. Each line produces two Word template placeholders: `{{LINECODE_CY}}` (current year) and `{{LINECODE_PY}}` (prior year).

### 5.1 Line Item Fields

| Field | Description |
|---|---|
| **Sort Order** | Processing sequence (lower = first). Order matters — subtotals and formulas depend on earlier lines. |
| **Line Code** | Unique identifier within the definition (up to 100 chars). Becomes the placeholder name. Use UPPER_CASE with underscores. |
| **Description** | Human-readable label (up to 255 chars, for your reference) |
| **Line Type** | How this line's value is calculated (see below) |
| **Account From** | Start of GL account range, inclusive (Account Range only) |
| **Account To** | End of GL account range, inclusive (Account Range only) |
| **Account Type Filter** | Restrict to: Asset (A), Liability (L), Expense (E), Income (I), Equity (Q), or All |
| **Balance Type** | Ending (default), Beginning, Debit, Credit, or Movement |
| **Sign Rule** | As-Is (default) or Flip Sign (multiply by -1) |
| **Group / Parent Line** | Links this line to a Subtotal parent |
| **Formula** | Mathematical expression for Calculated lines (up to 500 chars) |
| **Visible in Report** | If unchecked, value is calculated but placeholder resolves to empty |
| **Subaccount Filter** | Optional exact-match filter (up to 30 chars) |
| **Branch Filter** | Optional exact-match filter (selectable from Acumatica branches) |
| **Organization Filter** | Optional exact-match filter (selectable from Acumatica organizations) |
| **Ledger Filter** | Optional exact-match filter (selectable from Acumatica ledgers) |

### 5.2 Line Types

#### Account Range

Sums GL account balances within `AccountFrom` to `AccountTo`.

**Processing steps:**
1. Iterates all accounts from the GI
2. Includes accounts in the range (smart alphanumeric comparison that handles segmented codes like `1000-00`)
3. Optionally filters by Account Type
4. Reads the specified Balance Type (Ending, Beginning, Debit, Credit, or Movement where Movement = Debit − Credit)
5. Applies **sign normalization** — credit-normal accounts (L/I/Q) are flipped to positive
6. Applies the **Sign Rule** if set to Flip
7. Sums all matching values

**Example:**
```
Line Code:    CASH
Account From: 10100
Account To:   10199
Balance Type: Ending Balance
Sign Rule:    As-Is
→ Sums the ending balance of all accounts from 10100 to 10199
```

#### Subtotal

Sums all lines whose `Parent Line Code` equals this line's code.

```
Sort  Code              Type           Parent
10    CASH              Account Range  CURRENT_ASSETS
20    RECEIVABLES       Account Range  CURRENT_ASSETS
30    INVENTORY         Account Range  CURRENT_ASSETS
40    CURRENT_ASSETS    Subtotal       (blank)

→ CURRENT_ASSETS = CASH + RECEIVABLES + INVENTORY
```

Child lines must have a lower Sort Order than the Subtotal line.

#### Calculated

Evaluates a mathematical formula referencing other Line Codes.

**Supported:** `+`, `-`, `*`, `/`, parentheses `()`, numeric literals, unary minus

**Operator precedence:** `*` and `/` bind tighter than `+` and `-`

**Examples:**
```
NET_INCOME          = TOTAL_REVENUE - TOTAL_EXPENSES
GROSS_MARGIN_PCT    = (REVENUE - COGS) / REVENUE * 100
LIAB_EQUITY         = TOTAL_LIAB + TOTAL_EQUITY
ADJUSTED            = -ADJUSTMENTS + NET_INCOME
```

All referenced Line Codes must have a lower Sort Order. Unknown references default to 0 (with a trace warning). Division by zero returns 0.

#### Heading

Display-only line for section headers. No value is calculated. The placeholder resolves to empty. The `Visible` flag is automatically set to false.

### 5.3 Field Availability by Line Type

When you change the Line Type, irrelevant fields are automatically cleared and disabled.

| Field | Account Range | Subtotal | Calculated | Heading |
|---|---|---|---|---|
| Account From / To | ✓ | — | — | — |
| Account Type Filter | ✓ | — | — | — |
| Balance Type | ✓ | — | — | — |
| Sign Rule | ✓ | — | — | — |
| Parent Line Code | ✓ | ✓ | — | — |
| Formula | — | — | ✓ | — |
| Visible | ✓ | ✓ | ✓ | — |
| Dimension Filters | ✓ | — | — | — |

### 5.4 Validation Rules

The system validates on save:
- **Line Code** is required and must be unique within the definition
- **Account From** and **Account To** are required for Account Range lines
- **Formula** is required for Calculated lines
- Field-level error messages are shown for each violation

### 5.5 Per-Line Dimension Filters

Account Range lines can be restricted to specific dimensions:

| Filter | Type | Example |
|---|---|---|
| **Subaccount Filter** | Free text | `000-000` |
| **Branch Filter** | Selector | `HQ` |
| **Organization Filter** | Selector | `CORPORATE` |
| **Ledger Filter** | Selector | `ACTUAL` |

**How filters work:**

- **No filters set (default):** The engine uses pre-aggregated data (fastest path)
- **Any filter set:** The engine switches to per-row detail data and checks each row against all filters (AND logic, case-insensitive exact match)
- When zero rows match, the trace log shows filter values and sample data rows for debugging

**Important:** If the report header (FR101000) already filters to a specific branch, setting a *different* branch in a line filter will return 0 — that branch's data was never fetched. Best practice: leave report-level Branch/Organization blank when using per-line filters.

---

## 6. The Calculation Engine

The **ReportCalculationEngine** is the core of Definition Mode. Understanding how it works helps you design correct definitions.

### 6.1 Processing Pipeline

```
1. Load all line items for the definition, ordered by SortOrder ASC
2. For each line (skipping Headings):
   ┌─────────────────────────────────────────────────────────┐
   │  ACCOUNT RANGE                                          │
   │  ├── Iterate GI accounts in the From:To range           │
   │  ├── Filter by Account Type (if set)                    │
   │  ├── Filter by Dimension Filters (if any set)           │
   │  ├── Read the specified Balance Type                     │
   │  ├── Apply sign normalization (flip L/I/Q to positive)  │
   │  ├── Apply Sign Rule (flip if set)                      │
   │  └── Sum all matching values                            │
   ├─────────────────────────────────────────────────────────┤
   │  SUBTOTAL                                               │
   │  └── Sum all lines whose ParentLineCode = this LineCode │
   ├─────────────────────────────────────────────────────────┤
   │  CALCULATED                                             │
   │  └── Evaluate formula (recursive descent parser)        │
   └─────────────────────────────────────────────────────────┘
   Store CY and PY values separately
3. Build placeholder map:
   ├── Visible lines → formatted value (e.g. "1,234" or "(500)" or "-")
   └── Hidden lines & Headings → empty string
```

### 6.2 Sign Normalization

The engine automatically normalizes signs so that financial statements read naturally:

| Account Type | GL Natural Sign | Engine Treatment |
|---|---|---|
| Asset (A) | Debit (positive) | Kept as-is |
| Expense (E) | Debit (positive) | Kept as-is |
| Liability (L) | Credit (negative in GL) | **Flipped to positive** |
| Income (I) | Credit (negative in GL) | **Flipped to positive** |
| Equity (Q) | Credit (negative in GL) | **Flipped to positive** |

The **Sign Rule** on the line item is applied *after* normalization:
- **As-Is:** No additional change
- **Flip Sign:** Multiplies by -1 (use when you need a value to subtract in a subtotal, e.g. showing COGS as a deduction from Revenue)

### 6.3 Balance Types

| Balance Type | What It Returns |
|---|---|
| **Ending** (default) | Ending balance for the selected period |
| **Beginning** | Beginning balance for the selected period |
| **Debit** | Debit amount for the selected period |
| **Credit** | Credit amount for the selected period |
| **Movement** | Net activity: Debit minus Credit |

### 6.4 Account Code Comparison

The engine uses smart alphanumeric comparison for account ranges:
- Segmented codes (containing `-`) are compared segment by segment
- Numeric segments are compared numerically (`2` < `10`)
- Non-segmented codes use character-by-character comparison with numeric grouping
- Comparison is case-insensitive

### 6.5 Formula Evaluation

The formula parser uses recursive descent with proper operator precedence:

```
Precedence (highest to lowest):
  1. Parentheses ()
  2. Unary minus -
  3. Multiplication *, Division /
  4. Addition +, Subtraction -

Operands:
  - Line Code references (looked up from already-calculated values)
  - Numeric literals (e.g. 100, 1.5)

Safety:
  - Unknown Line Codes → 0 (with trace warning)
  - Division by zero → 0
```

### 6.6 Priority: Engine vs Legacy

When a Report Definition is linked to a report, **both** the engine and legacy placeholder systems run. The engine's placeholders take priority. Legacy placeholders fill in anything not covered by the definition.

This means you can use a definition for the main financial statement lines (`{{CASH_CY}}`, `{{TOTAL_ASSETS_CY}}`) while still referencing individual accounts for notes or schedules (`{{A10100_CY}}`).

### 6.7 Trace Logging

The engine writes detailed trace information during processing:

```
ReportCalculationEngine: Processing 15 line items for DefinitionID 42.
  [0010] CASH                           CY=       50,000  PY=       45,000
    Account range 10100:10199 matched 5 accounts → 50,000
  [0020] RECEIVABLES                    CY=      120,000  PY=      110,000
  [0030] INVENTORY                      CY=       80,000  PY=       75,000
  [0040] CURRENT_ASSETS                 CY=      250,000  PY=      230,000
  ...
  [0160] LIAB_EQUITY                    CY=      500,000  PY=      480,000
ReportCalculationEngine produced 32 placeholders.
```

When dimension filters match zero rows, the trace shows filter values and sample data for debugging.

Check traces at **System > Management > Trace** in Acumatica.

---

## 7. Creating a Word Template

The Word template is a standard `.docx` file with placeholders that the engine replaces with calculated values.

### 7.1 File Naming

The template filename **must** contain `FRTemplate` (case-sensitive).

- ✓ `BalanceSheet_FRTemplate_2024.docx`
- ✓ `PL_FRTemplate.docx`
- ✗ `BalanceSheet_Template_2024.docx`

### 7.2 Placeholder Format

Placeholders use double curly braces: `{{PLACEHOLDER_NAME}}`

### 7.3 Definition-Mode Placeholders

When a Report Definition is linked, use Line Codes from your definition:

| Placeholder | Description |
|---|---|
| `{{CASH_CY}}` | Cash line, Current Year |
| `{{CASH_PY}}` | Cash line, Prior Year |
| `{{TOTAL_ASSETS_CY}}` | Total Assets, Current Year |
| `{{NET_INCOME_PY}}` | Net Income, Prior Year |
| `{{CY}}` | Current year number (e.g. `2024`) |
| `{{PY}}` | Prior year number (e.g. `2023`) |

Every line in your definition automatically produces both `_CY` and `_PY` placeholders.

### 7.4 Placeholder Rules

- Always include year suffix: `_CY` or `_PY`
- Matching is case-insensitive
- Do not include spaces inside braces
- Do not split a placeholder across multiple lines in Word
- Type placeholders directly — do NOT copy/paste from other sources (hidden formatting characters can break matching)
- Avoid mixed formatting (bold/italic) within a single placeholder
- Maximum **1,000 placeholders** per template

### 7.5 Example Template: Balance Sheet

```
                        ABC COMPANY
                        BALANCE SHEET
                    As at December 31, {{CY}}

                                            {{CY}}              {{PY}}
ASSETS
Current Assets
  Cash and Cash Equivalents             {{CASH_CY}}         {{CASH_PY}}
  Accounts Receivable                   {{AR_CY}}           {{AR_PY}}
  Inventory                             {{INV_CY}}          {{INV_PY}}
Total Current Assets                    {{CURR_ASSETS_CY}}  {{CURR_ASSETS_PY}}

Non-Current Assets
  Property, Plant & Equipment           {{PPE_CY}}          {{PPE_PY}}
  Accumulated Depreciation              {{ACCUM_DEPR_CY}}   {{ACCUM_DEPR_PY}}
Total Non-Current Assets                {{NONCURR_ASSETS_CY}} {{NONCURR_ASSETS_PY}}

TOTAL ASSETS                            {{TOTAL_ASSETS_CY}} {{TOTAL_ASSETS_PY}}

LIABILITIES
Current Liabilities
  Accounts Payable                      {{AP_CY}}           {{AP_PY}}
  Accrued Expenses                      {{ACCRUED_CY}}      {{ACCRUED_PY}}
Total Current Liabilities               {{CURR_LIAB_CY}}    {{CURR_LIAB_PY}}

Non-Current Liabilities
  Long-Term Loans                       {{LOANS_CY}}        {{LOANS_PY}}
Total Non-Current Liabilities           {{NONCURR_LIAB_CY}} {{NONCURR_LIAB_PY}}

TOTAL LIABILITIES                       {{TOTAL_LIAB_CY}}   {{TOTAL_LIAB_PY}}

EQUITY
  Share Capital                         {{SHARE_CAP_CY}}    {{SHARE_CAP_PY}}
  Retained Earnings                     {{RET_EARN_CY}}     {{RET_EARN_PY}}
TOTAL EQUITY                            {{TOTAL_EQUITY_CY}} {{TOTAL_EQUITY_PY}}

TOTAL LIABILITIES & EQUITY              {{LIAB_EQUITY_CY}}  {{LIAB_EQUITY_PY}}
```

### 7.6 Template Design Tips

- Use Word tables for clean column alignment in comparative reports
- Add currency symbols outside the placeholder: `${{CASH_CY}}`
- Placeholders work in headers, footers, tables, and body text
- Start with a small test template before building the full report
- Use `{{CY}}` and `{{PY}}` in column headers for dynamic year labels

---

## 8. Generating a Report

### 8.1 Step-by-Step

1. Navigate to **FR101000 - Financial Report**
2. Create a new report record (auto-saves on insert)
3. Fill in the header:

| Field | Required | Description |
|---|---|---|
| **Template Name** | Yes | Descriptive name (up to 225 chars) |
| **Company Number** | Yes | Links to Tenant Credentials |
| **Current Year** | Yes | Selectable from Acumatica financial years |
| **Financial Month** | Yes | Dropdown 01-12, defaults to December |
| **Branch** | No | Filter data to a specific branch |
| **Organization** | No | Filter data to a specific organization |
| **Ledger** | No | Filter to a specific ledger (e.g. `ACTUAL`) |
| **Report Definition** | **Recommended** | Link to a Report Definition for structured calculation |

4. Upload a Word template (filename must contain `FRTemplate`)
5. Click **Save**
6. Check the **Select** checkbox for your report
7. Click **Generate Report**
8. Wait for status to change to **Ready to Download** (typically 1-5 minutes)
9. Click **Download Report**

### 8.2 What Happens During Generation

```
Phase 1: Validation & Setup
  ├── Verify record selection, template file, credentials
  ├── Load Report Definition (if linked) → GI mapping, rounding settings
  └── Authenticate with Acumatica API (OAuth2)

Phase 2: Template Analysis
  ├── Extract all placeholders from the Word template
  └── Categorize: wildcard range, exact range, regular

Phase 3: Data Fetching (6 parallel API calls)
  ├── Current year period data
  ├── Prior year period data
  ├── January beginning balance (current year)
  ├── January beginning balance (prior year)
  ├── Cumulative CY data (Jan → selected month)
  └── Cumulative PY data (Jan → Dec prior year)

Phase 4: Calculation Engine (if definition linked)
  ├── Process all line items in SortOrder
  ├── Account Range → sum GL data with sign normalization
  ├── Subtotal → sum child lines
  ├── Calculated → evaluate formulas
  └── Build placeholder map with formatted values

Phase 5: Legacy Processing (fills gaps not covered by definition)
  ├── Regular placeholders (e.g. {{A10100_CY}})
  ├── Exact range placeholders (e.g. {{A10100:A10199_e_CY}})
  └── Wildcard range placeholders (e.g. {{A????:B????_e_CY}})

Phase 6: Template Population & Cleanup
  ├── Replace all placeholders in headers, footers, body
  ├── Add year constants ({{CY}}, {{PY}})
  ├── Save generated file to Acumatica
  ├── Delete temporary files
  └── Clear credential cache
```

**Engine placeholders take priority** over legacy placeholders. If both produce a value for the same key, the engine wins.

### 8.3 Timeout Protection

Report generation has a built-in **15-minute timeout**. If exceeded, the process is automatically cancelled and the status is set to Failed.

---

## 9. Report Status & Workflow

### 9.1 Status Values

| Status | Display | Meaning |
|---|---|---|
| `File not Generated` | Pending | Initial state — can edit and generate |
| `File Generation In Progress` | In Progress | Background generation running |
| `Ready to Download` | Ready | Generation succeeded — download available |
| `Failed to Generate File` | Failed | Error occurred |

### 9.2 Status Lifecycle

```
         ┌──────────────┐
         │   Pending     │◄──── Reset Status (from any state)
         └──────┬───────┘
                │ Generate Report
                ▼
         ┌──────────────┐
         │  In Progress  │
         └───┬──────┬───┘
        Success    Error/Timeout
             │        │
             ▼        ▼
      ┌──────────┐  ┌────────┐
      │  Ready   │  │ Failed │
      └──────────┘  └────────┘
```

### 9.3 System-Wide Generation Lock

Only **one** report can generate at a time across the entire system. While any report is In Progress:
- All Generate and Download buttons are disabled for all users
- All report fields become read-only
- Other users must wait for completion

### 9.4 Reset Status

Recovers a stuck or failed report:

1. Select the report (checkbox)
2. Click **Reset Status**
3. Confirm the dialog
4. Status resets to Pending, generated file reference is cleared
5. All buttons are re-enabled system-wide

Use this when:
- A report has been stuck In Progress (e.g. after a server restart)
- A report failed and you want to retry
- You want to clear a completed report and regenerate

### 9.5 Regenerating

Clicking Generate Report on a previously completed report overwrites the previous file. Download first if you want to keep the old version. The system does not maintain version history.

### 9.6 Expected Generation Times

| Data Volume | Typical Time |
|---|---|
| Small (< 1,000 accounts) | 1-2 minutes |
| Medium (1,000-5,000 accounts) | 2-4 minutes |
| Large (5,000-10,000 accounts) | 4-8 minutes |
| Very Large (> 10,000 accounts) | 8-15 minutes |

---

## 10. Worked Examples

### 10.1 Balance Sheet Definition

**Definition Code:** `BS` | **Report Type:** Balance Sheet | **Rounding:** Thousands, 0 decimals

| Sort | Line Code | Type | Account From | Account To | Type Filter | Sign | Parent |
|---|---|---|---|---|---|---|---|
| 10 | CASH | Account Range | 10100 | 10199 | All | As-Is | CURR_ASSETS |
| 20 | AR | Account Range | 11000 | 11999 | All | As-Is | CURR_ASSETS |
| 30 | INVENTORY | Account Range | 12000 | 12999 | All | As-Is | CURR_ASSETS |
| 40 | CURR_ASSETS | Subtotal | | | | | TOTAL_ASSETS |
| 50 | PPE | Account Range | 15000 | 15999 | All | As-Is | NONCURR_ASSETS |
| 60 | NONCURR_ASSETS | Subtotal | | | | | TOTAL_ASSETS |
| 70 | TOTAL_ASSETS | Subtotal | | | | | |
| 80 | AP | Account Range | 20000 | 20999 | All | As-Is | CURR_LIAB |
| 90 | CURR_LIAB | Subtotal | | | | | TOTAL_LIAB |
| 100 | LOANS | Account Range | 25000 | 25999 | All | As-Is | NONCURR_LIAB |
| 110 | NONCURR_LIAB | Subtotal | | | | | TOTAL_LIAB |
| 120 | TOTAL_LIAB | Subtotal | | | | | |
| 130 | SHARE_CAPITAL | Account Range | 30000 | 30999 | All | As-Is | TOTAL_EQUITY |
| 140 | RET_EARNINGS | Account Range | 32000 | 32999 | All | As-Is | TOTAL_EQUITY |
| 150 | TOTAL_EQUITY | Subtotal | | | | | |
| 160 | LIAB_EQUITY | Calculated | | | | | |

**Line 160 Formula:** `TOTAL_LIAB + TOTAL_EQUITY`

**Verification:** `TOTAL_ASSETS` should equal `LIAB_EQUITY`.

### 10.2 P&L with Branch Dimension Filters

A multi-branch company wants revenue broken down by branch.

| Sort | Line Code | Type | Account From | Account To | Branch Filter | Parent |
|---|---|---|---|---|---|---|
| 10 | REV_HQ | Account Range | 40000 | 49999 | HQ | |
| 20 | REV_RETAIL | Account Range | 40000 | 49999 | RETAIL | |
| 30 | REV_WAREHOUSE | Account Range | 40000 | 49999 | WAREHOUSE | |
| 40 | TOTAL_REVENUE | Calculated | | | | |
| 50 | COGS | Account Range | 50000 | 59999 | | |
| 60 | GROSS_PROFIT | Calculated | | | | |

**Formulas:**
- Line 40: `REV_HQ + REV_RETAIL + REV_WAREHOUSE`
- Line 60: `TOTAL_REVENUE - COGS`

> Leave the report header Branch and Organization **blank** so all branch data is fetched.

### 10.3 Hidden Lines for Intermediate Calculations

| Sort | Line Code | Type | Formula | Visible |
|---|---|---|---|---|
| 10 | REVENUE | Account Range | | Yes |
| 20 | COGS | Account Range | | Yes |
| 25 | _GROSS_AMT | Calculated | REVENUE - COGS | **No** |
| 30 | GROSS_MARGIN_PCT | Calculated | _GROSS_AMT / REVENUE * 100 | Yes |

`_GROSS_AMT` is calculated and available for formulas, but its placeholder resolves to empty in the Word template. Only `GROSS_MARGIN_PCT` appears.

### 10.4 Using Copy Definition for Variants

1. Create a consolidated Balance Sheet definition `BS_CONSOL`
2. Click **Copy Definition** → creates `BS_CONSOL_COPY`
3. Rename to `BS_HQ`
4. Add Branch Filter = `HQ` to each Account Range line
5. Now you have both consolidated and branch-specific versions sharing the same structure

---

## 11. Troubleshooting Guide

### 11.1 Engine / Definition Issues

| Problem | Cause | Solution |
|---|---|---|
| Subtotal shows 0 | Child lines missing Parent Line Code | Set Parent Line Code on each child line |
| Subtotal shows 0 | Children have higher Sort Order | Ensure children have LOWER Sort Order than the Subtotal |
| Formula shows 0 | Referenced Line Code not yet calculated | Ensure referenced lines have a LOWER Sort Order |
| Formula shows 0 | Typo in formula Line Code reference | Check trace log for "unknown LineCode" warnings |
| Negative where positive expected | Credit-normal account not flipped | Engine auto-flips L/I/Q. Use Flip Sign for manual override. |
| Positive where negative expected | Double-flip | Remove Flip Sign if the engine already normalizes the account type |
| Account Range shows 0 | Account range doesn't match any accounts | Verify AccountFrom/AccountTo match your chart of accounts |
| Account Range shows 0 | Account Type Filter too restrictive | Try "All Types" to see if data appears |
| Per-line filter returns 0 | Report header scoped to different branch | Leave report header Branch/Org blank when using per-line filters |
| Per-line filter returns 0 | Filter value doesn't match GI data exactly | Check trace log for sample data rows; verify exact values |
| Values not rounded | Rounding set to Units | Change Rounding Level to Thousands or Millions in the definition |

### 11.2 Template / Placeholder Issues

| Problem | Cause | Solution |
|---|---|---|
| Placeholder shows `{{CASH_CY}}` in output | Line Code doesn't match | Verify Line Code in definition matches placeholder exactly |
| Placeholder not replaced | Hidden formatting in Word | Delete and retype the placeholder (don't copy/paste) |
| Placeholder not replaced | Missing year suffix | Must include `_CY` or `_PY` |
| All values are 0 | API returns no data | Check GI name, period, and that the GI has data |
| "Template contains X placeholders" error | Too many placeholders | Maximum is 1,000. Simplify or split into multiple reports. |

### 11.3 Generation Issues

| Problem | Cause | Solution |
|---|---|---|
| Generate button disabled | Another report In Progress | Wait for completion, or Reset Status on the stuck report |
| Report stuck In Progress | Server restart or crash | Use **Reset Status** to recover |
| "Report generation timed out" | Very large dataset or slow API | Simplify template, try off-hours |
| "Failed to authenticate" | Invalid credentials | Verify all fields in Tenant Credentials |
| "No API credentials found" | Wrong Company Number | Verify Company Number matches a tenant record |
| Generation fails immediately | Template file missing | Upload file with `FRTemplate` in the name |

### 11.4 Pre-Generation Checklist

- ☐ Tenant Credentials configured for the Company Number
- ☐ Report Definition is Active and has line items configured
- ☐ Template Name filled in, Current Year and Financial Month set
- ☐ Template file attached (filename contains `FRTemplate`)
- ☐ Report Definition linked in the report record
- ☐ No other report shows "In Progress" status
- ☐ Record is saved and selected (checkbox checked)
- ☐ Financial data posted for the selected period

### 11.5 Using Trace Logs

Check **System > Management > Trace** for:
- `ReportCalculationEngine: Processing X line items for DefinitionID Y` — confirms engine ran
- `[0010] CASH  CY=50,000  PY=45,000` — line-by-line values
- `Account range 10100:10199 matched 5 accounts → 50,000` — account match counts
- `Account range ... [filtered] matched 0 of 200 detail rows` — filter debugging with sample data
- `Filters: Sub='...' Branch='...' Org='...' Ledger='...'` — filter values when zero matches
- `Formula references unknown LineCode 'TYPO'` — formula reference errors
- `📊 Extracted X total placeholders` — placeholder categorization counts
- `Total report generation completed in X ms` — performance timing

---

## 12. Best Practices & Tips

### 12.1 Definition Design

- Use meaningful Line Codes: `CASH`, `TOTAL_ASSETS`, `NET_INCOME` — not `LINE1`, `LINE2`
- Set Sort Order with gaps (10, 20, 30...) to allow inserting lines later
- Always ensure dependencies have lower Sort Order than the lines that reference them
- Use hidden lines (`Visible = false`) for intermediate calculations that shouldn't appear in the report
- Use **Copy Definition** to create variants instead of rebuilding from scratch
- Test with a small definition (3-5 lines) before building the full statement

### 12.2 Template Design

- Always include `FRTemplate` in the filename
- Use descriptive Template Names: "Balance Sheet Q4 2024" not "Report1"
- Type placeholders directly in Word
- Use Word tables for clean column alignment
- Add `$` or currency symbols outside the placeholder: `${{CASH_CY}}`
- Use `{{CY}}` and `{{PY}}` in column headers for dynamic year labels
- Start with a small test template before building the full report

### 12.3 Dimension Filters

- Leave report-level Branch/Organization blank when using per-line filters
- Per-line filters use exact match — verify the exact values in your GI data
- Check the trace log when filters return unexpected zeros

### 12.4 Performance

- Generate large reports during off-hours
- Coordinate with team to avoid generation conflicts (system-wide lock)
- Keep templates under 1,000 placeholders
- Legacy wildcard placeholders are more expensive than definition-mode lines

### 12.5 Workflow

- Use **Reset Status** to recover stuck reports instead of waiting
- Regenerating overwrites the previous file — download first if you want to keep it
- For monthly close: generate preliminary reports before close, then final reports after
- Use dedicated API user accounts and rotate credentials periodically

---

## 13. Appendix A: Legacy Placeholder Reference

Legacy placeholders work when **no** Report Definition is linked, or alongside a definition (definition placeholders take priority). They use raw account codes instead of Line Codes and do not include sign normalization, rounding, or accounting formatting.

### Year Constants
```
{{CY}}  - Current Year (e.g., 2024)
{{PY}}  - Previous Year (e.g., 2023)
```

### Simple Account Balance
```
{{AccountNumber_CY}}  - Ending balance, current year
{{AccountNumber_PY}}  - Ending balance, previous year
```

### Sum by Prefix
```
{{Sum[Level]_[Prefix]_CY}}        - Sum accounts by prefix (ending balance)
{{DebitSum[Level]_[Prefix]_CY}}   - Sum debits by prefix
{{CreditSum[Level]_[Prefix]_CY}}  - Sum credits by prefix
{{BegSum[Level]_[Prefix]_CY}}     - Sum beginning balances by prefix
```
Level = number of prefix characters (1-6). Example: `{{Sum1_A_CY}}` sums all "A" accounts.

### Specific Balance Types
```
{{Account_debit_CY}}    - Cumulative debit (Jan to selected month)
{{Account_credit_CY}}   - Cumulative credit (Jan to selected month)
{{Account_Jan1_CY}}     - January 1 beginning balance
```

### Exact Account Range
```
{{Start:End_e_CY}}  - Sum ending balances in range
{{Start:End_b_CY}}  - Sum beginning balances
{{Start:End_c_CY}}  - Sum credits
{{Start:End_d_CY}}  - Sum debits
```

### Wildcard Range
```
{{A????:B????_e_CY}}  - ? matches any single character
```
Both patterns must be the same length.

### Dimensional Filtering
```
{{Account_sb[Subacct]_CY}}        - Filter by subaccount
{{Account_br[Branch]_CY}}         - Filter by branch
{{Account_or[Org]_CY}}            - Filter by organization
{{Account_ld[Ledger]_CY}}         - Filter by ledger
{{Account_bt[BalType]_CY}}        - Specific balance type
{{Account_sb[Sub]_br[Br]_CY}}     - Multiple dimensions combined
```

---

## 14. Appendix B: Error Message Reference

| Error Message | Cause | Solution |
|---|---|---|
| "Please select a template to generate the report." | No record selected | Check Select checkbox |
| "The selected template does not have any attached files." | Missing template | Upload file with `FRTemplate` in name |
| "A report generation process is already running for this template." | Another report generating | Wait or Reset Status |
| "Failed to authenticate. Please check credentials." | Invalid credentials | Verify Tenant Credentials |
| "Current Year is not specified for the selected report." | Missing field | Enter Current Year |
| "No generated file is available for download." | Not generated | Generate first |
| "No API credentials found for company" | Missing credentials | Configure Tenant Credentials |
| "Tenant mapping not found." | Company Number mismatch | Verify Company Number |
| "Template contains X placeholders. Maximum allowed is 1000." | Too many | Simplify or split |
| "Report generation timed out after 15 minutes." | Timeout | Simplify template |
| "The selected template file is empty or could not be retrieved." | Corrupted file | Re-upload |
| "Definition Code must be unique." | Duplicate | Choose different code |
| "Line Code must be unique within the same definition." | Duplicate | Use unique code |
| "Account From is required for Account Range line types." | Missing field | Enter Account From |
| "Account To is required for Account Range line types." | Missing field | Enter Account To |
| "Formula is required for Calculated line types." | Missing field | Enter formula |
| "Generic Inquiry Name is required to detect columns." | No GI name | Enter GI Name |
| "No columns were detected from the specified Generic Inquiry." | GI empty/invalid | Verify GI name |
| "Tenant Name must be unique." | Duplicate | Choose different name |
| "Company Number is required." | Missing field | Enter Company Number |

---

## 15. Appendix C: Field Reference

### FR101000 - Financial Report

| Field | Type | Required | Description |
|---|---|---|---|
| Template Name | String(225) | Yes | Report template name |
| Description | String(50) | No | Brief description |
| Company Number | Integer | Yes | Links to Tenant Credentials |
| Current Year | String(4) | Yes | Reporting year (selector from financial years) |
| Financial Month | String(2) | Yes | Period month 01-12, default "12" |
| Branch | String(10) | No | Branch filter (selector) |
| Organization | String(50) | No | Organization filter (selector) |
| Ledger | String(20) | No | Ledger filter (selector) |
| Report Definition | Integer | No | Link to FR101002 definition (selector) |
| Status | String(20) | Read-only | Current report status |
| Select | Boolean | No | Selection checkbox |

### FR101002 - Report Definition Header

| Field | Type | Required | Description |
|---|---|---|---|
| Definition Code | String(50) | Yes | Unique identifier (immutable after save) |
| Description | String(255) | No | Friendly description |
| Report Type | String(10) | Yes | BS, PL, CF, EQ, or CU |
| Active | Boolean | Yes | Default true |
| GI Name | String(100) | Yes | Generic Inquiry name, default "TrialBalance" |
| Account Column | String(100) | Yes | Default "Account" |
| Type Column | String(100) | Yes | Default "Type" |
| Beginning Bal Column | String(100) | Yes | Default "BeginningBalance" |
| Ending Bal Column | String(100) | Yes | Default "EndingBalance" |
| Debit Column | String(100) | Yes | Default "Debit" |
| Credit Column | String(100) | Yes | Default "Credit" |
| Rounding Level | String(10) | Yes | UNITS, THOUS, or MILL |
| Decimal Places | Integer | Yes | 0, 1, or 2 |

### FR101002 - Line Items

| Field | Type | Description |
|---|---|---|
| Sort Order | Integer | Processing sequence (lower = first) |
| Line Code | String(100) | Unique identifier → `{{CODE_CY}}` placeholder |
| Description | String(255) | Human-readable label |
| Line Type | String(20) | ACCOUNT, SUBTOTAL, CALCULATED, HEADING |
| Account From | String(50) | Range start (Account Range only) |
| Account To | String(50) | Range end (Account Range only) |
| Account Type Filter | String(5) | A, L, E, I, Q, or blank for All |
| Balance Type | String(10) | ENDING, BEGINNING, DEBIT, CREDIT, MOVEMENT |
| Sign Rule | String(10) | ASIS or FLIP |
| Parent Line Code | String(100) | Links to Subtotal parent |
| Formula | String(500) | Expression for Calculated lines |
| Visible in Report | Boolean | Default true |
| Subaccount Filter | String(30) | Exact match filter |
| Branch Filter | String(30) | Exact match filter (selector) |
| Organization Filter | String(30) | Exact match filter (selector) |
| Ledger Filter | String(20) | Exact match filter (selector) |

### Tenant Credentials

| Field | Type | Encrypted | Description |
|---|---|---|---|
| Company Number | Integer | No | Unique identifier |
| Tenant Name | String(50) | No | Unique tenant name |
| Base URL | String(255) | No | API base URL |
| Username | String | RSA | API username |
| Password | String | RSA | API password |
| Client ID | String | RSA | OAuth Client ID |
| Client Secret | String | RSA | OAuth Client Secret |

---

## 16. Appendix D: Glossary

| Term | Definition |
|---|---|
| **Report Definition** | A configured structure in FR101002 that describes how a financial statement is calculated |
| **Line Code** | Unique identifier for a report line; becomes `{{CODE_CY}}` / `{{CODE_PY}}` in templates |
| **Sort Order** | Processing sequence for line items — lower numbers are processed first |
| **Account Range** | Line type that sums GL accounts within a From:To range |
| **Subtotal** | Line type that sums all child lines (those with matching Parent Line Code) |
| **Calculated** | Line type that evaluates a formula referencing other Line Codes |
| **Heading** | Display-only line type for section headers |
| **Sign Normalization** | Automatic flipping of credit-normal accounts (L/I/Q) to positive values |
| **Flip Sign** | Manual sign override that multiplies a value by -1 after normalization |
| **Movement** | Balance type calculated as Debit minus Credit (net activity) |
| **Rounding Level** | Scale factor: Units (÷1), Thousands (÷1,000), Millions (÷1,000,000) |
| **Parent Line Code** | Links a line to a Subtotal parent for automatic summation |
| **CY / PY** | Current Year / Previous Year |
| **GI** | Generic Inquiry — Acumatica's configurable data query tool |
| **OData** | Open Data Protocol — RESTful API used to fetch GI data |
| **FRTemplate** | Required text in template filenames for the system to recognize them |
| **Legacy Mode** | Report generation using raw account-code placeholders without a definition |
| **Definition Mode** | Report generation using a structured Report Definition with the calculation engine |
| **RSA Encryption** | Encryption method used for storing sensitive credential fields |
| **Reset Status** | Action that recovers a stuck/failed report back to Pending |
| **Copy Definition** | Action that duplicates a definition with all its line items |
| **Detect Columns** | Action that auto-maps GI column names to definition fields |

---

*Financial Report Application User Guide v2.0 — March 2026*
