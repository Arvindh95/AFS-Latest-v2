# Financial Report Module - User Manual v2

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
13. [Copy Definition](#13-copy-definition)
14. [Reset Status](#14-reset-status)
15. [Worked Examples](#15-worked-examples)
16. [Troubleshooting](#16-troubleshooting)
17. [Technical Reference](#17-technical-reference)

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
|  Generation               |   click Generate, Reset Status
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
| **FR101000** | Main report generation screen (select template, generate, download, reset status) |
| **FR101002** | Report Definition maintenance (define line items, GI mapping, rounding, copy definitions) |
| **ReportGenerationService** | Orchestrates the end-to-end report generation process |
| **FinancialDataService** | Fetches GL data from the GI via OData API |
| **ReportCalculationEngine** | Processes line items and produces placeholder values |
| **WordTemplateService** | Extracts placeholders from and populates Word templates |
| **GIColumnMapping** | Maps GI column names to expected data fields |
| **RoundingSettings** | Carries rounding configuration to the engine |
| **AuthService** | Handles OAuth2 authentication with the Acumatica API |
| **CredentialProvider** | Retrieves and caches encrypted tenant credentials |
| **TraceLogger** | Writes diagnostic information to the Acumatica Trace log |

---

## 3. Screens Reference

### FR101000 - Financial Report Generation

The main screen where you:
- Upload a Word template (`.docx`)
- Select the reporting year, month, branch, organization, and ledger
- Optionally link a Report Definition
- Click **Generate** to produce the report
- Click **Download** to get the generated file
- Click **Reset Status** to recover a stuck or failed report back to Pending

### FR101002 - Report Definition

The configuration screen where you define:
- The GI data source and column mapping
- Report line items (account ranges, subtotals, formulas)
- Rounding and formatting settings
- Use **Detect Columns** to auto-map GI columns
- Use **Copy Definition** to duplicate an existing definition with all its line items

---

## 4. Setting Up API Credentials

Before the module can fetch GL data, you need API credentials configured in the **Tenant Credentials** screen.

### Required Fields

| Field | Description | Example |
|---|---|---|
| **Company Number** | Unique integer linking reports to credentials | `1` |
| **Tenant Name** | The Acumatica tenant/company name (must be unique) | `MyCompany` |
| **Base URL** | The Acumatica instance URL (no trailing slash) | `https://mycompany.acumatica.com` |
| **Client ID** | OAuth2 client ID (RSA encrypted at rest) | `xxxxxxxx-xxxx-xxxx-xxxx` |
| **Client Secret** | OAuth2 client secret (RSA encrypted at rest) | `(your secret)` |
| **Username** | API user account (RSA encrypted at rest) | `admin@MyCompany` |
| **Password** | API user password (RSA encrypted at rest) | `(your password)` |

### Important Notes
- All sensitive fields (Username, Password, Client ID, Client Secret) are RSA-encrypted when saved
- The API user must have permissions to access the Generic Inquiry via OData
- The GI must be published and accessible via the OData endpoint
- Use a dedicated API user (not a regular user account) for reliability
- Company Number must be unique across all tenant records
- Tenant Name must be unique across all tenant records

---

## 5. Creating a Report Definition

Navigate to **FR101002 - Report Definition**.

### Step 1: Create the Definition Header

| Field | Description | Example |
|---|---|---|
| **Definition Code** | Unique identifier for this definition (immutable after save) | `BS_2024` |
| **Report Type** | Type of financial statement | Balance Sheet, Profit & Loss, Cash Flow, Changes in Equity, Custom |
| **Description** | Friendly description (up to 255 characters) | `Balance Sheet FY2024` |
| **Active** | Whether this definition can be used | Checked |

### Step 2: Configure the Data Source

The **Data Source** section tells the module which GI to query and which columns to read.

| Field | Default | Description |
|---|---|---|
| **Generic Inquiry Name** | `TrialBalance` | Name of the GI to query via OData (selectable from published GIs) |
| **Account Column** | `Account` | Column containing the GL account code |
| **Account Type Column** | `Type` | Column containing the account type (A/L/E/I/Q) |
| **Beginning Balance Column** | `BeginningBalance` | Column for beginning balance |
| **Ending Balance Column** | `EndingBalance` | Column for ending balance |
| **Debit Column** | `Debit` | Column for debit amounts |
| **Credit Column** | `Credit` | Column for credit amounts |

#### Using Detect Columns

1. Enter the **Generic Inquiry Name** (e.g. `TrialBalance`)
2. Click the **Detect Columns** button in the toolbar
3. The system connects to the API, fetches column metadata from the GI, and auto-maps column names using case-insensitive matching
4. Review and adjust the mapped columns if needed
5. Click **Save**

The auto-mapping logic searches for columns by name:
- Account: looks for "Account" (excluding "Sub" matches)
- Type: looks for "Type" or "AccountType"
- Beginning Balance: looks for "BeginningBalance", "Beginning", or "BegBal"
- Ending Balance: looks for "EndingBalance", "Ending", or "YtdBalance"
- Debit/Credit: looks for "Debit" and "Credit"

> **Note:** The Detect Columns button is only enabled when a GI Name is entered. Tenant Credentials must be configured first.

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
| **Line Code** | Unique identifier used in Word template placeholders (up to 100 characters). Use UPPER_CASE with underscores. |
| **Description** | Human-readable description (up to 255 characters, for your reference only) |
| **Line Type** | How this line's value is calculated (see Section 7) |
| **Account From** | Start of GL account range (inclusive, up to 50 characters) |
| **Account To** | End of GL account range (inclusive, up to 50 characters) |
| **Account Type Filter** | Restrict to specific account type: Asset (A), Liability (L), Expense (E), Income (I), Equity (Q), or All |
| **Balance Type** | Which balance to use: Ending, Beginning, Debit, Credit, Movement |
| **Sign Rule** | As-Is or Flip Sign (multiply by -1) |
| **Group / Parent Line** | Links this line to a Subtotal parent |
| **Formula** | Mathematical expression for Calculated lines (up to 500 characters) |
| **Visible in Report** | If unchecked, value is calculated but placeholder resolves to empty |
| **Subaccount Filter** | Optional exact-match filter on subaccount code |
| **Branch Filter** | Optional exact-match filter on branch (selectable from Acumatica branches) |
| **Organization Filter** | Optional exact-match filter on organization (selectable from Acumatica organizations) |
| **Ledger Filter** | Optional exact-match filter on ledger (selectable from Acumatica ledgers) |

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
| Dimension Filters | Yes | - | - | - |

When you change the Line Type, irrelevant fields are automatically cleared and disabled.

### Validation Rules

- **Line Code** is required and must be unique within the same definition
- **Account From** and **Account To** are required for Account Range lines
- **Formula** is required for Calculated lines
- The system validates these rules on save and shows field-level error messages

---

## 7. Line Types Explained

### Account Range

Sums GL account balances within the specified `AccountFrom` to `AccountTo` range.

**How it works:**
1. Iterates all accounts returned by the GI
2. Includes only accounts that fall within the `AccountFrom:AccountTo` range (smart alphanumeric comparison that handles segmented account codes like `1000-00`)
3. Optionally filters by Account Type (Asset, Liability, etc.)
4. Gets the specified balance type (Ending, Beginning, Debit, Credit, Movement)
5. Applies automatic sign normalization (credit-normal accounts like Liability/Income/Equity are flipped to positive)
6. Applies the Sign Rule if set to "Flip"
7. Sums all matching values

**Movement balance type:** When Balance Type is set to "Movement", the engine calculates `Debit - Credit` for each matching account, giving you the net activity for the period.

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
1. Parses the Formula expression using a recursive descent parser
2. Replaces each Line Code reference with its already-calculated value
3. Evaluates the expression respecting operator precedence (`*` and `/` bind tighter than `+` and `-`)
4. Supports parentheses for grouping
5. Supports numeric literals (e.g. `* 100` for percentages)
6. Supports unary minus (e.g. `-ADJUSTMENTS`)
7. Division by zero is safely handled (returns 0)

**Examples:**
```
Line Code: NET_INCOME
Formula:   TOTAL_REVENUE - TOTAL_EXPENSES

Line Code: WORKING_CAPITAL
Formula:   CURRENT_ASSETS - CURRENT_LIABILITIES

Line Code: GROSS_MARGIN_PCT
Formula:   GROSS_PROFIT / TOTAL_REVENUE * 100
```

> **Important:** All referenced Line Codes must have a lower Sort Order (be calculated first). Unknown Line Codes default to 0 with a trace warning.

### Heading

Display-only line used for section headers in the Word template. No value is calculated. The placeholder resolves to an empty string. The `Visible` flag is automatically set to false for Heading lines.

---

## 8. Dimension Filters (Per-Line)

Each Account Range line can optionally be restricted to specific dimensions. These filters appear in the line item fields.

### Available Filters

| Filter | Type | Description |
|---|---|---|
| **Subaccount Filter** | Free text (up to 30 chars) | Exact subaccount code (e.g. `000-000`) |
| **Branch Filter** | Selector (from Acumatica branches) | Select from Acumatica branches |
| **Organization Filter** | Selector (from Acumatica organizations) | Select from Acumatica organizations |
| **Ledger Filter** | Selector (from Acumatica ledgers) | Select from Acumatica ledgers |

### How Filters Work

**No filters set (default):**
- The engine uses the pre-aggregated data (all subaccounts, branches, and organizations summed per account)
- This is the most common scenario and is the fastest path

**Any filter set:**
- The engine switches to the per-row detail data (one entry per GI row)
- Each row is checked against all set filters (AND logic — all must match)
- Only matching rows contribute to the line's total
- When zero rows match, the trace log shows the filter values and sample data rows for debugging

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

Rounding uses `MidpointRounding.AwayFromZero` (standard banker's rounding away from zero).

### Decimal Places

| Decimal Places | Thousands Example |
|---|---|
| 0 | 1,808 |
| 1 | 1,808.3 |
| 2 | 1,808.34 |

### Number Display Format (Definition Mode)

| Value | Display |
|---|---|
| Positive | `1,234,567` |
| Negative | `(1,234,567)` — accounting bracket notation |
| Zero | `-` — dash (standard financial statement practice) |

### Number Display Format (Legacy Mode)

| Value | Display |
|---|---|
| All values | `#,##0` format (thousands separator, no decimals) |
| Zero | `0` |
| Negative | `-1,234,567` (minus sign) |

---

## 10. Creating a Word Template

The Word template is a standard `.docx` file with placeholders that get replaced with calculated values.

### File Naming Requirement

The template filename **must** contain the text `FRTemplate` (case-sensitive) for the system to recognize it. Example: `BalanceSheet_FRTemplate_2024.docx`

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
| `{{A1???:A2???_e_CY}}` | Wildcard range: sum all accounts from A1000-A2999, ending balance, CY |
| `{{Sum1_A_CY}}` | Sum all accounts starting with "A", CY |
| `{{DebitSum3_B53_CY}}` | Sum debits for accounts starting with "B53", CY |
| `{{CreditSum3_B53_CY}}` | Sum credits for accounts starting with "B53", CY |
| `{{BegSum3_A11_CY}}` | Sum beginning balances for accounts starting with "A11", CY |
| `{{A12345_sb000123_br001_CY}}` | Account with subaccount and branch dimensional filters |
| `{{A12345_btcredit_CY}}` | Account with specific balance type (credit) |
| `{{A12345_Jan1_CY}}` | January 1 beginning balance |
| `{{A12345_debit_CY}}` | Cumulative debit activity |
| `{{A12345_credit_CY}}` | Cumulative credit activity |

### Placeholder Limit

Templates are limited to a maximum of **1,000 placeholders** to prevent performance issues. If your template exceeds this limit, split it into multiple reports.

### Template Design Tips

1. Create the template in Microsoft Word with normal formatting
2. Type placeholders directly — do NOT copy/paste from other sources (formatting characters can break matching)
3. Use uppercase for Line Codes to match exactly
4. Test with a small template first before building the full report
5. Placeholders can appear in tables, headers, footers, and body text
6. Do not split a placeholder across multiple lines in Word
7. Avoid applying mixed formatting (bold/italic) within a single placeholder

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
3. Upload a Word template (attach `.docx` file with `FRTemplate` in the filename)
4. Fill in the header fields:

   | Field | Description |
   |---|---|
   | **Template Name** | Display name for this report (up to 225 characters) |
   | **Company Number** | Links to tenant credentials |
   | **Current Year** | The reporting year (selectable from Acumatica financial years) |
   | **Financial Month** | The period month, defaults to December |
   | **Branch** | Optional: filter data to a specific branch |
   | **Organization** | Optional: filter data to a specific organization |
   | **Ledger** | Optional: filter to a specific ledger (e.g. `ACTUAL`) |
   | **Report Definition** | Optional: link to a Report Definition for structured calculation |

5. Click **Save**
6. Check the **Select** checkbox for your report
7. Click **Generate Report**
8. Wait for the status to change to **Ready to Download**
9. Click **Download Report** to get the generated `.docx` file

### Timeout Protection

Report generation has a built-in **15-minute timeout**. If the process exceeds this limit, it is automatically cancelled and the status is set to Failed. This prevents indefinitely stuck reports.

### What Happens During Generation

1. Template file is extracted and placeholders are identified and categorized into three types:
   - **Wildcard range** placeholders (e.g. `A????:B????_e_CY`)
   - **Exact range** placeholders (e.g. `A74101:A75101_e_CY`)
   - **Regular** placeholders (e.g. `A74101_CY`)
2. Six parallel API calls fetch GL data:
   - Current year period data
   - Prior year period data
   - January beginning balance (current year)
   - January beginning balance (prior year)
   - Cumulative CY data (Jan to selected month)
   - Cumulative PY data (Jan to Dec prior year)
3. If a Report Definition is linked:
   - The ReportCalculationEngine processes all line items in SortOrder
   - Account Range lines sum GL data within their ranges with sign normalization
   - Subtotal and Calculated lines derive values from other lines
   - Rounding and formatting are applied
   - Engine placeholders take priority over legacy placeholders
4. Legacy placeholders (raw account codes) fill in anything not covered by the definition
5. Year constants (`{{CY}}` and `{{PY}}`) are added
6. All placeholders are replaced in the Word document (headers, footers, body)
7. The generated file is saved to Acumatica
8. Temporary files are cleaned up
9. Credential cache is cleared

---

## 12. Legacy Mode vs Definition Mode

### Legacy Mode (no Report Definition linked)

- Placeholders use raw account codes: `{{A10100_CY}}`
- Returns the ending balance of that exact account
- No sign correction, no rounding, no subtotals
- Simple and direct — one placeholder per account
- Supports Sum, DebitSum, CreditSum, BegSum prefix placeholders
- Supports exact and wildcard range placeholders
- Supports dimensional filtering via placeholder syntax
- Best for quick, simple reports

### Definition Mode (Report Definition linked)

- Placeholders use Line Codes: `{{CASH_CY}}`
- Full calculation engine with account ranges, subtotals, formulas
- Automatic sign normalization (credit-normal accounts flipped for presentation)
- Configurable rounding (Units/Thousands/Millions) with decimal places
- Per-line dimension filters (Subaccount, Branch, Organization, Ledger)
- Accounting bracket notation for negatives, dash for zeros
- Best for formal financial statements

### Both Modes Together

When a Report Definition is linked, **both systems run**. Definition-mode placeholders take priority. Legacy placeholders fill in anything not covered by the definition. This allows you to use a definition for the main financial statement lines while still referencing individual accounts for notes or schedules.

---

## 13. Copy Definition

The **Copy Definition** action on the FR101002 screen duplicates an existing Report Definition along with all its line items.

### How to Use

1. Open the definition you want to copy in FR101002
2. Click **Copy Definition** in the toolbar
3. Confirm the dialog prompt
4. A new definition is created with:
   - Definition Code = original code + `_COPY`
   - Description = original description + ` (Copy)`
   - All header settings (GI name, column mapping, rounding) are copied
   - All line items are duplicated with the same sort order, codes, formulas, and settings
5. Rename the Definition Code and Description as needed
6. Modify line items for your variant

This is useful for creating variations of an existing report (e.g. a Balance Sheet with notes, or a branch-specific P&L based on a consolidated template).

---

## 14. Reset Status

The **Reset Status** action on the FR101000 screen allows you to recover a report that is stuck in "In Progress" or "Failed" status.

### How to Use

1. Select the report record (check the Select checkbox)
2. Click **Reset Status** in the toolbar
3. Confirm the dialog prompt
4. The report status is reset to "Pending" (File not Generated)
5. The previously generated file reference is cleared
6. You can now edit the report and regenerate

### When to Use

- A report has been stuck in "In Progress" for an extended period (e.g. after a server restart)
- A report failed and you want to retry after fixing the underlying issue
- You want to clear a completed report and start fresh

> **Note:** While any report has status "In Progress", all Generate and Download buttons are disabled system-wide. Resetting the stuck report will re-enable these buttons for all reports.

---

## 15. Worked Examples

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

### Example 3: Using Hidden Lines and Percentages

Sometimes you need intermediate calculations that shouldn't appear in the report:

| Sort | Line Code | Type | Formula | Visible |
|---|---|---|---|---|
| 10 | REVENUE | Account Range | | Yes |
| 20 | COGS | Account Range | | Yes |
| 25 | _GROSS_AMT | Calculated | REVENUE - COGS | **No** |
| 30 | GROSS_MARGIN | Calculated | _GROSS_AMT / REVENUE * 100 | Yes |

Line `_GROSS_AMT` is calculated and available for formulas, but its placeholder resolves to empty in the Word template. Only `GROSS_MARGIN` (the percentage) appears.

### Example 4: Using Copy Definition for Variants

1. Create a consolidated Balance Sheet definition `BS_CONSOL`
2. Use **Copy Definition** to create `BS_CONSOL_COPY`
3. Rename to `BS_HQ` and add Branch Filters to each Account Range line for the HQ branch
4. Now you have both a consolidated and branch-specific version sharing the same structure

---

## 16. Troubleshooting

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
| Generate button disabled | Another report is In Progress | Wait for it to complete, or use Reset Status on the stuck report |
| Report stuck In Progress | Server restart or timeout | Use **Reset Status** to recover the report to Pending |
| "Template contains X placeholders" error | Too many placeholders | Maximum is 1,000. Simplify or split into multiple reports. |
| "Report generation timed out" | Very large dataset or slow API | Check template complexity, try during off-hours |
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
- Filter values and sample data rows when zero rows match (helps debug dimension filter issues)
- Placeholder counts by type (regular, exact range, wildcard range)
- Total generation time in milliseconds
- Authentication and API call status

---

## 17. Technical Reference

### Database Tables

| Table | Purpose |
|---|---|
| `FLRTFinancialReport` | Main report records (template, year, period, branch, definition link, status, file IDs) |
| `FLRTReportDefinition` | Report definition header (GI name, column mapping, rounding, report type) |
| `FLRTReportLineItem` | Line items within a definition (account ranges, formulas, dimension filters, visibility) |
| `FLRTTenantCredentials` | API credentials per tenant/company (RSA-encrypted sensitive fields) |

### Screen IDs

| Screen ID | Name | Purpose |
|---|---|---|
| FR101000 | Financial Report | Main generation screen (Generate, Download, Reset Status) |
| FR101002 | Report Definition | Definition & line item configuration (Detect Columns, Copy Definition) |

### Report Status Lifecycle

| Status Constant | Display Name | Description |
|---|---|---|
| `File not Generated` | Pending | Initial state, report can be edited and generated |
| `File Generation In Progress` | In Progress | Background generation running (all buttons disabled system-wide) |
| `Ready to Download` | Ready to Download | Generation succeeded, file available for download |
| `Failed to Generate File` | Failed | Error occurred during generation |

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
6. Values are stored for both CY and PY in separate dictionaries
7. After all lines are processed, the placeholder map is built
8. Non-visible lines and Heading lines get empty string placeholders
9. Rounding is applied during placeholder formatting

### Account Code Comparison

The engine uses a smart alphanumeric comparison for account ranges:
- Segmented codes (containing `-`) are compared segment by segment
- Numeric segments are compared numerically (so `2` < `10`)
- Non-segmented codes use character-by-character comparison with numeric grouping
- Comparison is case-insensitive

### Formula Syntax

```
OPERAND: LineCode | NumericLiteral
OPERATOR: + | - | * | /
EXPRESSION: OPERAND (OPERATOR OPERAND)*
GROUPING: ( EXPRESSION )
UNARY: -OPERAND

Examples:
  REVENUE - EXPENSES
  (REVENUE - COGS) / REVENUE * 100
  TOTAL_LIAB + TOTAL_EQUITY
  GROSS_PROFIT - OPEX - FINANCE_COSTS
  -ADJUSTMENTS + NET_INCOME
```

### API Data Flow

```
1. ReportGenerationService reads FLRTFinancialReport record
2. If DefinitionID is set, loads FLRTReportDefinition
3. Creates GIColumnMapping from definition (or uses defaults)
4. Creates RoundingSettings from definition (or uses defaults: Units, 0 decimals)
5. Creates FinancialDataService with the column mapping
6. Six parallel API calls to the GI OData endpoint:
   a. Current year data (selected period)
   b. Prior year data (same month, prior year)
   c. January beginning balance - prior year
   d. January beginning balance - current year
   e. Cumulative CY data (Jan to selected month)
   f. Cumulative PY data (Jan to Dec prior year)
7. Placeholders are categorized: wildcard range, exact range, regular
8. If DefinitionID is set:
   - ReportCalculationEngine processes all line items
   - Engine placeholders (LINECODE_CY/PY) are added first (highest priority)
9. Legacy placeholders fill remaining gaps:
   - Regular placeholders processed
   - Exact range placeholders processed
   - Wildcard range placeholders processed
10. Year constants (CY, PY) are added
11. WordTemplateService populates the template
12. Generated file saved to Acumatica
13. Temporary files cleaned up, credential cache cleared
```

### Error Messages Reference

| Message | Cause |
|---|---|
| `Please select a template to generate the report.` | No record selected via checkbox |
| `The selected template does not have any attached files.` | No file attached or filename missing `FRTemplate` |
| `A report generation process is already running for this template.` | Another report is In Progress |
| `Failed to authenticate. Please check credentials.` | Invalid API credentials |
| `Current Year is not specified for the selected report.` | Missing Current Year field |
| `No generated file is available for download.` | Report not yet generated or generation failed |
| `No API credentials found for company` | No tenant credentials for the Company Number |
| `Tenant mapping not found.` | Company Number doesn't match any tenant |
| `Template contains X placeholders. Maximum allowed is Y.` | Too many placeholders (limit: 1,000) |
| `Report generation timed out after X minutes.` | Generation exceeded 15-minute timeout |
| `Definition Code must be unique.` | Duplicate Definition Code on save |
| `Line Code must be unique within the same definition.` | Duplicate Line Code in same definition |
| `Account From is required for Account Range line types.` | Missing Account From on Account Range line |
| `Formula is required for Calculated line types.` | Missing Formula on Calculated line |
| `Generic Inquiry Name is required to detect columns.` | Detect Columns clicked without GI Name |
| `No columns were detected from the specified Generic Inquiry.` | GI returned no data or doesn't exist |

### Customization Package

The module is deployed as an Acumatica Customization Package. To deploy to a new instance:

1. Export the customization package from the source instance
2. Import into the target instance via the Customization Projects screen
3. Publish the customization
4. Run any required SQL scripts to create/alter tables
5. Configure API credentials for the new tenant
6. Create or import Report Definitions

---

*Generated for FinancialReport Module v2.0 — March 2026*
