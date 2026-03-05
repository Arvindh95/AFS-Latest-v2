# Financial Report Module - User Manual

## Table of Contents

1. [Overview](#1-overview)
2. [Module Architecture](#2-module-architecture)
3. [Screens Reference](#3-screens-reference)
4. [Setting Up API Credentials](#4-setting-up-api-credentials)
5. [Creating a Report Definition](#5-creating-a-report-definition)
6. [Configuring Line Items](#6-configuring-line-items)
7. [Line Types Explained](#7-line-types-explained)
8. [Dimension Filters (Per-Line)](#8-dimension-filters-per-line)
9. [Rounding & Formatting](#9-rounding--formatting)
10. [Creating a Word Template](#10-creating-a-word-template)
11. [Generating a Report](#11-generating-a-report)
12. [Legacy Mode vs Definition Mode](#12-legacy-mode-vs-definition-mode)
13. [Worked Examples](#13-worked-examples)
14. [Troubleshooting](#14-troubleshooting)
15. [Technical Reference](#15-technical-reference)

---

## 1. Overview

The Financial Report Module is a customization for Acumatica ERP that automates the generation of financial statements (Balance Sheet, Profit & Loss, Cash Flow, Changes in Equity, and custom reports) by:

1. Fetching GL data from a configurable Generic Inquiry (GI) via OData API
2. Calculating line values using a configurable report definition
3. Populating a Microsoft Word template with calculated values
4. Saving the generated document back to Acumatica

The module eliminates manual financial statement preparation and supports multi-company, multi-branch, and multi-period reporting.

---

## 2. Module Architecture

```
+---------------------------+
|  FR101000                 |   Main screen: select template, year, period,
|  Financial Report         |   branch/org/ledger, link a definition,
|  Generation               |   click Generate
+---------------------------+
            |
            v
+---------------------------+
|  ReportGenerationService  |   Orchestrator: fetches data, runs engine,
|  (Services/)              |   populates Word template, saves file
+---------------------------+
     |              |
     v              v
+-----------+  +-----------------------+
| Financial |  | ReportCalculation     |
| DataSvc   |  | Engine                |
| (OData)   |  | (Definition-based)    |
+-----------+  +-----------------------+
     |              |
     v              v
+-----------+  +-----------------------+
| GI via    |  | FLRTReportDefinition  |
| OData API |  | FLRTReportLineItem    |
+-----------+  | (FR101002)            |
               +-----------------------+
```

### Key Components

| Component | Purpose |
|---|---|
| **FR101000** | Main report generation screen (select template, generate, download) |
| **FR101002** | Report Definition maintenance (define line items, GI mapping, rounding) |
| **ReportGenerationService** | Orchestrates the end-to-end report generation process |
| **FinancialDataService** | Fetches GL data from the GI via OData API |
| **ReportCalculationEngine** | Processes line items and produces placeholder values |
| **WordTemplateService** | Extracts placeholders from and populates Word templates |
| **GIColumnMapping** | Maps GI column names to expected data fields |
| **RoundingSettings** | Carries rounding configuration to the engine |

---

## 3. Screens Reference

### FR101000 - Financial Report Generation

The main screen where you:
- Upload a Word template (`.docx`)
- Select the reporting year, month, branch, organization, and ledger
- Optionally link a Report Definition
- Click **Generate** to produce the report
- Click **Download** to get the generated file

### FR101002 - Report Definition

The configuration screen where you define:
- The GI data source and column mapping
- Report line items (account ranges, subtotals, formulas)
- Rounding and formatting settings

---

## 4. Setting Up API Credentials

Before the module can fetch GL data, you need API credentials configured in the **Tenant Credentials** screen.

### Required Fields

| Field | Description | Example |
|---|---|---|
| **Tenant Name** | The Acumatica tenant/company name | `MyCompany` |
| **Base URL** | The Acumatica instance URL | `https://mycompany.acumatica.com` |
| **Client ID** | OAuth2 client ID | `xxxxxxxx-xxxx-xxxx-xxxx` |
| **Client Secret** | OAuth2 client secret | `(your secret)` |
| **Username** | API user account | `admin@MyCompany` |
| **Password** | API user password | `(your password)` |

### Important Notes
- The API user must have permissions to access the Generic Inquiry via OData
- The GI must be published and accessible via the OData endpoint
- Use a dedicated API user (not a regular user account) for reliability

---

## 5. Creating a Report Definition

Navigate to **FR101002 - Report Definition**.

### Step 1: Create the Definition Header

| Field | Description | Example |
|---|---|---|
| **Definition Code** | Unique identifier for this definition | `BS_2024` |
| **Report Type** | Type of financial statement | Balance Sheet |
| **Description** | Friendly description | `Balance Sheet FY2024` |
| **Active** | Whether this definition can be used | Checked |

### Step 2: Configure the Data Source

The **Data Source** section tells the module which GI to query and which columns to read.

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
2. Click the **Detect Columns** button in the grid toolbar
3. The system connects to the API, fetches one row from the GI, and auto-maps column names
4. Review and adjust the mapped columns if needed
5. Click **Save**

> **Note:** The Detect Columns button is only enabled when a GI Name is entered.

### Step 3: Configure Formatting

| Field | Options | Description |
|---|---|---|
| **Rounding Level** | Units, Thousands, Millions | Scale factor for displayed values |
| **Decimal Places** | 0, 1, 2 | Number of decimal places after rounding |

**Examples:**
- Units + 0 decimals: `1,808,344`
- Thousands + 0 decimals: `1,808`
- Thousands + 1 decimal: `1,808.3`
- Millions + 2 decimals: `1.81`

---

## 6. Configuring Line Items

Line items define what the report calculates. Each line produces two Word template placeholders: `{{LINECODE_CY}}` (current year) and `{{LINECODE_PY}}` (prior year).

### Line Item Fields

| Field | Description |
|---|---|
| **Sort Order** | Processing sequence (lower = first). Order matters because subtotals and formulas depend on earlier lines. |
| **Line Code** | Unique identifier used in Word template placeholders. Use UPPER_CASE with underscores. |
| **Description** | Human-readable description (for your reference only) |
| **Line Type** | How this line's value is calculated (see Section 7) |
| **Account From** | Start of GL account range (inclusive) |
| **Account To** | End of GL account range (inclusive) |
| **Account Type Filter** | Restrict to specific account type (A/L/E/I/Q) or All |
| **Balance Type** | Which balance to use: Ending, Beginning, Debit, Credit, Movement |
| **Sign Rule** | As-Is or Flip Sign (multiply by -1) |
| **Group / Parent Line** | Links this line to a Subtotal parent |
| **Formula** | Mathematical expression for Calculated lines |
| **Visible in Report** | If unchecked, value is calculated but placeholder resolves to empty |

### Field Availability by Line Type

| Field | Account Range | Subtotal | Calculated | Heading |
|---|---|---|---|---|
| Account From / To | Yes | - | - | - |
| Account Type Filter | Yes | - | - | - |
| Balance Type | Yes | - | - | - |
| Sign Rule | Yes | - | - | - |
| Parent Line Code | Yes | Yes | - | - |
| Formula | - | - | Yes | - |
| Visible | Yes | Yes | Yes | - |

---

## 7. Line Types Explained

### Account Range

Sums GL account balances within the specified `AccountFrom` to `AccountTo` range.

**How it works:**
1. Iterates all accounts returned by the GI
2. Includes only accounts that fall within the `AccountFrom:AccountTo` range (alphanumeric comparison)
3. Optionally filters by Account Type (Asset, Liability, etc.)
4. Gets the specified balance type (Ending, Beginning, Debit, Credit, Movement)
5. Applies automatic sign normalization (credit-normal accounts like Liability/Income/Equity are flipped to positive)
6. Applies the Sign Rule if set to "Flip"
7. Sums all matching values

**Example:**
```
Line Code:    CASH
Account From: 10100
Account To:   10199
Balance Type: Ending Balance
Sign Rule:    As-Is
```
This sums the ending balance of all accounts from 10100 to 10199.

### Subtotal

Sums all lines that have this line's code as their `Parent Line Code`.

**How it works:**
1. Finds all lines in the definition where `ParentLineCode = this LineCode`
2. Sums their already-calculated values
3. No account range or formula needed

**Example:**
```
Sort Order  Line Code         Line Type      Parent Line
10          CASH              Account Range  CURRENT_ASSETS
20          RECEIVABLES       Account Range  CURRENT_ASSETS
30          INVENTORY         Account Range  CURRENT_ASSETS
40          CURRENT_ASSETS    Subtotal       (blank)
```
`CURRENT_ASSETS` = `CASH` + `RECEIVABLES` + `INVENTORY`

> **Important:** Child lines (Sort Order 10-30) must be processed BEFORE the Subtotal line (Sort Order 40).

### Calculated

Evaluates a mathematical formula referencing other Line Codes.

**Supported operators:** `+`, `-`, `*`, `/`, `(`, `)`

**How it works:**
1. Parses the Formula expression
2. Replaces each Line Code reference with its already-calculated value
3. Evaluates the expression respecting operator precedence
4. Supports parentheses for grouping

**Examples:**
```
Line Code: NET_INCOME
Formula:   TOTAL_REVENUE - TOTAL_EXPENSES

Line Code: WORKING_CAPITAL
Formula:   CURRENT_ASSETS - CURRENT_LIABILITIES

Line Code: GROSS_MARGIN_PCT
Formula:   GROSS_PROFIT / TOTAL_REVENUE * 100
```

> **Important:** All referenced Line Codes must have a lower Sort Order (be calculated first).

### Heading

Display-only line used for section headers in the Word template. No value is calculated. The placeholder resolves to an empty string.

---

## 8. Dimension Filters (Per-Line)

Each Account Range line can optionally be restricted to specific dimensions. These filters appear in the **Dimension Filters** section of the line item edit panel.

### Available Filters

| Filter | Type | Description |
|---|---|---|
| **Subaccount Filter** | Free text | Exact subaccount code (e.g. `000-000`) |
| **Branch Filter** | Dropdown | Select from Acumatica branches |
| **Organization Filter** | Dropdown | Select from Acumatica organizations |
| **Ledger Filter** | Dropdown | Select from Acumatica ledgers (reserved for future use) |

### How Filters Work

**No filters set (default):**
- The engine uses the pre-aggregated data (all subaccounts, branches, and organizations summed per account)
- This is the most common scenario and is the fastest path

**Any filter set:**
- The engine switches to the per-row detail data (one entry per GI row)
- Each row is checked against all set filters (AND logic — all must match)
- Only matching rows contribute to the line's total

### Example Scenario

Your company has 3 branches: HQ, WAREHOUSE, RETAIL. The report-level header has no branch filter (fetches all data). You want CASH to show only for HQ:

```
Line Code:     CASH
Account From:  10100
Account To:    10199
Branch Filter: HQ          ← only HQ rows included
```

Lines without a Branch Filter will still include data from all 3 branches.

### Important Notes

- Filters are only applied to **Account Range** lines (not Subtotal, Calculated, or Heading)
- All filters use **exact match** (case-insensitive)
- Multiple filters are combined with AND logic
- If the report header already filters to a specific branch, setting a different branch in the line filter will return 0 (that branch's data was never fetched)
- The most useful scenario: leave report-level branch/org blank (fetch everything) and use per-line filters to scope individual lines

---

## 9. Rounding & Formatting

### Rounding Levels

| Level | Divisor | Raw Value | Result |
|---|---|---|---|
| **Units** | 1 | 1,808,344 | 1,808,344 |
| **Thousands** | 1,000 | 1,808,344 | 1,808 |
| **Millions** | 1,000,000 | 1,808,344 | 2 |

### Decimal Places

| Decimal Places | Thousands Example |
|---|---|
| 0 | 1,808 |
| 1 | 1,808.3 |
| 2 | 1,808.34 |

### Number Display Format

| Value | Display |
|---|---|
| Positive | `1,234,567` |
| Negative | `(1,234,567)` — accounting bracket notation |
| Zero | `-` — dash (standard financial statement practice) |

---

## 10. Creating a Word Template

The Word template is a standard `.docx` file with placeholders that get replaced with calculated values.

### Placeholder Format

Placeholders use double curly braces: `{{PLACEHOLDER_NAME}}`

### Definition-Mode Placeholders

When a Report Definition is linked, placeholders use Line Codes:

| Placeholder | Description |
|---|---|
| `{{CASH_CY}}` | Cash line, Current Year value |
| `{{CASH_PY}}` | Cash line, Prior Year value |
| `{{TOTAL_ASSETS_CY}}` | Total Assets, Current Year |
| `{{NET_INCOME_PY}}` | Net Income, Prior Year |
| `{{CY}}` | The current year number (e.g. `2024`) |
| `{{PY}}` | The prior year number (e.g. `2023`) |

### Legacy-Mode Placeholders

When no Report Definition is linked, placeholders use raw account codes:

| Placeholder | Description |
|---|---|
| `{{A10100_CY}}` | Account A10100, Current Year ending balance |
| `{{A10100_PY}}` | Account A10100, Prior Year ending balance |
| `{{A10100:A10199_e_CY}}` | Sum of accounts A10100 through A10199, ending balance, CY |

### Template Design Tips

1. Create the template in Microsoft Word with normal formatting
2. Type placeholders directly — do NOT copy/paste from other sources (formatting characters can break matching)
3. Use uppercase for Line Codes to match exactly
4. Test with a small template first before building the full report
5. Placeholders can appear in tables, headers, footers, and body text

### Example Template Structure

```
                    BALANCE SHEET
                As at December 31, {{CY}}
                                        {{CY}}          {{PY}}

ASSETS
Current Assets
  Cash and Cash Equivalents         {{CASH_CY}}     {{CASH_PY}}
  Accounts Receivable               {{AR_CY}}       {{AR_PY}}
  Inventory                         {{INV_CY}}      {{INV_PY}}
Total Current Assets                {{CURR_ASSETS_CY}} {{CURR_ASSETS_PY}}

Non-Current Assets
  Property, Plant & Equipment       {{PPE_CY}}      {{PPE_PY}}
Total Non-Current Assets            {{NONCURR_ASSETS_CY}} {{NONCURR_ASSETS_PY}}

TOTAL ASSETS                        {{TOTAL_ASSETS_CY}} {{TOTAL_ASSETS_PY}}
```

---

## 11. Generating a Report

### Step-by-Step

1. Navigate to **FR101000 - Financial Report**
2. Select or create a report record
3. Upload a Word template (attach `.docx` file to the record)
4. Fill in the header fields:

   | Field | Description |
   |---|---|
   | **Template Name** | Display name for this report |
   | **Current Year** | The reporting year (e.g. `2024`) |
   | **Financial Month** | The period month (e.g. `12` for December) |
   | **Branch** | Optional: filter data to a specific branch |
   | **Organization** | Optional: filter data to a specific organization |
   | **Ledger** | Optional: filter to a specific ledger (e.g. `ACTUAL`) |
   | **Report Definition** | Optional: link to a Report Definition for structured calculation |

5. Click **Generate**
6. Wait for the status to change to **Ready to Download**
7. Click **Download** to get the generated `.docx` file

### What Happens During Generation

1. Template file is extracted and placeholders are identified
2. Six parallel API calls fetch GL data (CY, PY, January beginning balances, cumulative ranges)
3. If a Report Definition is linked:
   - The ReportCalculationEngine processes all line items
   - Account Range lines sum GL data within their ranges
   - Subtotal and Calculated lines derive values from other lines
   - Sign normalization and rounding are applied
4. Legacy placeholders (raw account codes) are also resolved
5. All placeholders are replaced in the Word document
6. The generated file is saved to Acumatica

---

## 12. Legacy Mode vs Definition Mode

### Legacy Mode (no Report Definition linked)

- Placeholders use raw account codes: `{{A10100_CY}}`
- Returns the ending balance of that exact account
- No sign correction, no rounding, no subtotals
- Simple and direct — one placeholder per account
- Best for quick, simple reports

### Definition Mode (Report Definition linked)

- Placeholders use Line Codes: `{{CASH_CY}}`
- Full calculation engine with account ranges, subtotals, formulas
- Automatic sign normalization (credit-normal accounts flipped for presentation)
- Configurable rounding (Units/Thousands/Millions)
- Per-line dimension filters (Subaccount, Branch, Organization)
- Accounting bracket notation for negatives
- Best for formal financial statements

### Both Modes Together

When a Report Definition is linked, **both systems run**. Definition-mode placeholders take priority. Legacy placeholders fill in anything not covered by the definition. This allows you to use a definition for the main financial statement lines while still referencing individual accounts for notes or schedules.

---

## 13. Worked Examples

### Example 1: Simple Balance Sheet

**Report Definition:** `BS` (Balance Sheet)

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

**Verification check:** `TOTAL_ASSETS` should equal `LIAB_EQUITY`

### Example 2: Profit & Loss with Dimension Filters

A multi-branch company wants the P&L to show revenue broken down by branch:

| Sort | Line Code | Type | Account From | Account To | Branch Filter |
|---|---|---|---|---|---|
| 10 | REVENUE_HQ | Account Range | 40000 | 49999 | HQ |
| 20 | REVENUE_RETAIL | Account Range | 40000 | 49999 | RETAIL |
| 30 | REVENUE_WAREHOUSE | Account Range | 40000 | 49999 | WAREHOUSE |
| 40 | TOTAL_REVENUE | Calculated | | | |

**Line 40 Formula:** `REVENUE_HQ + REVENUE_RETAIL + REVENUE_WAREHOUSE`

> **Note:** The report header must have Branch and Organization left **blank** so all branch data is fetched.

### Example 3: Using Hidden Lines

Sometimes you need intermediate calculations that shouldn't appear in the report:

| Sort | Line Code | Type | Formula | Visible |
|---|---|---|---|---|
| 10 | REVENUE | Account Range | | Yes |
| 20 | COGS | Account Range | | Yes |
| 25 | _GROSS_AMT | Calculated | REVENUE - COGS | **No** |
| 30 | GROSS_MARGIN | Calculated | _GROSS_AMT / REVENUE * 100 | Yes |

Line `_GROSS_AMT` is calculated and available for formulas, but its placeholder resolves to empty in the Word template. Only `GROSS_MARGIN` (the percentage) appears.

---

## 14. Troubleshooting

### Common Issues

| Problem | Cause | Solution |
|---|---|---|
| Placeholder shows `{{CASH_CY}}` in output | Line Code doesn't match | Check Line Code matches exactly (case-insensitive) |
| Value shows `0` or `-` unexpectedly | Account range doesn't match any accounts | Verify AccountFrom/AccountTo match your chart of accounts |
| Subtotal shows `0` | Child lines missing ParentLineCode | Set the Parent Line Code on each child line |
| Formula shows `0` | Referenced Line Code not yet calculated | Ensure referenced lines have a LOWER Sort Order |
| Detect Columns fails | API credentials not configured | Set up Tenant Credentials first |
| All values are `0` | API returns no data | Check GI name, period, and that the GI has data in Acumatica |
| Negative where positive expected | Credit-normal account sign | The engine auto-flips L/I/Q accounts. Use Flip Sign for manual override. |
| Per-line branch filter returns `0` | Report header scoped to different branch | Leave report header branch blank when using per-line filters |
| Screen doesn't show new fields | Old ASPX cached | Recycle app pool or re-publish the customization |

### Sign Normalization

The engine automatically normalizes signs based on account type:

| Account Type | GL Natural Sign | Engine Treatment |
|---|---|---|
| Asset (A) | Debit (positive) | Kept as-is |
| Expense (E) | Debit (positive) | Kept as-is |
| Liability (L) | Credit (negative in GL) | Flipped to positive |
| Income (I) | Credit (negative in GL) | Flipped to positive |
| Equity (Q) | Credit (negative in GL) | Flipped to positive |

The **Sign Rule** on the line item is an additional override applied AFTER auto-normalization:
- **As-Is**: No additional change
- **Flip Sign**: Multiplies by -1 (use when you need to subtract in a subtotal context)

### Checking Trace Logs

The module writes detailed trace information during generation. Check the **Acumatica Trace** (System > Management > Trace) for:

- `ReportCalculationEngine: Processing X line items for DefinitionID Y`
- `[0010] CASH               CY=    50,000  PY=    45,000`
- `Account range 10100:10199 matched 5 accounts → 50,000`
- `Account range 10100:10199 [filtered] matched 2 detail rows → 25,000` (when filters are active)

---

## 15. Technical Reference

### Database Tables

| Table | Purpose |
|---|---|
| `FLRTFinancialReport` | Main report records (template, year, period, branch, etc.) |
| `FLRTReportDefinition` | Report definition header (GI name, column mapping, rounding) |
| `FLRTReportLineItem` | Line items within a definition (account ranges, formulas, etc.) |
| `FLRTTenantCredentials` | API credentials per tenant/company |

### Screen IDs

| Screen ID | Name | Purpose |
|---|---|---|
| FR101000 | Financial Report | Main generation screen |
| FR101002 | Report Definition | Definition & line item configuration |

### GI Column Name Derivation (OData)

When the module fetches data from a GI via OData, column names are derived as:
- If the GI column has a **Caption**: `Caption.Replace(" ", "")` (spaces stripped)
- If no Caption but has a **Field**: `ObjectName_FieldName`
- Formula columns without captions are skipped

### Calculation Engine Processing Order

1. Lines are loaded ordered by `SortOrder ASC`
2. Heading lines are skipped (no calculation)
3. Account Range lines are processed first (they read GL data)
4. Subtotal lines sum their children (children must be processed earlier)
5. Calculated lines evaluate their formula (referenced lines must be processed earlier)
6. Values are stored for both CY and PY
7. After all lines are processed, the placeholder map is built
8. Rounding is applied during placeholder formatting

### Formula Syntax

```
OPERAND: LineCode | NumericLiteral
OPERATOR: + | - | * | /
EXPRESSION: OPERAND (OPERATOR OPERAND)*
GROUPING: ( EXPRESSION )

Examples:
  REVENUE - EXPENSES
  (REVENUE - COGS) / REVENUE * 100
  TOTAL_LIAB + TOTAL_EQUITY
  GROSS_PROFIT - OPEX - FINANCE_COSTS
```

### API Data Flow

```
1. ReportGenerationService reads FLRTFinancialReport record
2. If DefinitionID is set, loads FLRTReportDefinition
3. Creates GIColumnMapping from definition (or uses defaults)
4. Creates FinancialDataService with the column mapping
5. Six parallel API calls to the GI OData endpoint:
   a. Current year data (selected period)
   b. Prior year data (same month, prior year)
   c. January beginning balance - prior year
   d. January beginning balance - current year
   e. Cumulative CY data (Jan to selected month)
   f. Cumulative PY data (Jan to Dec prior year)
6. ReportCalculationEngine processes all line items
7. Engine placeholders (LINECODE_CY/PY) take priority
8. Legacy placeholders (A10100_CY/PY) fill remaining gaps
9. WordTemplateService populates the template
10. Generated file saved to Acumatica
```

### Customization Package

The module is deployed as an Acumatica Customization Package. To deploy to a new instance:

1. Export the customization package from the source instance
2. Import into the target instance via the Customization Projects screen
3. Publish the customization
4. Run any required SQL scripts to create/alter tables
5. Configure API credentials for the new tenant
6. Create or import Report Definitions

---

*Generated for FinancialReport Module v2.0*
