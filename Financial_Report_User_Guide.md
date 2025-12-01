# FINANCIAL REPORT APPLICATION
## Comprehensive User Guide

---

**Version:** 1.0
**Last Updated:** November 2025
**For:** Acumatica ERP Users

---

## Table of Contents

1. [Introduction & Overview](#1-introduction--overview)
2. [Getting Started](#2-getting-started)
3. [Setting Up Tenant Credentials](#3-setting-up-tenant-credentials)
4. [Creating Your First Report](#4-creating-your-first-report)
5. [Understanding Placeholder Syntax](#5-understanding-placeholder-syntax)
6. [Report Status & Workflow](#6-report-status--workflow)
7. [Troubleshooting Guide](#7-troubleshooting-guide)
8. [Best Practices & Tips](#8-best-practices--tips)
9. [Appendices](#9-appendices)

---

## 1. Introduction & Overview

### 1.1 What is the Financial Report Application?

The Financial Report Application is a powerful Acumatica ERP customization that automates the generation of financial reports by:

- **Extracting financial data** from your Acumatica General Ledger via OData APIs
- **Populating Microsoft Word templates** with actual account balances and financial data
- **Generating professional reports** in minutes instead of hours
- **Supporting complex calculations** including account ranges, summations, and year-over-year comparisons

This application eliminates manual data entry, reduces errors, and saves significant time in financial reporting processes.

### 1.2 Key Features & Benefits

#### Key Features

✓ **Template-Based Reporting**
- Use your own Microsoft Word templates
- Customize layout, formatting, and branding
- Support for headers, footers, tables, and charts

✓ **Flexible Placeholder System**
- 10+ placeholder types for different data needs
- Simple account balances
- Aggregate sums and calculations
- Account ranges with wildcards
- Dimensional filtering (Branch, Subaccount, Organization, Ledger)

✓ **Year-over-Year Comparisons**
- Compare current year vs. previous year data
- Built-in support for comparative analysis
- Automatic period matching

✓ **Multi-Tenant Support**
- Configure credentials for multiple companies
- Secure credential storage with RSA encryption
- Centralized credential management

✓ **Background Processing**
- Long-running reports don't block your work
- Status tracking throughout generation
- Automatic error handling and recovery

✓ **High Performance**
- Parallel API data fetching (up to 6 concurrent calls)
- Optimized placeholder processing
- Efficient data aggregation

#### Benefits

📊 **Save Time**: Generate reports in minutes that previously took hours
🎯 **Improve Accuracy**: Eliminate manual data entry errors
🔄 **Standardize Reports**: Consistent formatting across all reports
🔒 **Maintain Security**: Encrypted credentials and secure API access
📈 **Scale Easily**: Generate multiple reports with the same template
🎨 **Customize Freely**: Full control over report appearance

### 1.3 Who Should Use This Guide?

This user guide is designed for:

- **Finance Team Members** who generate regular financial reports
- **Accountants** preparing month-end and year-end reports
- **Controllers** needing consolidated financial statements
- **Report Designers** creating and maintaining Word templates
- **Administrators** setting up and configuring the system

### 1.4 Prerequisites

Before using the Financial Report Application, ensure you have:

- ✅ **Acumatica ERP** installed and configured
- ✅ **User access** to the Financial Report screens
- ✅ **API credentials** for your Acumatica instance (provided by IT/Admin)
- ✅ **Microsoft Word** installed (for template creation and viewing reports)
- ✅ **Basic understanding** of your Chart of Accounts
- ✅ **Financial period data** available in General Ledger

### 1.5 Document Conventions

Throughout this guide, we use the following conventions:

> **📘 NOTE:** Additional information or helpful tips

> **⚠️ WARNING:** Important information to prevent errors

> **✅ TIP:** Best practices and recommendations

> **❌ COMMON MISTAKE:** Frequent errors to avoid

**Bold text** indicates field names, button names, or important terms
`Code formatting` indicates placeholder syntax or technical values

---

## 2. Getting Started

### 2.1 Accessing the Financial Report Application

The Financial Report Application consists of two main screens within Acumatica ERP:

#### Screen 1: Financial Report Maintenance
**Purpose:** Create, configure, and generate financial reports

**How to Access:**
1. Log into Acumatica ERP
2. Navigate to the main menu
3. Look for the Financial Report or Custom Reports section
4. Select **Financial Report Maintenance**

#### Screen 2: Tenant Credentials Maintenance
**Purpose:** Configure API credentials for external data sources (Administrator only)

**How to Access:**
1. Log into Acumatica ERP with administrator privileges
2. Navigate to Configuration or Settings
3. Select **Tenant Credentials Maintenance**

> **📘 NOTE:** Your administrator should configure Tenant Credentials before you create your first report.

### 2.2 Understanding the Interface

#### Financial Report Maintenance Screen

The Financial Report Maintenance screen contains the following sections:

**Main Grid/List View:**
- Displays all existing financial report templates
- Shows key information: Template Name, Description, Status
- Allows selection of records for generation or download

**Detail Panel:**
When you select a record, the detail panel shows:

| Section | Fields |
|---------|--------|
| **Template Information** | Template Name, Description |
| **Company Settings** | Company Number |
| **Report Period** | Current Year, Financial Month |
| **Filters** | Branch, Organization, Ledger |
| **Status** | Current status (Read-only) |
| **Selection** | Select checkbox |

**Action Buttons:**
- **Save**: Saves changes to the current record
- **Cancel**: Discards unsaved changes
- **Generate Report**: Starts report generation process
- **Download Report**: Downloads completed report

**File Attachments:**
- Paper clip icon or **Files** tab
- Upload Word templates
- View attached files

### 2.3 User Roles & Permissions

Different users may have different levels of access:

| Role | Permissions |
|------|-------------|
| **Administrator** | Full access to all screens, can configure tenant credentials |
| **Report Generator** | Can create reports, generate, and download |
| **Report Viewer** | Can view and download existing reports only |
| **Template Designer** | Can create/modify templates and test generation |

> **⚠️ WARNING:** Only administrators should have access to Tenant Credentials Maintenance. This screen contains sensitive authentication information.

### 2.4 Typical Workflow Overview

Here's a high-level overview of the report generation process:

```
┌─────────────────────────────────────┐
│  1. Administrator Setup             │
│  Configure Tenant Credentials       │
│  (One-time)                          │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│  2. Create Report Record            │
│  Enter template name, parameters    │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│  3. Upload Word Template            │
│  Attach .docx file with placeholders│
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│  4. Generate Report                 │
│  Click Generate Report button       │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│  5. Wait for Completion             │
│  Monitor status field               │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│  6. Download Report                 │
│  Click Download Report button       │
└────────────┬────────────────────────┘
             │
             ▼
┌─────────────────────────────────────┐
│  7. Review Report                   │
│  Open in Microsoft Word             │
└─────────────────────────────────────┘
```

### 2.5 System Requirements

**Client Requirements:**
- Modern web browser (Chrome, Firefox, Edge, Safari)
- Microsoft Word 2016 or later (for viewing reports)
- Stable internet connection

**Server Requirements:**
- Acumatica ERP instance with API access enabled
- Financial Report customization package installed
- Database with tenant credentials configured

**Data Requirements:**
- General Ledger data posted for the reporting period
- Chart of Accounts configured
- Financial periods defined

---

## 3. Setting Up Tenant Credentials

> **📘 NOTE:** This section is primarily for **Administrators**. If you're a regular user, your administrator should complete this setup before you create reports.

### 3.1 What are Tenant Credentials?

Tenant Credentials are the authentication details required to connect to the Acumatica API and retrieve financial data. Each company or tenant requires its own set of credentials.

**Why are they needed?**
- The application fetches data via Acumatica's OData API
- API access requires authentication (username, password, OAuth credentials)
- Credentials are stored securely and reused for each report generation

### 3.2 Required Information

Before configuring credentials, gather the following information:

| Field | Description | Example |
|-------|-------------|---------|
| **Company Number** | Unique identifier for the company | 1, 2, 3 |
| **Tenant Name** | Unique name for the tenant | "MainCompany", "Subsidiary1" |
| **Base URL** | Acumatica instance URL | http://server/AcumaticaERP |
| **Username** | API user account | apiuser@company.com |
| **Password** | API user password | ••••••••••• |
| **Client ID** | OAuth Client ID | abc123xyz789 |
| **Client Secret** | OAuth Client Secret | ••••••••••• |

> **⚠️ WARNING:** The API user must have appropriate permissions to access General Ledger data via OData. Consult your Acumatica administrator to create a dedicated API user.

### 3.3 Step-by-Step Configuration

#### Step 1: Access Tenant Credentials Screen

1. Log into Acumatica with **administrator** privileges
2. Navigate to **Tenant Credentials Maintenance**
3. Click **Add New** or the **+** button to create a new record

#### Step 2: Enter Company Information

1. **Company Number:**
   - Enter a unique integer (e.g., 1, 2, 3)
   - This number will be used to link reports to credentials
   - Must be unique across all tenant records

2. **Tenant Name:**
   - Enter a descriptive, unique name (e.g., "MainCompany", "Branch_NY")
   - Maximum 50 characters
   - Must be unique (system validates on save)
   - Use meaningful names for easy identification

> **✅ TIP:** Use a naming convention like "CompanyName_Environment" (e.g., "ACME_Production", "ACME_Test")

#### Step 3: Enter API Connection Details

1. **Base URL:**
   - Enter the full URL to your Acumatica instance
   - Format: `http://servername/InstanceName` or `https://company.acumatica.com`
   - Example: `http://localhost/AcumaticaERP`
   - Do not include trailing slash

2. **Username:**
   - Enter the API user's login username
   - Typically an email format: `user@company.com`
   - This field is **encrypted** when saved

3. **Password:**
   - Enter the API user's password
   - This field is **encrypted** when saved
   - Use a strong password

4. **Client ID:**
   - Enter the OAuth 2.0 Client ID
   - Obtained from Acumatica Application Settings
   - This field is **encrypted** when saved

5. **Client Secret:**
   - Enter the OAuth 2.0 Client Secret
   - Obtained from Acumatica Application Settings
   - This field is **encrypted** when saved

> **📘 NOTE:** All sensitive fields (Username, Password, Client ID, Client Secret) use RSA encryption and are stored securely in the database.

#### Step 4: Save and Validate

1. Click **Save** button
2. System validates:
   - All required fields are filled
   - Company Number is unique
   - Tenant Name is unique
3. If validation passes, record is saved
4. If validation fails, error message displays

**Common Validation Errors:**

| Error Message | Solution |
|---------------|----------|
| "Company Number is required." | Enter a Company Number |
| "Tenant Name is required." | Enter a Tenant Name |
| "Tenant Name must be unique." | Choose a different Tenant Name |

#### Step 5: Test Credentials (Recommended)

After saving, it's recommended to test the credentials:

1. Create a test report using this Company Number
2. Upload a simple template
3. Generate the report
4. If successful, credentials are correctly configured
5. If it fails with authentication error, review credentials

### 3.4 Managing Existing Credentials

#### Viewing Credentials

1. Navigate to **Tenant Credentials Maintenance**
2. Select a record from the list
3. Encrypted fields show as dots (••••••) for security
4. You can edit any field except Company Number (key field)

#### Editing Credentials

1. Select the credential record to edit
2. Modify the necessary fields
3. Re-enter encrypted fields even if not changed (security requirement)
4. Click **Save**

#### Deleting Credentials

> **⚠️ WARNING:** Deleting credentials will prevent all reports using that Company Number from generating. Ensure no active reports reference the credentials before deleting.

1. Select the credential record to delete
2. Click **Delete** button
3. Confirm deletion
4. Record is removed

### 3.5 Security Best Practices

✅ **DO:**
- Use dedicated API user accounts (don't use personal accounts)
- Grant minimum necessary permissions to API users
- Use strong, unique passwords
- Regularly rotate Client ID and Client Secret
- Limit access to Tenant Credentials screen to administrators only
- Document which credentials belong to which environment

❌ **DON'T:**
- Share credentials between production and test environments
- Use personal user accounts for API access
- Leave default or weak passwords
- Grant excessive permissions to API users
- Allow non-administrators to view/edit credentials

### 3.6 Troubleshooting Credential Issues

**Problem: "Failed to authenticate. Please check credentials."**

Possible causes and solutions:

1. **Incorrect Username/Password**
   - Verify credentials in Acumatica
   - Test login manually through Acumatica web interface
   - Check for expired passwords

2. **Incorrect Client ID/Client Secret**
   - Verify OAuth application settings in Acumatica
   - Regenerate Client ID/Secret if needed
   - Ensure OAuth application is active

3. **Incorrect Base URL**
   - Verify URL format (no trailing slash)
   - Check server name and instance name
   - Test URL in browser

4. **API User Permissions**
   - Ensure user has API access enabled
   - Grant access to General Ledger entities
   - Check OData permissions

5. **Network Issues**
   - Verify server is accessible from application server
   - Check firewall rules
   - Test connectivity

---

## 4. Creating Your First Report

This section provides a complete walkthrough of creating and generating your first financial report.

### 4.1 Before You Begin

Ensure you have:
- ✅ Tenant Credentials configured (see Section 3)
- ✅ A Word template prepared with placeholders (or use the example below)
- ✅ Financial data posted for the period you want to report on
- ✅ Access to Financial Report Maintenance screen

### 4.2 Step 1: Create a New Report Record

1. Navigate to **Financial Report Maintenance** screen
2. Click **Add New** or the **+** button
3. A new blank record appears

### 4.3 Step 2: Enter Report Details

Fill in the following fields:

#### Template Name (Required)
- **Field:** Template Name
- **What to enter:** A descriptive name for your report template
- **Examples:**
  - "Balance Sheet Q4 2024"
  - "Income Statement Monthly"
  - "Trial Balance December"
- **Tips:** Use clear, descriptive names that indicate the report type and period

#### Description (Optional)
- **Field:** Description
- **What to enter:** Additional details about the report
- **Examples:**
  - "Quarterly balance sheet for board presentation"
  - "Monthly P&L for management review"
- **Tips:** Include purpose, audience, or special notes

#### Company Number (Required)
- **Field:** Company Number
- **What to enter:** The Company Number from Tenant Credentials setup
- **How to find:** This should match the Company Number you (or your administrator) configured in Tenant Credentials Maintenance
- **Example:** 1, 2, 3

> **📘 NOTE:** The Company Number links this report to the appropriate API credentials. Ensure you use the correct number for your intended data source.

#### Current Year (Required)
- **Field:** Current Year
- **What to enter:** The year for which you want to generate the report
- **Format:** 4-digit year (e.g., 2024)
- **Default:** Current year is pre-filled
- **Tips:**
  - For year-over-year reports, this is the "current" year
  - The system automatically calculates "previous year" as Current Year - 1

#### Financial Month (Required)
- **Field:** Financial Month
- **What to enter:** The month-end for your report period
- **Format:** Dropdown selector
- **Options:** 01 (January) through 12 (December)
- **Default:** 12 (December)
- **Examples:**
  - Select "03" for Q1 reports (Jan-Mar data)
  - Select "06" for Q2 reports (Jan-Jun data)
  - Select "12" for year-end reports (Jan-Dec data)

> **📘 NOTE:** The selected month represents the **ending period**. If you select "06" (June), the report includes data from January through June for cumulative calculations.

#### Branch (Optional)
- **Field:** Branch
- **What to enter:** Select a specific branch to filter data
- **When to use:** When generating branch-specific reports
- **When to skip:** When generating company-wide consolidated reports
- **Example:** "MAIN", "BRANCH001", "NYC"

#### Organization (Optional)
- **Field:** Organization
- **What to enter:** Select a specific organization to filter data
- **When to use:** When generating organization-specific reports
- **When to skip:** When generating consolidated reports across all organizations
- **Example:** "CORPORATE", "SUBSIDIARY1"

#### Ledger (Optional but Recommended)
- **Field:** Ledger
- **What to enter:** Select the ledger type
- **Common values:** "ACTUAL", "BUDGET", "FORECAST"
- **Example:** "ACTUAL"
- **Tips:** Most financial reports use "ACTUAL" ledger

> **✅ TIP:** The Branch, Organization, and Ledger fields act as **global filters** for all data in your report. Any account balances retrieved will be filtered by these dimensions if specified.

### 4.4 Step 3: Save the Record

1. After entering all required fields, click **Save** button
2. System validates your entries
3. If successful, record is saved with status "File not Generated"
4. If validation fails, correct the errors and save again

**Common Validation Errors:**

| Error Message | Solution |
|---------------|----------|
| "Template Name is required" | Enter a Template Name |
| "Current Year is required" | Enter or select a Current Year |
| "Financial Month is required" | Select a Financial Month from dropdown |
| "Company Number is required" | Enter the Company Number |

### 4.5 Step 4: Upload Your Word Template

#### Creating a Simple Test Template

If this is your first report, create a simple test template:

1. Open Microsoft Word
2. Create a new document
3. Add the following content:

```
SAMPLE FINANCIAL REPORT
Year: {{CY}}

Account A11101 Balance: {{A11101_CY}}
Account A11101 Previous Year: {{A11101_PY}}

Total Assets: {{Sum1_A_CY}}
```

4. Save the file as: **TestReport_FRTemplate_2024.docx**

> **⚠️ WARNING:** The filename MUST contain the text **"FRTemplate"** (case-sensitive) for the system to recognize it as a template file.

#### Uploading the Template

1. In the Financial Report Maintenance screen, with your record selected:
2. Click the **paper clip icon** or navigate to the **Files** tab
3. Click **Upload File** or **Attach File**
4. Browse to your template file
5. Select **TestReport_FRTemplate_2024.docx**
6. Click **Upload** or **OK**
7. File appears in the attachments list

> **✅ TIP:** You can attach multiple files, but the system will use only the most recently uploaded file with "FRTemplate" in the filename.

#### Verifying the Upload

1. Check the Files tab or attachment area
2. Confirm your template file is listed
3. File should show filename containing "FRTemplate"
4. Click **Save** to ensure the attachment is linked to the record

### 4.6 Step 5: Generate the Report

Now you're ready to generate your first report!

#### Pre-Generation Checklist

Before clicking Generate Report, verify:
- ✅ Template Name is filled
- ✅ Company Number is correct
- ✅ Current Year is set
- ✅ Financial Month is selected
- ✅ Template file is uploaded (contains "FRTemplate" in filename)
- ✅ Record is saved
- ✅ No other report shows status "File Generation In Progress"

#### Generating the Report

1. **Select the record:** Check the **Select** checkbox for your report record
2. **Click Generate Report button:** The button should be enabled
3. **System begins generation:**
   - Status immediately changes to "File Generation In Progress"
   - All fields become read-only
   - All buttons become disabled (for all records, not just this one)
4. **Background processing:** The system runs the generation in the background

> **📘 NOTE:** Generation typically takes 1-5 minutes depending on data volume. You can navigate away from the screen; the process continues in the background.

#### What Happens During Generation

While the report generates, the system:

1. **Validates inputs** (credentials exist, template file exists, etc.)
2. **Authenticates** with the Acumatica API using stored credentials
3. **Extracts placeholders** from your Word template
4. **Fetches financial data** via 6 parallel API calls:
   - Current year period data
   - Previous year period data
   - Current year beginning balance (January 1)
   - Previous year beginning balance (January 1)
   - Current year cumulative data (Jan to selected month)
   - Previous year cumulative data (full year)
5. **Processes placeholders** and calculates values
6. **Populates template** with actual data
7. **Saves generated file** back to Acumatica
8. **Updates status** to "Ready to Download" or "Failed to Generate File"

### 4.7 Step 6: Monitor Status

#### Understanding Status Values

The **Status** field shows the current state of your report:

| Status Display | Meaning | What to Do |
|----------------|---------|------------|
| **File not Generated** | Initial state, report not yet generated | Generate the report |
| **File Generation In Progress** | Report is currently being generated | Wait for completion |
| **Ready to Download** | Report successfully generated | Download the report |
| **Failed to Generate File** | An error occurred during generation | Check error logs, troubleshoot |

#### Checking Status

1. **Refresh the screen** to see latest status
2. **Wait for status change** from "In Progress" to "Ready to Download"
3. **Typical wait time:** 1-5 minutes for most reports

> **📘 NOTE:** While any report has status "File Generation In Progress", you cannot generate other reports. This is a system-wide limitation to prevent resource conflicts.

### 4.8 Step 7: Download Your Report

Once status shows **"Ready to Download"**:

1. **Select the record:** Check the **Select** checkbox
2. **Click Download Report button:** Button should now be enabled
3. **File downloads** automatically through your browser
4. **Save the file** to your desired location

#### Generated File Details

- **File format:** Microsoft Word (.docx)
- **File naming:** Original template name with timestamp
- **Example:** `TestReport_Generated_20241107_143025.docx`
- **Content:** All placeholders replaced with actual financial data

### 4.9 Step 8: Review Your Report

1. **Open the downloaded file** in Microsoft Word
2. **Review the data:**
   - Placeholders should be replaced with numbers
   - Format should match your template
   - Check for any remaining placeholders (indicates data not found)

**Example output from our test template:**

```
SAMPLE FINANCIAL REPORT
Year: 2024

Account A11101 Balance: 125,450
Account A11101 Previous Year: 118,230

Total Assets: 2,456,789
```

> **✅ TIP:** If placeholders show "0", it may indicate the account has no balance or doesn't exist. See Troubleshooting section for details.

### 4.10 Complete Example Walkthrough

Let's create a complete Balance Sheet report:

**Scenario:** You need a Balance Sheet for December 2024 for the Main branch.

**Step-by-Step:**

1. **Create record:**
   - Template Name: "Balance Sheet December 2024"
   - Description: "Year-end balance sheet"
   - Company Number: 1
   - Current Year: 2024
   - Financial Month: 12 (December)
   - Branch: MAIN
   - Ledger: ACTUAL

2. **Save record**

3. **Create Word template** (BalanceSheet_FRTemplate.docx):

```
ABC COMPANY
BALANCE SHEET
As of December 31, {{CY}}

ASSETS
Current Assets
  Cash                          {{A11101_CY}}
  Accounts Receivable          {{A12101_CY}}
  Inventory                     {{A14101_CY}}
    Total Current Assets       {{A11101:A14999_e_CY}}

Fixed Assets
  Property & Equipment          {{A15101_CY}}
  Less: Accumulated Depreciation {{A15201_CY}}
    Net Fixed Assets           {{Sum2_A15_CY}}

TOTAL ASSETS                   {{Sum1_A_CY}}

LIABILITIES & EQUITY
Current Liabilities
  Accounts Payable             {{L21101_CY}}
  Accrued Expenses             {{L22101_CY}}
    Total Current Liabilities  {{Sum1_L_CY}}

Equity
  Retained Earnings            {{E31101_CY}}
  Current Year Net Income      {{E32101_CY}}
    Total Equity               {{Sum1_E_CY}}

TOTAL LIABILITIES & EQUITY     {{Sum1_L_CY}} + {{Sum1_E_CY}}

Comparative - Prior Year       {{Sum1_A_PY}}
```

4. **Upload template** to the Files tab

5. **Save record** again to ensure template is linked

6. **Select record** (checkbox)

7. **Click Generate Report**

8. **Wait 2-3 minutes** for status to change to "Ready to Download"

9. **Click Download Report**

10. **Open in Word** and review

**Expected Result:** A fully populated Balance Sheet with all account balances filled in from your General Ledger data for December 2024.

---

## 5. Understanding Placeholder Syntax

Placeholders are special tags in your Word template that the system replaces with actual financial data. This section explains all supported placeholder types.

### 5.1 Placeholder Basics

#### What is a Placeholder?

A placeholder is a text pattern enclosed in double curly braces:

```
{{PLACEHOLDER_NAME}}
```

**Example:**
```
Account Balance: {{A11101_CY}}
```

When the report generates, this becomes:
```
Account Balance: 125,450
```

#### Placeholder Rules

✅ **DO:**
- Use double curly braces: `{{ }}`
- Use exact account numbers from your Chart of Accounts
- Include year suffix: `_CY` or `_PY`
- Use uppercase for consistency
- Test placeholders before using in production

❌ **DON'T:**
- Use single braces: `{PLACEHOLDER}`
- Misspell account numbers
- Forget the year suffix
- Use spaces inside braces: `{{ A11101_CY }}`
- Split placeholders across multiple lines in Word

> **⚠️ WARNING:** Placeholders are **case-sensitive**. `{{A11101_CY}}` is different from `{{a11101_cy}}`. Use consistent casing.

### 5.2 Year Suffixes

All placeholders require a year suffix:

| Suffix | Meaning | Data Retrieved |
|--------|---------|----------------|
| **_CY** | Current Year | Data from the year specified in "Current Year" field |
| **_PY** | Previous Year | Data from Current Year - 1 |

**Examples:**

If Current Year is set to 2024:
- `{{A11101_CY}}` → 2024 data
- `{{A11101_PY}}` → 2023 data

### 5.3 Placeholder Type 1: Simple Account Balance

**Purpose:** Retrieve the ending balance for a single account

**Syntax:**
```
{{AccountNumber_CY}}
{{AccountNumber_PY}}
```

**Examples:**

| Placeholder | Returns |
|-------------|---------|
| `{{A11101_CY}}` | Current year ending balance for account A11101 |
| `{{A34101_PY}}` | Previous year ending balance for account A34101 |
| `{{L21101_CY}}` | Current year ending balance for account L21101 |

**Use Cases:**
- Displaying specific account balances
- Line items in financial statements
- Individual account analysis

**Real-World Example:**

```
Cash in Bank (Account A11101): ${{A11101_CY}}
Prior Year Cash Balance: ${{A11101_PY}}
Change: ${{A11101_CY}} - {{A11101_PY}}
```

Output:
```
Cash in Bank (Account A11101): $125,450
Prior Year Cash Balance: $118,230
Change: $125,450 - $118,230
```

> **📘 NOTE:** By default, this returns the **ending balance** for the selected financial period. For other balance types, see Section 5.7.

### 5.4 Placeholder Type 2: Sum Prefix

**Purpose:** Sum all accounts starting with a specific prefix

**Syntax:**
```
{{Sum[Level]_[Prefix]_CY}}
{{Sum[Level]_[Prefix]_PY}}
```

**Parameters:**
- **Level:** Number of characters in the prefix (1-6)
- **Prefix:** The starting characters of accounts to sum

**Examples:**

| Placeholder | Matches Accounts | Returns |
|-------------|------------------|---------|
| `{{Sum1_A_CY}}` | All accounts starting with "A" | Total of A00000, A11101, A12345, etc. |
| `{{Sum2_A1_CY}}` | All accounts starting with "A1" | Total of A10000, A11101, A12345, etc. |
| `{{Sum3_A11_CY}}` | All accounts starting with "A11" | Total of A11000, A11101, A11999, etc. |
| `{{Sum4_A111_CY}}` | All accounts starting with "A111" | Total of A11100, A11101, A11199, etc. |

**How It Works:**

If your Chart of Accounts has:
- A10000 = $10,000
- A11101 = $125,450
- A12345 = $50,000
- A20000 = $75,000

Then:
- `{{Sum1_A_CY}}` = $260,450 (sum of all A accounts)
- `{{Sum2_A1_CY}}` = $185,450 (sum of A10000 + A11101 + A12345)
- `{{Sum3_A11_CY}}` = $125,450 (only A11101)

**Use Cases:**
- Subtotals in financial statements
- Category summaries (all Cash accounts, all Revenue accounts)
- Rollup calculations

**Real-World Example:**

```
ASSETS
Current Assets:
  Cash & Cash Equivalents      ${{Sum3_A11_CY}}
  Accounts Receivable          ${{Sum3_A12_CY}}
  Inventory                     ${{Sum3_A14_CY}}

Total Current Assets           ${{Sum2_A1_CY}}

Fixed Assets                   ${{Sum2_A2_CY}}

TOTAL ASSETS                   ${{Sum1_A_CY}}
```

> **✅ TIP:** Use Sum placeholders for subtotals and category rollups. This is much more efficient than manually adding individual accounts.

### 5.5 Placeholder Type 3: Debit/Credit Sum

**Purpose:** Sum debit or credit amounts for accounts matching a prefix

**Syntax:**
```
{{DebitSum[Level]_[Prefix]_CY}}
{{CreditSum[Level]_[Prefix]_CY}}
```

**Examples:**

| Placeholder | Returns |
|-------------|---------|
| `{{DebitSum3_B53_CY}}` | Total debit activity for accounts starting with B53 (current year) |
| `{{CreditSum3_B53_CY}}` | Total credit activity for accounts starting with B53 (current year) |
| `{{DebitSum2_A1_PY}}` | Total debit activity for accounts starting with A1 (previous year) |

**Difference from Regular Sum:**
- **Sum** returns the **ending balance** (net result)
- **DebitSum** returns the **total debits** (gross debit activity)
- **CreditSum** returns the **total credits** (gross credit activity)

**Use Cases:**
- Activity analysis (total sales, total purchases)
- Audit reports showing gross movements
- Reconciliation reports

**Real-World Example:**

```
REVENUE ANALYSIS

Total Sales (Credits)          ${{CreditSum3_B40_CY}}
Sales Returns (Debits)         ${{DebitSum3_B41_CY}}
Net Revenue                    ${{Sum2_B4_CY}}

Prior Year Comparison:
Total Sales (Credits)          ${{CreditSum3_B40_PY}}
Sales Returns (Debits)         ${{DebitSum3_B41_PY}}
Net Revenue                    ${{Sum2_B4_PY}}
```

### 5.6 Placeholder Type 4: Beginning Balance Sum

**Purpose:** Sum beginning balances (as of January 1) for accounts matching a prefix

**Syntax:**
```
{{BegSum[Level]_[Prefix]_CY}}
{{BegSum[Level]_[Prefix]_PY}}
```

**Examples:**

| Placeholder | Returns |
|-------------|---------|
| `{{BegSum3_A11_CY}}` | January 1 beginning balance sum for accounts starting with A11 (current year) |
| `{{BegSum1_A_PY}}` | January 1 beginning balance sum for all A accounts (previous year) |

**Use Cases:**
- Opening balances in financial statements
- Year-over-year balance comparisons
- Retained earnings rollforward

**Real-World Example:**

```
RETAINED EARNINGS ROLLFORWARD

Beginning Balance (Jan 1, {{CY}})     ${{BegSum3_E31_CY}}
Add: Net Income                        ${{E32101_CY}}
Less: Dividends                        ${{E33101_CY}}
Ending Balance (Dec 31, {{CY}})       ${{E31101_CY}}

Prior Year:
Beginning Balance (Jan 1, {{PY}})     ${{BegSum3_E31_PY}}
Net Income                             ${{E32101_PY}}
Ending Balance (Dec 31, {{PY}})       ${{E31101_PY}}
```

### 5.7 Placeholder Type 5: Specific Balance Types

**Purpose:** Retrieve specific balance types for a single account (debit, credit, beginning balance, or specific month)

**Syntax:**
```
{{AccountNumber_debit_CY}}      - Cumulative debit
{{AccountNumber_credit_CY}}     - Cumulative credit
{{AccountNumber_Jan1_CY}}       - January 1 beginning balance
```

**Examples:**

| Placeholder | Returns |
|-------------|---------|
| `{{A34101_debit_CY}}` | Total debits from January to selected month (current year) |
| `{{A34101_credit_CY}}` | Total credits from January to selected month (current year) |
| `{{A21101_Jan1_CY}}` | Beginning balance as of January 1 (current year) |
| `{{A21101_Jan1_PY}}` | Beginning balance as of January 1 (previous year) |

**Balance Type Reference:**

| Balance Type | What It Returns |
|--------------|-----------------|
| **Default (no suffix)** | Ending balance for selected period |
| **_debit** | Cumulative debit activity (Jan to selected month) |
| **_credit** | Cumulative credit activity (Jan to selected month) |
| **_Jan1** | Beginning balance as of January 1 |

**Use Cases:**
- Activity analysis for specific accounts
- Reconciliation between beginning and ending balances
- Detailed account movements

**Real-World Example:**

```
ACCOUNT RECONCILIATION: A12101 (Accounts Receivable)

Beginning Balance (Jan 1, {{CY}}):    ${{A12101_Jan1_CY}}
Add: Sales (Credits)                   ${{A12101_credit_CY}}
Less: Collections (Debits)             ${{A12101_debit_CY}}
Ending Balance ({{FinMonth}}/31/{{CY}}): ${{A12101_CY}}

Prior Year for Comparison:
Beginning Balance (Jan 1, {{PY}}):    ${{A12101_Jan1_PY}}
Ending Balance (Dec 31, {{PY}}):      ${{A12101_PY}}
```

### 5.8 Placeholder Type 6: Account Range (Exact)

**Purpose:** Sum accounts within a specific range (exact match)

**Syntax:**
```
{{StartAccount:EndAccount_[BalanceType]_CY}}
```

**Balance Type Codes:**
- **e** = ending balance (default)
- **b** = beginning balance
- **c** = credit
- **d** = debit

**Examples:**

| Placeholder | Returns |
|-------------|---------|
| `{{A74101:A75101_e_CY}}` | Sum of ending balances from A74101 to A75101 (current year) |
| `{{A10000:A19999_b_CY}}` | Sum of beginning balances from A10000 to A19999 (current year) |
| `{{B40000:B49999_c_CY}}` | Sum of credits from B40000 to B49999 (current year) |
| `{{B50000:B59999_d_PY}}` | Sum of debits from B50000 to B59999 (previous year) |

**How It Works:**

The system sums all accounts where the account number is >= StartAccount AND <= EndAccount.

If your accounts are:
- A74101 = $10,000
- A74500 = $15,000
- A75000 = $20,000
- A75101 = $25,000
- A75200 = $30,000

Then `{{A74101:A75101_e_CY}}` = $70,000 (sums A74101, A74500, A75000, A75101 only)

**Use Cases:**
- Summing a specific range of accounts
- Sub-category totals
- Department or project ranges

**Real-World Example:**

```
EXPENSE ANALYSIS

Salaries & Wages (E60000-E60999)      ${{E60000:E60999_e_CY}}
Benefits (E61000-E61999)              ${{E61000:E61999_e_CY}}
Payroll Taxes (E62000-E62999)         ${{E62000:E62999_e_CY}}

Total Compensation                     ${{E60000:E62999_e_CY}}

Prior Year Total Compensation          ${{E60000:E62999_e_PY}}
```

> **📘 NOTE:** Range placeholders include **all accounts** within the range, not just those with balances. If an account has a zero balance, it contributes 0 to the sum.

### 5.9 Placeholder Type 7: Wildcard Range

**Purpose:** Sum accounts matching a wildcard pattern using `?` as a wildcard character

**Syntax:**
```
{{StartPattern:EndPattern_[BalanceType]_CY}}
```

**Wildcard Rules:**
- `?` matches any single character (0-9, A-Z)
- Can use multiple `?` characters
- Patterns must be same length

**Examples:**

| Placeholder | Matches | Returns |
|-------------|---------|---------|
| `{{A????:B????_e_CY}}` | All accounts from A0000 to B9999 | Sum of ending balances |
| `{{A1???:A2???_e_CY}}` | All accounts from A1000 to A2999 | Sum of ending balances |
| `{{A3????:A4????_c_PY}}` | All accounts from A30000 to A49999 | Sum of credits (prior year) |

**How It Works:**

The `?` wildcard matches any character. The system:
1. Generates all possible account patterns
2. Finds actual accounts matching those patterns
3. Sums the balances

**Example:**

`{{A11??:A12??_e_CY}}` matches:
- A1100, A1101, A1102, ... A1199
- A1200, A1201, A1202, ... A1299

If your accounts include:
- A1101 = $10,000
- A1150 = $15,000
- A1201 = $20,000
- A1250 = $25,000

Then `{{A11??:A12??_e_CY}}` = $70,000

**Use Cases:**
- Flexible account groupings
- Pattern-based summations
- Dynamic account ranges

**Real-World Example:**

```
COMPREHENSIVE ASSET SUMMARY

All 1000-series Assets (A1000-A1999)  ${{A1???:A1???_e_CY}}
All 2000-series Assets (A2000-A2999)  ${{A2???:A2???_e_CY}}
All 3000-series Assets (A3000-A3999)  ${{A3???:A3???_e_CY}}

Total Current Assets (A1000-A3999)    ${{A1???:A3???_e_CY}}

Alternative using broader wildcard:
All Assets (A00000-A99999)            ${{A????:A????_e_CY}}
```

> **✅ TIP:** Wildcard ranges are powerful for creating flexible templates that work across different account structures. Use them when you want to sum broad categories.

### 5.10 Placeholder Type 8: Advanced Prefix with Dimensions

**Purpose:** Retrieve balance for an account filtered by specific dimensions (Subaccount, Branch, Organization, Ledger)

**Syntax:**
```
{{AccountNumber_sb[Subacct]_br[Branch]_or[Org]_ld[Ledger]_bt[BalType]_CY}}
```

**Dimension Codes:**
- **sb** = Subaccount
- **br** = Branch
- **or** = Organization
- **ld** = Ledger
- **bt** = Balance Type (credit, debit, beginning, ending)

**Examples:**

| Placeholder | Returns |
|-------------|---------|
| `{{A12345_sb000123_CY}}` | Balance for A12345 with Subaccount 000123 |
| `{{A12345_br001_CY}}` | Balance for A12345 at Branch 001 |
| `{{A12345_sb000123_br001_CY}}` | Balance for A12345 with Subaccount 000123 at Branch 001 |
| `{{A12345_btcredit_CY}}` | Credit activity for A12345 |
| `{{A12345_sb000123_br001_btcredit_CY}}` | Credit activity for A12345, Subaccount 000123, Branch 001 |

**Use Cases:**
- Multi-dimensional reporting
- Department or project-specific balances
- Branch or location analysis
- Subaccount detail reports

**Real-World Example:**

```
DEPARTMENT EXPENSE REPORT

IT Department (Subaccount 100):
  Salaries       ${{E60101_sb100_CY}}
  Supplies       ${{E65101_sb100_CY}}
  Travel         ${{E68101_sb100_CY}}

Sales Department (Subaccount 200):
  Salaries       ${{E60101_sb200_CY}}
  Supplies       ${{E65101_sb200_CY}}
  Travel         ${{E68101_sb200_CY}}

Branch Analysis:
NYC Branch (Branch 001):
  Total Revenue  ${{B40101_br001_CY}}

LA Branch (Branch 002):
  Total Revenue  ${{B40101_br002_CY}}
```

> **⚠️ WARNING:** Dimension filters are **exact match**. If you specify `br001`, only transactions with Branch = "001" are included. Use global Branch/Organization/Ledger filters (in the report setup) for broader filtering.

### 5.11 Placeholder Type 9: Year Constants

**Purpose:** Display the current or previous year value in your report

**Syntax:**
```
{{CY}}  - Current Year
{{PY}}  - Previous Year
```

**Examples:**

If Current Year is set to 2024:
- `{{CY}}` → 2024
- `{{PY}}` → 2023

**Use Cases:**
- Report headers and titles
- Comparative labels
- Dynamic date references

**Real-World Example:**

```
ABC COMPANY
INCOME STATEMENT
For the Year Ended December 31, {{CY}}

                                    {{CY}}          {{PY}}
Revenue                         ${{B40101_CY}}  ${{B40101_PY}}
Cost of Goods Sold             ${{B50101_CY}}  ${{B50101_PY}}
Gross Profit                    ${{B60101_CY}}  ${{B60101_PY}}

Comparative Period: {{CY}} vs. {{PY}}
```

### 5.12 Combining Multiple Placeholder Types

You can combine different placeholder types in a single template for comprehensive reports.

**Comprehensive Example:**

```
ACME CORPORATION
BALANCE SHEET
As of December 31, {{CY}}

                                            {{CY}}              {{PY}}
ASSETS
Current Assets:
  Cash & Equivalents                    ${{Sum3_A11_CY}}    ${{Sum3_A11_PY}}
  Accounts Receivable                   ${{A12101_CY}}      ${{A12101_PY}}
  Less: Allowance for Bad Debts         ${{A12201_CY}}      ${{A12201_PY}}
    Net Accounts Receivable             ${{A12101:A12999_e_CY}}  ${{A12101:A12999_e_PY}}
  Inventory                              ${{Sum3_A14_CY}}    ${{Sum3_A14_PY}}
  Prepaid Expenses                       ${{Sum3_A15_CY}}    ${{Sum3_A15_PY}}

    Total Current Assets                ${{A10000:A19999_e_CY}}  ${{A10000:A19999_e_PY}}

Fixed Assets:
  Property, Plant & Equipment            ${{Sum3_A20_CY}}    ${{Sum3_A20_PY}}
  Less: Accumulated Depreciation         ${{Sum3_A21_CY}}    ${{Sum3_A21_PY}}

    Net Fixed Assets                     ${{A20000:A29999_e_CY}}  ${{A20000:A29999_e_PY}}

Other Assets                             ${{A30000:A39999_e_CY}}  ${{A30000:A39999_e_PY}}

TOTAL ASSETS                             ${{Sum1_A_CY}}      ${{Sum1_A_PY}}


LIABILITIES & EQUITY
Current Liabilities:
  Accounts Payable                       ${{L21101_CY}}      ${{L21101_PY}}
  Accrued Expenses                       ${{Sum3_L22_CY}}    ${{Sum3_L22_PY}}
  Current Portion LT Debt                ${{L23101_CY}}      ${{L23101_PY}}

    Total Current Liabilities            ${{L20000:L29999_e_CY}}  ${{L20000:L29999_e_PY}}

Long-Term Liabilities                    ${{L30000:L39999_e_CY}}  ${{L30000:L39999_e_PY}}

    Total Liabilities                    ${{Sum1_L_CY}}      ${{Sum1_L_PY}}

Stockholders' Equity:
  Common Stock                           ${{E31101_CY}}      ${{E31101_PY}}
  Retained Earnings - Beginning          ${{E32101_Jan1_CY}}  ${{E32101_Jan1_PY}}
  Add: Net Income                        ${{E33101_CY}}      ${{E33101_PY}}
  Less: Dividends                        ${{E34101_CY}}      ${{E34101_PY}}
  Retained Earnings - Ending             ${{E32101_CY}}      ${{E32101_PY}}

    Total Equity                         ${{Sum1_E_CY}}      ${{Sum1_E_PY}}

TOTAL LIABILITIES & EQUITY               ${{Sum1_L_CY}} + {{Sum1_E_CY}}  ${{Sum1_L_PY}} + {{Sum1_E_PY}}
```

### 5.13 Placeholder Formatting

#### Number Formatting

By default, the system formats all numeric values as:
- **Format:** `#,##0` (thousands separator, no decimals)
- **Example:** 1250450 becomes 1,250,450
- **Zero values:** Display as "0"
- **Negative values:** Display with minus sign: -1,250,450

#### Custom Formatting in Word

You can apply additional formatting in your Word template:

1. **Currency symbols:** Add $ or other symbols outside the placeholder
   ```
   ${{A11101_CY}}  → $125,450
   ```

2. **Decimal places:** Use Word's field formatting after generation

3. **Percentages:** Calculate in Word using formulas
   ```
   Growth Rate: =({{A11101_CY}}-{{A11101_PY}})/{{A11101_PY}}
   ```

4. **Conditional formatting:** Use Word's conditional fields
   ```
   {IF {{A11101_CY}} > 0 "Positive" "Negative"}
   ```

### 5.14 Troubleshooting Placeholders

#### Problem: Placeholder shows "0" in generated report

**Possible Causes:**
1. Account doesn't exist in Chart of Accounts
2. Account has zero balance for the period
3. Dimensional filters don't match (Branch, Organization, Ledger)
4. Financial period has no data
5. Placeholder syntax is incorrect

**Solutions:**
1. Verify account number exists in GL
2. Check if account should have a balance
3. Review Branch/Organization/Ledger filters in report setup
4. Ensure data is posted for the selected period
5. Verify placeholder syntax (case-sensitive, correct format)

#### Problem: Placeholder still appears in generated report (not replaced)

**Possible Causes:**
1. Placeholder syntax is incorrect
2. Account number is invalid
3. Placeholder is split across multiple text runs in Word
4. Missing year suffix (_CY or _PY)

**Solutions:**
1. Check syntax: Must be `{{AccountNumber_CY}}` exactly
2. Verify account number exists
3. Retype the placeholder in Word (don't copy/paste)
4. Always include _CY or _PY suffix

#### Problem: Wildcard range returns unexpected results

**Possible Causes:**
1. Wildcard pattern too broad
2. Pattern length mismatch
3. Unexpected accounts match the pattern

**Solutions:**
1. Narrow the wildcard range
2. Ensure start and end patterns are same length
3. Test the pattern on a small subset first
4. Review which accounts match using a test report

---

## 6. Report Status & Workflow

### 6.1 Understanding Report Status

The **Status** field indicates the current state of your report. This section explains each status in detail.

#### Status: "File not Generated"

**Meaning:** Initial state. Report has been created but never generated.

**What you can do:**
- Edit all fields
- Upload or change template
- Click "Generate Report" to start generation

**What you cannot do:**
- Download report (no file exists yet)

**Next Status:** "File Generation In Progress" (when you click Generate Report)

#### Status: "File Generation In Progress"

**Meaning:** Report is currently being generated in a background process.

**What the system is doing:**
- Authenticating with API
- Fetching financial data
- Processing placeholders
- Populating template
- Saving generated file

**What you can do:**
- Wait for completion (typically 1-5 minutes)
- Navigate to other screens (process continues)
- Refresh to check current status

**What you cannot do:**
- Edit any fields (all disabled system-wide)
- Generate other reports (system-wide restriction)
- Download the current report (not ready yet)
- Cancel the generation (no cancel option)

**Next Status:**
- "Ready to Download" (if successful)
- "Failed to Generate File" (if error occurs)

> **📘 NOTE:** While ANY report has status "In Progress", ALL generate/download buttons are disabled for ALL reports. Only one generation can run at a time.

#### Status: "Ready to Download"

**Meaning:** Report successfully generated and available for download.

**What you can do:**
- Download the generated report
- Generate the report again (overwrites previous)
- Edit fields and regenerate

**What you cannot do:**
- Nothing restricted at this status

**File Available:** Yes, GeneratedFileID is populated

**Typical Actions:**
1. Select the record (checkbox)
2. Click "Download Report"
3. Save file to your computer
4. Open in Microsoft Word
5. Review the report

#### Status: "Failed to Generate File"

**Meaning:** An error occurred during report generation.

**What you can do:**
- Review error logs (administrator access)
- Check troubleshooting guide (Section 7)
- Verify credentials
- Verify template file
- Try generating again after fixing issues

**Common Causes:**
- Invalid API credentials
- Network connectivity issues
- Invalid template file
- Missing placeholder data
- API timeout

**Next Steps:**
1. Identify the error (check logs or contact administrator)
2. Fix the underlying issue
3. Try "Generate Report" again

### 6.2 Status Lifecycle Diagram

```
┌─────────────────────────┐
│  New Record Created     │
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│  File not Generated     │◄────────┐
│  (Pending)              │         │
└────────────┬────────────┘         │
             │                      │
             │ Click                │
             │ Generate             │ Edit fields
             │ Report               │ and
             ▼                      │ regenerate
┌─────────────────────────┐         │
│  File Generation        │         │
│  In Progress            │         │
│  (Processing)           │         │
└────────┬────────┬───────┘         │
         │        │                 │
    Success│      │Error            │
         │        │                 │
         ▼        ▼                 │
┌─────────────┐  ┌──────────────┐  │
│Ready to     │  │Failed to     │  │
│Download     │──┤Generate File │──┘
└──────┬──────┘  └──────────────┘
       │
       │ Click
       │ Download
       ▼
┌─────────────────────────┐
│  File Downloaded        │
│  (Report still shows    │
│   "Ready to Download")  │
└─────────────────────────┘
```

### 6.3 Background Processing Details

#### What Happens During "File Generation In Progress"?

The report generation runs as a long-running background operation using Acumatica's `PXLongOperation` framework. Here's the detailed workflow:

**Phase 1: Pre-Validation (1-2 seconds)**
- ✓ Verify record is selected
- ✓ Verify template file exists (contains "FRTemplate")
- ✓ Verify no other generation is in progress
- ✓ Update status to "In Progress"
- ✓ Save record

**Phase 2: Authentication (5-10 seconds)**
- Retrieve credentials from database (based on Company Number)
- Connect to Acumatica API endpoint
- Authenticate using OAuth2 (password grant)
- Receive access token
- Store token for subsequent requests

**Phase 3: Template Processing (5-15 seconds)**
- Download template file from Acumatica file storage
- Save to temporary location
- Open Word document
- Extract all placeholders using regex pattern: `\{\{[^{}]+\}\}`
- Merge fragmented text runs
- Catalog all unique placeholders

**Phase 4: Placeholder Analysis (2-5 seconds)**
- Categorize placeholders by type:
  - Wildcard ranges (highest priority)
  - Exact ranges
  - Regular placeholders
- Group placeholders by API call requirements
- Create optimized API request plan

**Phase 5: Data Fetching (30-120 seconds, varies by data volume)**

The system makes **6 parallel API calls**:

1. **Current Year Period Data**
   - Endpoint: `/odata/{tenant}/TrialBalance`
   - Filter: FinancialPeriod = Selected Month/Year
   - Fields: Account, BeginningBalance, EndingBalance, Debit, Credit
   - Pagination: 5000 records per page

2. **Previous Year Period Data**
   - Same as #1 but for Previous Year

3. **Current Year January Beginning Balance**
   - Filter: FinancialPeriod = "01/{CurrentYear}"
   - Used for "Jan1" placeholders

4. **Previous Year January Beginning Balance**
   - Filter: FinancialPeriod = "01/{PreviousYear}"
   - Used for "Jan1" placeholders

5. **Current Year Cumulative Data (Jan to Selected Month)**
   - Cumulative data from January through selected month
   - Used for "debit" and "credit" placeholders

6. **Previous Year Cumulative Data (Full Year)**
   - Full year cumulative data
   - Used for prior year "debit" and "credit" placeholders

**Concurrency:** Up to 5 parallel requests at a time
**Timeout:** 3 minutes per request
**Retry:** No automatic retry (errors fail immediately)

**Phase 6: Data Processing (10-30 seconds)**
- Process regular placeholders (direct lookup)
- Process exact range placeholders (sum accounts in range)
- Process wildcard range placeholders (pattern matching + sum)
- Process sum prefix placeholders (prefix matching + sum)
- Process dimensional placeholders (filter by dimensions + sum)
- Format all values as `#,##0`
- Store results in memory

**Phase 7: Template Population (10-20 seconds)**
- Open template document
- Iterate through all paragraphs (headers, footers, body)
- Search for placeholders
- Replace with calculated values
- Update document fields
- Save to output location

**Phase 8: File Storage (5-10 seconds)**
- Upload generated file to Acumatica file storage
- Create NoteDoc association
- Link file to report record (GeneratedFileID)
- Delete temporary files

**Phase 9: Cleanup & Finalization (2-5 seconds)**
- Logout from API
- Delete temporary template file
- Delete temporary output file
- Update status to "Ready to Download"
- Save record

**Phase 10: Error Handling (if any error occurs)**
- Catch exception
- Log detailed error message
- Update status to "Failed to Generate File"
- Cleanup temporary files
- Logout from API (if authenticated)
- Save record

**Total Time:** 1-5 minutes typical, up to 10 minutes for very large data sets

### 6.4 Concurrency Limitations

#### System-Wide Generation Lock

**Important:** Only **ONE** report can generate at a time across the entire system.

**Why?**
- Prevents resource conflicts
- Ensures API rate limits are not exceeded
- Maintains system stability
- Prevents concurrent access to temporary file locations

**How It Works:**

The system checks all records before allowing generation:

```
IF any record has Status = "File Generation In Progress"
  THEN disable "Generate Report" button for ALL records
  ELSE enable "Generate Report" button for selected records
```

**User Impact:**

If User A is generating a report:
- User B cannot generate ANY report (even a different template)
- User B must wait for User A's report to complete
- User B can still create, edit, and view reports
- User B cannot download reports during this time

**Best Practices:**
- Schedule large report generations during off-hours
- Inform team when generating long-running reports
- Monitor status before starting new generations
- Keep templates optimized to reduce generation time

### 6.5 Monitoring Long-Running Reports

#### How to Check Progress

1. **Stay on the screen:**
   - Status updates are visible immediately
   - No need to refresh manually

2. **Navigate away and return:**
   - Progress continues in background
   - Return to screen and check Status field
   - Click Refresh (F5) if status hasn't updated

3. **Check from another workstation:**
   - Log in from different computer
   - Navigate to Financial Report Maintenance
   - View status of the generating report

#### Expected Time Estimates

| Data Volume | Typical Time |
|-------------|--------------|
| Small (< 1,000 accounts, < 10,000 transactions) | 1-2 minutes |
| Medium (1,000-5,000 accounts, 10,000-50,000 transactions) | 2-4 minutes |
| Large (5,000-10,000 accounts, 50,000-100,000 transactions) | 4-8 minutes |
| Very Large (> 10,000 accounts, > 100,000 transactions) | 8-15 minutes |

> **📘 NOTE:** Times vary based on network speed, server performance, and API response time.

#### What If Generation Takes Too Long?

If a report has been "In Progress" for more than 15 minutes:

1. **Contact your administrator** - They can check server logs
2. **Check network connectivity** - Ensure API server is accessible
3. **Wait a bit longer** - Very large data sets may take longer
4. **Last resort** - Administrator may need to restart the application service (this will fail the current generation)

### 6.6 Regenerating Reports

#### Why Regenerate?

Common reasons to regenerate an existing report:
- Updated financial data (new postings)
- Fixed placeholder errors in template
- Changed report parameters (different branch, month, etc.)
- Previous generation failed
- Testing template changes

#### How to Regenerate

1. **Option A: Same Parameters**
   - Select the record (checkbox)
   - Click "Generate Report"
   - System overwrites previous generated file

2. **Option B: Different Parameters**
   - Select the record
   - Edit fields (Year, Month, Branch, etc.)
   - Save changes
   - Upload new template (if template changed)
   - Select the record (checkbox)
   - Click "Generate Report"

> **⚠️ WARNING:** Regenerating **overwrites** the previous generated file. If you want to keep the old file, download it first before regenerating.

#### Version Control

The system does **not** maintain version history of generated files. Each regeneration replaces the previous file.

**Best Practices:**
- Download and rename important reports before regenerating
- Use descriptive Template Names to indicate version (e.g., "Balance Sheet v2")
- Maintain template files outside Acumatica for backup

---

## 7. Troubleshooting Guide

This section provides solutions to common issues you may encounter while using the Financial Report Application.

### 7.1 Common Error Messages

#### Error: "Please select a template to generate the report."

**Cause:** No record is selected via the checkbox.

**Solution:**
1. Locate the "Select" column in the grid
2. Check the checkbox for the desired report record
3. Click "Generate Report" again

---

#### Error: "The selected template does not have any attached files."

**Cause:** No template file is attached to the record, or the filename doesn't contain "FRTemplate".

**Solution:**
1. Click the **Files** tab or paper clip icon
2. Check if any files are attached
3. If no files: Click **Upload File** and attach your .docx template
4. If files exist but error persists: Verify filename contains "FRTemplate"
5. Rename file to include "FRTemplate" (e.g., "MyReport_FRTemplate.docx")
6. Re-upload the renamed file
7. Save the record
8. Try generation again

---

#### Error: "A report generation process is already running for this template."

**Cause:** Another report (possibly a different template) is currently generating.

**Solution:**
1. Check the **Status** column for all records
2. Find the record with status "File Generation In Progress"
3. Wait for that report to complete (check Status until it changes)
4. Once complete, try your generation again

**Prevention:**
- Coordinate with team members during report generation
- Schedule large reports during off-hours

---

#### Error: "Failed to authenticate. Please check credentials."

**Cause:** API credentials are invalid, expired, or incorrectly configured.

**Solution:**
1. Verify the **Company Number** in your report matches a configured tenant
2. Contact your administrator to check **Tenant Credentials Maintenance**
3. Administrator should verify:
   - Base URL is correct
   - Username and Password are valid
   - Client ID and Client Secret are correct
   - API user account is active in Acumatica
   - API user has necessary permissions
4. Test credentials by logging into Acumatica API manually
5. If credentials changed recently, update in Tenant Credentials
6. Try generation again

---

#### Error: "Current Year is not specified for the selected report."

**Cause:** The "Current Year" field is empty or null.

**Solution:**
1. Select the report record
2. Enter a valid 4-digit year in the **Current Year** field (e.g., 2024)
3. Click **Save**
4. Try generation again

---

#### Error: "No generated file is available for download. Please generate the report first."

**Cause:** Attempting to download before generation is complete, or generation failed.

**Solution:**
1. Check the **Status** field
2. If status is "File not Generated": Click "Generate Report" first
3. If status is "File Generation In Progress": Wait for completion
4. If status is "Failed to Generate File": Troubleshoot the failure and regenerate
5. Only when status is "Ready to Download" can you download

---

#### Error: "Failed to fetch OData" or "PTDBalance not found in OData response."

**Cause:** API connection issue or data structure mismatch.

**Solution:**
1. **Check API connectivity:**
   - Verify Base URL is accessible from the server
   - Test URL in browser: `{BaseURL}/odata/{TenantName}`
   - Check firewall rules
2. **Check OData endpoint:**
   - Verify TrialBalance OData entity exists
   - Check if endpoint is `/odata/` or `/t/` (system tries both)
3. **Check financial data:**
   - Ensure data exists for the selected period
   - Verify General Ledger is posted
4. **Contact administrator** if issue persists

---

#### Error: "Tenant Name must be unique."

**Cause:** Attempting to save tenant credentials with a duplicate Tenant Name.

**Solution:**
1. In **Tenant Credentials Maintenance**, check existing records
2. Choose a different, unique Tenant Name
3. Save the record

---

### 7.2 Generation Issues

#### Problem: Report generation takes longer than expected

**Expected Time:** 1-5 minutes for most reports
**If longer than 10 minutes:**

**Possible Causes:**
- Very large data set (thousands of accounts, millions of transactions)
- Slow network connection to API server
- API server under heavy load
- Complex template with many wildcard placeholders

**Solutions:**
1. **Wait a bit longer** - Large reports can take up to 15 minutes
2. **Check network connectivity** - Test ping to API server
3. **Simplify template** - Reduce number of wildcard placeholders
4. **Generate during off-hours** - Avoid peak usage times
5. **Contact administrator** - Check server logs for issues

---

#### Problem: Status stuck at "File Generation In Progress" for over 30 minutes

**Possible Causes:**
- Generation process crashed
- Network timeout
- Server issue

**Solutions:**
1. **Contact administrator immediately**
2. **Check server logs** (administrator)
3. **Restart application service** (administrator) - This will fail the stuck generation
4. **After restart**, status should update to "Failed to Generate File"
5. **Investigate root cause** before regenerating

---

#### Problem: Generation fails immediately (status changes to "Failed" in seconds)

**Possible Causes:**
- Invalid credentials
- Missing template file
- Network connectivity issue
- API server down

**Solutions:**
1. **Check error logs** (administrator access)
2. **Verify credentials** in Tenant Credentials Maintenance
3. **Verify template file** is attached and contains "FRTemplate"
4. **Test API connectivity** - Try accessing API endpoint in browser
5. **Check server status** - Ensure Acumatica instance is running
6. **Review troubleshooting checklist** in Section 7.5

---

### 7.3 Placeholder Issues

#### Problem: Placeholders show "0" in generated report

**Possible Causes & Solutions:**

**Cause 1: Account doesn't exist**
- **Solution:** Verify account number in Chart of Accounts
- **How to check:** Look up account in General Ledger
- **Fix:** Correct the account number in template

**Cause 2: Account has zero balance**
- **Solution:** This may be correct - verify expected balance
- **How to check:** Run Trial Balance for the period
- **If unexpected:** Check if transactions are posted

**Cause 3: Wrong period selected**
- **Solution:** Verify Financial Month matches your data
- **How to check:** Check when data was posted
- **Fix:** Change Financial Month in report parameters

**Cause 4: Dimensional filters don't match**
- **Solution:** Check Branch, Organization, Ledger filters
- **Example:** Report filtered to Branch "001" but account has transactions in Branch "002"
- **Fix:** Either adjust report filters or use dimensional placeholders

**Cause 5: Placeholder syntax incorrect**
- **Solution:** Verify exact syntax: `{{AccountNumber_CY}}`
- **Common mistakes:**
  - Missing underscore: `{{A11101CY}}` ❌
  - Wrong case: `{{a11101_cy}}` ❌
  - Extra spaces: `{{ A11101_CY }}` ❌
- **Fix:** Correct the syntax in template

---

#### Problem: Placeholder appears in report unchanged (not replaced)

**Possible Causes & Solutions:**

**Cause 1: Syntax error**
- **Solution:** Check exact format: `{{PLACEHOLDER_CY}}`
- **Common mistakes:**
  - Single braces: `{A11101_CY}` ❌
  - Missing braces: `A11101_CY` ❌
  - Wrong bracket type: `[[A11101_CY]]` ❌
- **Fix:** Use correct syntax with double curly braces

**Cause 2: Missing year suffix**
- **Solution:** All placeholders require `_CY` or `_PY`
- **Example:** `{{A11101}}` ❌ → `{{A11101_CY}}` ✓
- **Fix:** Add year suffix

**Cause 3: Placeholder split across text runs**
- **Cause:** Word sometimes splits text formatting, breaking placeholders
- **How to identify:** Placeholder looks correct but doesn't work
- **Solution:**
  1. Delete the placeholder completely
  2. Retype it (don't copy/paste)
  3. Use consistent formatting (no bold/italic within placeholder)

**Cause 4: Invalid account number**
- **Solution:** Account number doesn't exist in system
- **Fix:** Verify account number and correct in template

---

#### Problem: Wildcard range returns unexpected sum

**Possible Causes & Solutions:**

**Cause 1: Pattern too broad**
- **Example:** `{{A????:Z????_e_CY}}` matches ALL accounts from A0000 to Z9999
- **Solution:** Narrow the pattern to specific ranges

**Cause 2: Pattern length mismatch**
- **Example:** `{{A???:A????_e_CY}}` ❌ (different lengths)
- **Solution:** Ensure both patterns are same length

**Cause 3: Unintended accounts match**
- **Example:** `{{A1???:A2???_e_CY}}` matches A1000-A2999
- **Check:** Review your Chart of Accounts for unexpected matches
- **Solution:** Use exact range if you know specific accounts

---

### 7.4 Download Issues

#### Problem: Download button is disabled

**Possible Causes & Solutions:**

**Cause 1: Status not "Ready to Download"**
- **Solution:** Check Status field
- **If "File not Generated":** Generate the report first
- **If "In Progress":** Wait for completion
- **If "Failed":** Troubleshoot and regenerate

**Cause 2: No record selected**
- **Solution:** Check the Select checkbox for the record

**Cause 3: Another generation in progress**
- **Solution:** Wait for other generation to complete
- **Check:** Look for any record with status "In Progress"

---

#### Problem: Downloaded file is corrupted or won't open

**Possible Causes & Solutions:**

**Cause 1: Incomplete download**
- **Solution:** Download again
- **Check:** File size should be > 0 bytes

**Cause 2: Browser extension interference**
- **Solution:** Disable browser extensions and try again
- **Alternative:** Try different browser

**Cause 3: Template file was corrupted**
- **Solution:** Re-upload a fresh template and regenerate

**Cause 4: Word version incompatibility**
- **Solution:** Ensure Microsoft Word 2016 or later
- **Alternative:** Try opening in Word Online

---

### 7.5 Pre-Generation Troubleshooting Checklist

Before generating a report, verify the following:

**Tenant Credentials:**
- ☐ Credentials configured for the Company Number
- ☐ Base URL is correct and accessible
- ☐ Username and Password are valid
- ☐ Client ID and Client Secret are correct
- ☐ API user has GL data access permissions

**Report Configuration:**
- ☐ Template Name is filled in
- ☐ Company Number matches configured credentials
- ☐ Current Year is set (4-digit year)
- ☐ Financial Month is selected (01-12)
- ☐ Branch/Organization/Ledger filters are appropriate (if used)

**Template File:**
- ☐ File is attached (visible in Files tab)
- ☐ Filename contains "FRTemplate"
- ☐ File is valid .docx format
- ☐ Placeholders use correct syntax
- ☐ Template opens correctly in Word

**System Status:**
- ☐ No other report shows "In Progress" status
- ☐ Record is saved
- ☐ Record is selected (checkbox checked)

**Financial Data:**
- ☐ Data exists for selected period
- ☐ Accounts exist in Chart of Accounts
- ☐ Transactions are posted
- ☐ Period is not closed/locked (if regenerating)

If all items are checked and generation still fails, contact your administrator with details of the error.

### 7.6 Getting Help

#### When to Contact Your Administrator

Contact your administrator if:
- Authentication errors persist after verifying credentials
- Status stuck at "In Progress" for over 30 minutes
- Repeated generation failures
- API connectivity issues
- Need to review server logs
- Need to configure new tenant credentials

**Information to Provide:**
- Template Name
- Company Number
- Error message (exact text)
- Steps taken before error
- Screenshot of the issue

#### When to Contact Acumatica Support

Contact Acumatica support if:
- API endpoint not responding
- OData entity issues
- Acumatica application errors
- Permission issues for API user
- OAuth configuration problems

#### Self-Help Resources

- This User Guide (Section 7 - Troubleshooting)
- Placeholder Quick Reference (Appendix A)
- Error Message Reference (Appendix B)
- Your organization's IT documentation

---

## 8. Best Practices & Tips

### 8.1 Template Design Best Practices

#### Naming Conventions

**Template Names:**
✅ **DO:**
- Use descriptive names: "Balance Sheet Q4 2024"
- Include period: "Income Statement Monthly 2024"
- Include version: "Cash Flow Statement v2"
- Use consistent naming across templates

❌ **DON'T:**
- Use vague names: "Report1", "Template"
- Use special characters: "Report@#2024"
- Use very long names (>100 characters)

**File Names:**
✅ **DO:**
- Always include "FRTemplate": "BalanceSheet_FRTemplate.docx"
- Use descriptive prefixes: "Q4Report_FRTemplate_2024.docx"
- Use underscores instead of spaces: "Income_Statement_FRTemplate.docx"

❌ **DON'T:**
- Omit "FRTemplate": "BalanceSheet.docx" ❌
- Use only "FRTemplate.docx" (not descriptive)

#### Placeholder Best Practices

**Consistency:**
- Always use uppercase for account numbers
- Always include year suffix (_CY or _PY)
- Use consistent formatting throughout template

**Organization:**
- Group related placeholders together
- Comment complex placeholders in template
- Document custom calculations

**Testing:**
- Start with simple placeholders
- Test each new placeholder type before using extensively
- Verify calculations manually for first report

**Performance:**
- Limit wildcard placeholders (they're more expensive)
- Use exact ranges when you know the accounts
- Avoid redundant placeholders (use Word formulas for calculations)

#### Template Structure

**Headers & Footers:**
```
ABC COMPANY
BALANCE SHEET
As of December 31, {{CY}}
Page {PAGE} of {NUMPAGES}
```

**Comparative Columns:**
Use tables for clean alignment:

| Account | {{CY}} | {{PY}} | Change |
|---------|--------|--------|--------|
| Cash | {{A11101_CY}} | {{A11101_PY}} | =[Column2]-[Column3] |

**Subtotals & Totals:**
- Use Sum placeholders for subtotals
- Use Word formulas for grand totals if combining multiple sums
- Label clearly: "Total Current Assets", "TOTAL ASSETS"

### 8.2 Report Configuration Best Practices

#### Parameter Selection

**Current Year:**
- Use actual year (2024, not "24")
- For comparative reports, "Current Year" is the newer year
- Document fiscal year vs calendar year if different

**Financial Month:**
- Month represents END of period
- For Q1 report, use 03 (March)
- For Q2 report, use 06 (June)
- For year-end, use 12 (December)

**Branch/Organization/Ledger:**
- Leave blank for consolidated reports
- Specify for detailed/departmental reports
- Document filter logic in report description
- Be consistent across related reports

#### Company Number Management

**Strategy:**
- Use 1 for production
- Use 2 for test environment
- Use 3+ for subsidiaries
- Document the mapping

**Example:**
| Company Number | Description | Environment |
|----------------|-------------|-------------|
| 1 | Main Company | Production |
| 2 | Main Company | Test |
| 3 | Subsidiary A | Production |
| 4 | Subsidiary B | Production |

### 8.3 Data Quality Best Practices

#### Before Generation

**Verify Data:**
- ☐ All transactions posted
- ☐ Period closed (if required)
- ☐ Reconciliations complete
- ☐ Adjusting entries made
- ☐ No pending imports

**Check Accounts:**
- ☐ Chart of Accounts up to date
- ☐ Account numbers match template
- ☐ No archived accounts used in template
- ☐ Subaccounts configured if using dimensional placeholders

#### After Generation

**Review Report:**
- ☐ All placeholders replaced (no `{{...}}` visible)
- ☐ No unexpected zeros
- ☐ Totals reconcile
- ☐ Year-over-year changes are reasonable
- ☐ Formatting is correct

**Validation:**
- Compare totals to Trial Balance
- Spot-check key accounts manually
- Verify calculations (subtotals, percentages)
- Review with stakeholders before distribution

### 8.4 Security Best Practices

**Credentials:**
- Never share tenant credentials
- Use dedicated API users (not personal accounts)
- Rotate credentials periodically
- Limit access to Tenant Credentials screen

**Templates:**
- Store master templates in a secure location
- Version control templates (track changes)
- Backup templates regularly
- Limit who can modify templates

**Generated Reports:**
- Download and store securely
- Apply appropriate file permissions
- Consider watermarking sensitive reports
- Delete old generated reports from system

**Access Control:**
- Grant access based on role (viewer vs generator vs admin)
- Review user access quarterly
- Remove access for terminated employees
- Audit report generation activity

### 8.5 Performance Optimization

**Template Optimization:**
- Minimize wildcard placeholders
- Use exact ranges when possible
- Avoid duplicate placeholders
- Test with production data volume

**Scheduling:**
- Generate large reports during off-hours
- Coordinate with team to avoid conflicts
- Consider automated scheduling (if available)
- Stagger multiple report generations

**Data Volume:**
- Archive old financial periods if not needed
- Consider period-specific templates
- Use filters to limit data scope
- Monitor generation times and adjust

### 8.6 Workflow Tips

**For Regular Users:**

1. **Create Template Library:**
   - Build reusable templates
   - Document each template's purpose
   - Maintain version history
   - Share templates with team

2. **Monthly Close Process:**
   - Generate preliminary reports before close
   - Review for anomalies
   - Make adjustments
   - Generate final reports after close
   - Archive with month-end documentation

3. **Year-Over-Year Reports:**
   - Always use comparative format
   - Include {{CY}} and {{PY}} columns
   - Add variance column (use Word formulas)
   - Document significant changes

**For Administrators:**

1. **Initial Setup:**
   - Configure all tenant credentials upfront
   - Test connectivity before granting user access
   - Document configuration for future reference
   - Train users on proper usage

2. **Maintenance:**
   - Review credentials quarterly
   - Update API user permissions as needed
   - Monitor error logs
   - Track generation performance
   - Clean up old generated files

3. **Support:**
   - Create internal troubleshooting guide
   - Document common issues specific to your organization
   - Maintain list of contacts (API admin, network admin, etc.)
   - Schedule regular training for new users

### 8.7 Common Pitfalls to Avoid

**❌ Mistake:** Forgetting to upload template before generating
**✅ Solution:** Always verify file attachment before clicking Generate

**❌ Mistake:** Using personal accounts for API credentials
**✅ Solution:** Create dedicated service accounts

**❌ Mistake:** Not testing templates before production use
**✅ Solution:** Test with sample data first

**❌ Mistake:** Ignoring "Failed" status without investigation
**✅ Solution:** Always troubleshoot failures before retrying

**❌ Mistake:** Generating multiple reports simultaneously
**✅ Solution:** Wait for completion before starting next

**❌ Mistake:** Not documenting custom placeholders
**✅ Solution:** Add comments in template or separate documentation

**❌ Mistake:** Using wildcard ranges everywhere
**✅ Solution:** Use specific accounts or exact ranges when possible

**❌ Mistake:** Not backing up templates
**✅ Solution:** Maintain template repository with version control

### 8.8 Efficiency Tips

**⚡ Quick Wins:**

1. **Use Template Cloning:**
   - Create one master template
   - Clone for variations (different periods, branches)
   - Modify clones instead of creating from scratch

2. **Leverage Word Features:**
   - Use styles for consistent formatting
   - Create table templates
   - Use fields for dynamic dates
   - Set up page numbering

3. **Standardize Report Parameters:**
   - Create naming convention document
   - Use consistent Branch/Organization codes
   - Standardize month selection (always use end of period)

4. **Batch Downloads:**
   - Generate multiple reports
   - Download all at once
   - Organize in folder structure
   - Name files consistently

5. **Documentation:**
   - Create internal wiki for your organization
   - Document account mappings
   - Maintain placeholder cheat sheet
   - Record lessons learned

**⏱️ Time Savers:**

- Save frequently used filter combinations
- Create template variants for common scenarios
- Use descriptive Template Names for easy searching
- Keep a log of generation times to predict duration

---

## 9. Appendices

### Appendix A: Field Reference Table

Complete reference of all user-facing fields in the Financial Report Application.

#### Financial Report Maintenance Fields

| Field Name | Display Name | Data Type | Required | Description | Example Value |
|------------|--------------|-----------|----------|-------------|---------------|
| ReportID | Report ID | Integer | Auto | Unique identifier (hidden) | 123 |
| ReportCD | Template Name | String(225) | Yes | Name of report template | "Balance Sheet Q4 2024" |
| Description | Description | String(50) | No | Brief description | "Year-end balance sheet" |
| CompanyNum | Company Number | Integer | Yes | Links to tenant credentials | 1 |
| CurrYear | Current Year | String(4) | Yes | Year for report | "2024" |
| FinancialMonth | Financial Month | String(2) | Yes | Month for report period | "12" |
| Branch | Branch | String(10) | No | Branch filter | "MAIN" |
| Organization | Organization | String(50) | No | Organization filter | "CORPORATE" |
| Ledger | Ledger | String(20) | No | Ledger filter | "ACTUAL" |
| Status | Status | String(20) | Read-only | Current status | "Ready to Download" |
| Selected | Select | Boolean | No | Selection checkbox | true/false |
| UploadedFileID | Uploaded File ID | GUID | No | Template file ID (hidden) | {guid} |
| GeneratedFileID | Generated File ID | GUID | No | Generated file ID (hidden) | {guid} |

#### Tenant Credentials Fields

| Field Name | Display Name | Data Type | Required | Encrypted | Description |
|------------|--------------|-----------|----------|-----------|-------------|
| CompanyNum | Company Number | Integer | Yes | No | Unique identifier |
| TenantName | Tenant Name | String(50) | Yes | No | Unique tenant name |
| BaseURL | Base URL | String(255) | Yes | No | API base URL |
| UsernameNew | Username | String | Yes | Yes (RSA) | API username |
| PasswordNew | Password | String | Yes | Yes (RSA) | API password |
| ClientIDNew | Client ID | String | Yes | Yes (RSA) | OAuth Client ID |
| ClientSecretNew | Client Secret | String | Yes | Yes (RSA) | OAuth Client Secret |

### Appendix B: Complete Error Message Reference

Alphabetical listing of all possible error messages with causes and solutions.

| Error Message | Cause | Solution |
|---------------|-------|----------|
| "A report generation process is already running for this template." | Another report is generating | Wait for completion |
| "Access token not found in response." | API authentication response invalid | Check credentials, contact admin |
| "Company Number is required." | Tenant credential validation | Enter Company Number |
| "Current Year is not specified for the selected report." | Missing required field | Enter Current Year |
| "Failed to authenticate" | API credentials invalid | Verify credentials in Tenant Credentials |
| "Failed to authenticate. Please check credentials." | Invalid API credentials | Check username, password, client ID/secret |
| "Failed to fetch OData" | API data retrieval error | Check connectivity, verify endpoint |
| "Failed to refresh token" | Token refresh failed | Re-authenticate, check credentials |
| "Failed to retrieve the file content." | File read error | Check file exists and is accessible |
| "Failed to save: {0}" | General save error | Check details, verify data integrity |
| "Missing Config. Check Web Configurations" | Configuration error | Check application settings |
| "No API credentials found for company" | Credentials not configured | Configure Tenant Credentials |
| "No CompanyID found for ReportID {0}." | Company mapping missing | Set Company Number in report |
| "No files are associated with this record." | No template attached | Upload template file |
| "No generated file is available for download." | Generate not complete | Generate report first |
| "No Month or Year Specified" | Missing period fields | Enter Financial Month and Year |
| "No record is selected" | No checkbox checked | Select record via checkbox |
| "NoteID is null." | Internal file reference error | Contact administrator |
| "Please select a Branch" | Missing required filter | Select Branch |
| "Please select a Branch or Organization" | Missing dimension filter | Select Branch or Organization |
| "Please select a Ledger" | Missing required filter | Select Ledger |
| "Please select a template to generate the report." | No record selected | Check Select checkbox |
| "PTDBalance not found in OData response." | API data structure issue | Check API endpoint, contact admin |
| "Tenant mapping is missing in Database" | No credentials configured | Configure Tenant Credentials |
| "Tenant mapping not found." | Company to tenant mapping missing | Set Company Number correctly |
| "Tenant Name is required." | Tenant credential validation | Enter Tenant Name |
| "Tenant Name must be unique." | Duplicate tenant name | Choose different Tenant Name |
| "The selected template does not have any attached files." | Template file missing | Upload file with "FRTemplate" in name |
| "The selected template file is empty or could not be retrieved." | Template file corrupted | Re-upload fresh template |
| "Token expiration not found in response." | API token response incomplete | Check API configuration |
| "Unable to save the generated file." | File save error | Check permissions, disk space |

### Appendix C: Placeholder Quick Reference Card

#### Basic Syntax
```
{{AccountNumber_CY}}  - Current year ending balance
{{AccountNumber_PY}}  - Previous year ending balance
```

#### Sum Placeholders
```
{{Sum[Level]_[Prefix]_CY}}        - Sum accounts by prefix
{{DebitSum[Level]_[Prefix]_CY}}   - Sum debits
{{CreditSum[Level]_[Prefix]_CY}}  - Sum credits
{{BegSum[Level]_[Prefix]_CY}}     - Sum beginning balances
```

#### Range Placeholders
```
{{Start:End_e_CY}}  - Sum ending balances in range
{{Start:End_b_CY}}  - Sum beginning balances in range
{{Start:End_c_CY}}  - Sum credits in range
{{Start:End_d_CY}}  - Sum debits in range
```

#### Wildcard Placeholders
```
{{A????:B????_e_CY}}  - Wildcard range (? = any character)
```

#### Special Balance Types
```
{{Account_debit_CY}}   - Cumulative debit
{{Account_credit_CY}}  - Cumulative credit
{{Account_Jan1_CY}}    - January 1 beginning balance
```

#### Dimensional Filtering
```
{{Account_sb[Subacct]_CY}}           - Filter by subaccount
{{Account_br[Branch]_CY}}            - Filter by branch
{{Account_or[Org]_CY}}               - Filter by organization
{{Account_ld[Ledger]_CY}}            - Filter by ledger
{{Account_bt[BalType]_CY}}           - Specific balance type
{{Account_sb[Sub]_br[Br]_CY}}        - Multiple dimensions
```

#### Year Constants
```
{{CY}}  - Current Year (e.g., 2024)
{{PY}}  - Previous Year (e.g., 2023)
```

### Appendix D: Glossary of Terms

**Account Number:** Unique identifier for a General Ledger account (e.g., A11101)

**API (Application Programming Interface):** Software interface that allows communication between systems

**Balance Type:** Type of balance (ending, beginning, debit, credit)

**Beginning Balance:** Account balance at the start of a period (typically January 1)

**Branch:** Organizational dimension representing a physical location or division

**Chart of Accounts:** Complete listing of all accounts in the General Ledger

**Client ID:** OAuth 2.0 application identifier

**Client Secret:** OAuth 2.0 application secret key

**Company Number:** Unique identifier linking reports to tenant credentials

**Cumulative:** Total from beginning of period to current date

**Current Year (CY):** The year specified in the report's "Current Year" field

**DAC (Data Access Class):** Acumatica framework class representing database tables

**Dimensional Filter:** Filter based on Branch, Organization, Subaccount, or Ledger

**Ending Balance:** Account balance at the end of a period

**Financial Month:** The ending month for the report period

**Financial Period:** Specific time period (month/year) for financial data

**General Ledger (GL):** Complete record of financial transactions

**Graph:** Acumatica business logic class

**Ledger:** Accounting ledger type (ACTUAL, BUDGET, FORECAST)

**NoteDoc:** Acumatica file attachment association

**OAuth2:** Authentication protocol used for API access

**OData:** Open Data Protocol for RESTful APIs

**Organization:** Organizational dimension representing a legal entity

**Placeholder:** Tag in template that gets replaced with data (e.g., {{A11101_CY}})

**Previous Year (PY):** Current Year minus 1

**RSA Encryption:** Cryptographic encryption for secure credential storage

**Status:** Current state of report (Pending, In Progress, Ready, Failed)

**Subaccount:** Sub-classification of a General Ledger account

**Template:** Microsoft Word document containing placeholders

**Tenant:** Separate company or environment in Acumatica

**Tenant Credentials:** API authentication details for a tenant

**Trial Balance:** Report showing all account balances for a period

**Wildcard:** Pattern matching character (?) that matches any single character

**Year-over-Year:** Comparison between two consecutive years

### Appendix E: Sample Templates

#### Sample 1: Simple Balance Sheet

```
ACME CORPORATION
BALANCE SHEET
As of December 31, {{CY}}

ASSETS
Cash                                ${{A11101_CY}}
Accounts Receivable                 ${{A12101_CY}}
Inventory                           ${{A14101_CY}}
TOTAL ASSETS                        ${{Sum1_A_CY}}

LIABILITIES
Accounts Payable                    ${{L21101_CY}}
TOTAL LIABILITIES                   ${{Sum1_L_CY}}

EQUITY
Retained Earnings                   ${{E31101_CY}}
TOTAL EQUITY                        ${{Sum1_E_CY}}

TOTAL LIABILITIES & EQUITY          ${{Sum1_L_CY}} + {{Sum1_E_CY}}
```

#### Sample 2: Comparative Income Statement

```
ACME CORPORATION
INCOME STATEMENT
For the Year Ended December 31, {{CY}}

                                    {{CY}}              {{PY}}
REVENUE
Sales Revenue                   ${{B40101_CY}}      ${{B40101_PY}}
Service Revenue                 ${{B40201_CY}}      ${{B40201_PY}}
Total Revenue                   ${{Sum2_B4_CY}}     ${{Sum2_B4_PY}}

COST OF GOODS SOLD              ${{Sum2_B5_CY}}     ${{Sum2_B5_PY}}

GROSS PROFIT                    ${{Sum1_B_CY}}      ${{Sum1_B_PY}}

EXPENSES
Operating Expenses              ${{E60000:E69999_e_CY}}  ${{E60000:E69999_e_PY}}

NET INCOME                      ${{E70101_CY}}      ${{E70101_PY}}
```

#### Sample 3: Cash Flow Summary

```
ACME CORPORATION
CASH FLOW SUMMARY
Year Ended December 31, {{CY}}

Cash - Beginning (Jan 1)            ${{A11101_Jan1_CY}}

Cash Receipts (Credits)             ${{A11101_credit_CY}}
Cash Disbursements (Debits)         ${{A11101_debit_CY}}

Cash - Ending (Dec 31)              ${{A11101_CY}}

Prior Year Comparison:
Cash - Beginning (Jan 1, {{PY}})    ${{A11101_Jan1_PY}}
Cash - Ending (Dec 31, {{PY}})      ${{A11101_PY}}
```

---

## Document Information

**Document Title:** Financial Report Application - Comprehensive User Guide
**Version:** 1.0
**Last Updated:** November 2025
**Prepared For:** Acumatica ERP Users
**Document Owner:** IT Department

**Revision History:**

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | Nov 2025 | System | Initial release |

**Feedback:**

If you have suggestions for improving this guide, please contact your administrator or IT department.

---

**END OF USER GUIDE**
