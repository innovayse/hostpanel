# 📊 WHMCS Reports - Полный Анализ (37 отчётов)

## Общая Структура

Все отчёты имеют единую структуру:
- **Заголовок** с названием отчёта и описанием
- **Фильтры** (свёртываемые)
- **Визуализация** (Chart или Table)
- **Таблица данных** внизу
- **Export опции** (обычно в правом углу)

---

## 📈 BILLING REPORTS (13 отчётов)

### 1. **Daily Performance** 🔴 PRIORITY 1
**Тип:** Area Chart (многоцветный) + Data Table
- **Chart:** 6 метрик с разными цветами (Completed Orders, New Invoices, Paid Invoices, Failed Gateways, Ticket Replies, Cancellation Requests)
- **Legend:** Справа от графика с colored dots
- **Table columns:** Date | Completed Orders | New Invoices | Paid Invoices | Failed Gateways | Ticket Replies | Cancellation Requests
- **Filters:** Date Range picker
- **Tooltip:** При наведении показывает точные значения
- **Export:** PDF, CSV, Excel
- **Date format:** YYYY-MM-DD

### 2. **Annual Income Report** 🔴 PRIORITY 1
**Тип:** Bar Chart (вертикальный) + Data Table
- **Chart:** Столбчатая диаграмма по месяцам (красный цвет)
- **Table columns:** Month | Amount (в валюте)
- **Filters:** Year selector, Currency selector (USD, EUR, GBP, AUD)
- **Y-axis:** Денежные значения (0 - max)
- **X-axis:** Месяцы (Jan - Dec)
- **Interactive:** При клике на столбец может фильтровать таблицу
- **Tooltip:** Показывает точное значение при наведении
- **Color:** Red (#E53E3E или similar)

### 3. **Income Forecast** 🔴 PRIORITY 1
**Тип:** Area Chart (заполненный, голубой) + Data Table
- **Chart:** Cumulative Income Forecast (прогноз дохода)
- **Fill:** Голубой, полупрозрачный
- **Table columns:** Month | Monthly | Quarterly | Semi Annual | Annual | Biennial | Triennial | Total
- **Filters:** 
  - Currency selector (USD, EUR, GBP, AUD)
  - Date Range (можно выбрать период)
- **Y-axis:** Денежные значения
- **X-axis:** Месяцы с датами
- **Trends:** Показывает восходящий тренд (кривая растёт)

### 4. **Aging Invoices** 🔴 PRIORITY 2
**Тип:** Data Table only (нет графика)
- **Table columns:** 
  - Invoice # | Client | Amount | Aging Bucket | Due Date | Days Outstanding
  - Дополнительные: Invoice Date, Status
- **Aging Buckets:** 
  - 0-30 days (green indicator • )
  - 31-60 days (yellow indicator • )
  - 61-90 days (orange indicator • )
  - 90+ days (red indicator • )
- **Indicators:** Цветные bullet points слева от суммы
- **Filters:**
  - Date Range (due date)
  - Client selector
  - Status (Unpaid, Partially Paid, etc.)
- **Sorting:** Можно сортировать по Days Outstanding (DESC)
- **Table styling:** Striped rows, hover effect, collapsible sections by aging bucket

### 5. **Monthly Transactions** 🟠 PRIORITY 2
**Тип:** Data Table
- **Table columns:** Date | Type (Credit/Debit) | Gateway | Amount | Description | Status
- **Filters:**
  - Date Range (created date)
  - Type (All, Credit, Debit)
  - Gateway (All, Stripe, PayPal, etc.)
  - Status (All, Completed, Failed, Pending)
- **Pagination:** 20-50 items per page
- **Totals row:** Внизу сумма по Type (Total Credit, Total Debit)
- **Status badges:** Colored pills (green for completed, red for failed, yellow for pending)

### 6. **Income by Product** 🟠 PRIORITY 2
**Тип:** Table (с группировкой по продуктам)
- **Table columns:** 
  - Product Name | Unit Price | Units Sold | Total Income | Percentage of Total
  - Secondary: Description, Category
- **Grouping:** Можно раскрывать/скрывать по категориям
- **Totals:** 
  - Subtotal по категориям
  - Grand Total внизу
- **Percentage bar:** Визуальная линия, показывающая % от общего дохода
- **Filters:**
  - Date Range (billing date)
  - Category selector
  - Status (Active, Suspended, etc.)
- **Sorting:** По доходу DESC по умолчанию

### 7. **Sales Tax Liability** 🟠 PRIORITY 3
**Тип:** Data Table
- **Table columns:** 
  - Tax Type | Taxable Amount | Tax Rate | Tax Amount | Due Date | Status
  - Secondary: Tax Jurisdiction, Period
- **Filters:**
  - Date Range (period)
  - Tax Type (VAT, Sales Tax, GST, etc.)
  - Jurisdiction (country/region)
- **Status:** Paid / Unpaid / Overdue (colored badges)
- **Totals:** Total Taxable Amount | Total Tax Amount
- **Export:** PDF, CSV, Excel (для подачи налоговой отчётности)

### 8. **Server Revenue Forecasts** 🟠 PRIORITY 3
**Тип:** Area Chart + Data Table
- **Chart:** Area chart с разными шейдами для разных серверов
- **Multiple lines:** Для каждого сервера своя линия/площадь
- **Table columns:** 
  - Server Name | Current Revenue | Forecast 3M | Forecast 6M | Forecast 12M | Trend
  - Secondary: Total Capacity, Utilization %
- **Filters:**
  - Server group selector
  - Time period (3M, 6M, 12M forecast)
  - Confidence level (Low, Medium, High)
- **Trend indicators:** Стрелки вверх/вниз или % change

### 9. **Top 10 Clients by Income** 🟡 PRIORITY 3
**Тип:** Data Table (сортированная, top list)
- **Table columns:** 
  - Rank | Client Name | Total Income | # Invoices | Avg Invoice | Last Invoice Date
  - Secondary: Status, Account Type
- **Rank:** 1-10 с номерами
- **Filters:**
  - Date Range (revenue date)
  - Min Income threshold (slider или input)
  - Status (Active, Inactive, All)
  - Account Type (Individual, Company, etc.)
- **Sorting:** By Income DESC (fixed)
- **Pagination:** Нет, always top 10

### 10. **VAT MOSS** 🟡 PRIORITY 3
**Тип:** Data Table
- **Table columns:**
  - Transaction ID | Client Country | Supplier Country | VAT Rate | Net Amount | VAT Amount | Currency | Status
  - Secondary: Invoice #, Date, Product
- **Filters:**
  - Date Range
  - Country (supplier/client)
  - Status (Reported, Pending, Non-Taxable)
  - VAT Rate (5%, 10%, 20%, etc.)
- **Grouping:** By Month or Quarter
- **Compliance:** Table структурирована для MOSS reporting (EU VAT regulations)

### 11. **Credits Reviewer** 🟡 PRIORITY 3
**Тип:** Data Table
- **Table columns:**
  - Client Name | Credit Balance | Applied Credits | Remaining Balance | Last Updated | Actions
  - Secondary: Credit Source (Refund, Promotion, Adjustment)
- **Filters:**
  - Date Range (last updated)
  - Client selector
  - Balance status (Has Credits, No Credits, Negative)
- **Indicators:** 
  - Green ✓ for positive balance
  - Red ✗ for negative balance
- **Actions button:** View transaction history, Adjust balance

### 12. **Transactions** 🟡 PRIORITY 3
**Тип:** Data Table (список всех транзакций)
- **Table columns:**
  - Date | Type | Gateway | Amount | Client | Invoice # | Status | Description
  - Secondary: Transaction ID, Fees, Currency
- **Filters:**
  - Date Range (created date)
  - Type (All, Credit, Debit, Refund)
  - Gateway (Stripe, PayPal, Bank Transfer, etc.)
  - Status (Completed, Failed, Pending, Refunded)
  - Client search (autocomplete)
  - Amount range (slider)
- **Pagination:** 50 items per page
- **Status badges:** Color-coded (green/red/yellow)
- **Search:** Full-text search in Description

### 13. **Invoices** 🟡 PRIORITY 3
**Тип:** Data Table (все счета)
- **Table columns:**
  - Invoice # | Client | Amount | Status | Due Date | Date Issued | Days Past Due
  - Secondary: Currency, Tax, Total Paid
- **Filters:**
  - Date Range (issued date or due date)
  - Status (Draft, Sent, Paid, Partially Paid, Unpaid, Overdue, Refunded, Cancelled)
  - Client search
  - Amount range
  - Currency
- **Status badges:** Color-coded pills
- **Actions:** View, Edit, Pay, Refund, Send, Download PDF
- **Quick stats:** Top summary cards showing Total Revenue, Unpaid Amount, Overdue Count

---

## 👥 CLIENT REPORTS (8 отчётов)

### 14. **New Customers** 🔴 PRIORITY 1
**Тип:** Line/Area Chart + Data Table
- **Chart:** Line или Area chart с несколькими линиями (New Signups, Orders Placed, Orders Completed)
- **Legend:** справа с colored dots (может быть inline или отдельно)
- **Y-axis:** Количество клиентов
- **X-axis:** Месяцы/даты
- **Table columns:** Month | New Signups | Orders Placed | Orders Completed | Conversion Rate
- **Filters:**
  - Date Range (signup date)
  - Source selector (All, Referral, Direct, etc.)
  - Status (Active, Inactive)
- **Tooltip:** При наведении показывает values
- **Trend:** % change from previous period

### 15. **Clients by Country** 🟠 PRIORITY 2
**Тип:** Map visualization + Data Table
- **Map:** World map с цветными регионами (зелёный = больше клиентов)
- **Intensity:** Более яркий зелёный = больше клиентов
- **Table columns:** Country | # Clients | Total Revenue | Avg Lifetime Value | Active Status
- **Filters:**
  - Country selector (или на карте)
  - Signup date range
  - Status (All, Active, Inactive, Suspended)
  - Min clients threshold
- **Click on map:** Может фильтровать таблицу по выбранной стране
- **Tooltip on map:** Country name + count + total revenue

### 16. **Client** 🟡 PRIORITY 3
**Тип:** Data Table (детальная информация о клиентах)
- **Table columns:**
  - Client # | Name | Email | Company | Status | Total Spent | Last Login | Date Joined
  - Secondary: Country, Phone, City
- **Filters:**
  - Date range (joined date)
  - Status (Active, Inactive, Suspended, Closed)
  - Min spent (amount)
  - Country selector
  - Search by name/email
- **Pagination:** 25-50 items per page
- **Status badges:** Green (Active), Yellow (Inactive), Red (Suspended)
- **Click row:** Opens client detail view

### 17. **Client Statement** 🟡 PRIORITY 3
**Тип:** Data Table (выписка клиента)
- **Table columns:**
  - Date | Type | Description | Amount | Running Balance
  - Secondary: Invoice #, Transaction ID
- **Filters:**
  - Client selector (required)
  - Date Range (statement period)
  - Type (Invoice, Payment, Credit, Adjustment)
- **Running Balance:** Cumulative sum down the column
- **Totals row:**
  - Total Invoiced | Total Paid | Balance Due
- **Summary cards:** Top cards showing Total Revenue from Client, Credit Balance, Aging info
- **Export:** PDF (formatted as official statement)

### 18. **Client Sources** 🟡 PRIORITY 3
**Тип:** Pie Chart + Data Table
- **Chart:** Pie chart showing distribution by source
- **Colors:** Different color for each source
- **Table columns:**
  - Source | # New Clients | % of Total | Total Revenue | Avg Lifetime Value | ROI
  - Secondary: First Source vs Last Source
- **Filters:**
  - Date Range (signup date)
  - Source category (Paid Ads, Referral, Direct, Affiliate, etc.)
- **Percentages:** На pie chart и в таблице
- **Tooltip:** На pie chart shows % and count

### 19. **Customer Retention Time** 🟡 PRIORITY 3
**Тип:** Data Table + possibly Bar Chart
- **Table columns:**
  - Period | New Customers | Retained from Previous | Churn Rate | Avg Lifetime (months) | Retention Rate
  - Secondary: Revenue per Retained Customer
- **Chart:** Bar chart showing retention rate over time
- **Filters:**
  - Date Range (by period)
  - Service type (All, Hosting, SSL, Domains, etc.)
  - Status
- **Metrics:** 
  - Churn rate (% left)
  - Retention rate (% stayed)
  - Avg lifetime (months customers stay)

### 20. **Clients** 🟡 PRIORITY 3
**Тип:** Data Table (full client list)
- **Table columns:** 
  - Client # | Name | Email | Status | Total Spent | Invoices | Last Invoice | Join Date
  - Secondary: Credit Balance, Account Type
- **Filters:**
  - Search (by name, email, company)
  - Status (Active, Inactive, Suspended, Closed)
  - Date Range (joined)
  - Min/Max spend
  - Account type
  - Country
- **Pagination:** 50 items per page
- **Bulk actions:** Select multiple, export, send email, change status
- **Sorting:** By any column
- **Status indicators:** Colored badges

### 21. **Affiliates Overview** 🟡 PRIORITY 3
**Тип:** Data Table
- **Table columns:**
  - Affiliate | Status | Total Referred | Total Earned | Commissions Paid | Commissions Due | Conversion Rate
  - Secondary: Last Activity, Join Date
- **Filters:**
  - Date Range (commission period)
  - Status (Active, Inactive, Suspended)
  - Min earned threshold
  - Sort by earned DESC
- **Status badges:** Active (green), Inactive (gray), Suspended (red)
- **Commission details:** Hover shows breakdown

---

## 🌐 DOMAINS & SERVICES REPORTS (8 отчётов)

### 22. **Domains** 🟡 PRIORITY 3
**Тип:** Data Table
- **Table columns:**
  - Domain | Client | Status | Registrar | Renewal Date | Days Until Renewal | Price | Auto-Renew
  - Secondary: Created Date, Expiry Date, Registration Years
- **Filters:**
  - Status (Active, Expired, Expiring Soon, Suspended, Pending)
  - Date Range (expiry date)
  - Registrar (GoDaddy, Namecheap, etc.)
  - Client search
  - Extension (.com, .net, .org, etc.)
- **Warnings:** Red rows for domains expiring within 30 days
- **Pagination:** 50 items per page
- **Quick stats:** Total domains, Expiring soon count, Auto-renew enabled count

### 23. **Domain Renewal Emails** 🟡 PRIORITY 4
**Тип:** Data Table (email tracking)
- **Table columns:**
  - Domain | Client Email | Sent Date | Status | Open Count | Click Count | Bounced
  - Secondary: Email Template Used, Renewal Price
- **Filters:**
  - Date Range (sent date)
  - Status (Sent, Opened, Clicked, Bounced, Failed)
  - Domain search
  - Client email search
- **Status indicators:** Envelope icon (sent), Eye icon (opened), Hand icon (clicked), X icon (bounced)
- **Activity:** Shows if email was opened and what links were clicked

### 24. **SSL Certificate Monitoring** 🟡 PRIORITY 4
**Тип:** Data Table
- **Table columns:**
  - Domain | Certificate Issuer | Expiry Date | Days Until Expiry | Status | Provider | Auto-Renew
  - Secondary: Issued Date, Common Name, Subject Alt Names
- **Filters:**
  - Status (Active, Expiring Soon, Expired, Revoked)
  - Date Range (expiry)
  - Provider (Let's Encrypt, Sectigo, DigiCert, etc.)
  - Auto-Renew (Yes, No)
- **Warnings:** Red rows for < 30 days until expiry
- **Status badges:** Green (valid), Orange (warning), Red (expired)
- **Quick check:** Icon showing if cert is valid or revoked

### 25. **Smarty Compatibility** 🟡 PRIORITY 4
**Тип:** Data Table (system compatibility)
- **Table columns:**
  - Component | Version | Compatibility Status | Last Checked | Issues Found | Action Required
  - Secondary: Description, Recommendation
- **Status badges:** Green (compatible), Yellow (warning), Red (incompatible)
- **Issues column:** List of problems if any
- **Auto-check:** Shows "Last checked X minutes ago"

### 26. **Services** 🟡 PRIORITY 3
**Тип:** Data Table (all services/products)
- **Table columns:**
  - Service Name | Product | Client | Status | Next Due Date | Renewal Price | Auto-Renew | Cycles Remaining
  - Secondary: Creation Date, Billing Cycle
- **Filters:**
  - Status (Active, Suspended, Cancelled, Pending)
  - Date Range (next due date)
  - Product type
  - Client search
  - Billing cycle (monthly, yearly, etc.)
- **Pagination:** 50 items per page
- **Bulk actions:** Suspend, Cancel, Change renewal status
- **Status badges:** Color-coded

### 27. **Product Suspensions** 🟡 PRIORITY 4
**Тип:** Data Table
- **Table columns:**
  - Service # | Client | Product | Suspension Date | Reason | Status | Renewal Action
  - Secondary: Suspended By (Admin), Reason Details
- **Filters:**
  - Date Range (suspension date)
  - Status (Suspended, Unsuspended, Deleted)
  - Product type
  - Reason (Non-payment, Abuse, Request, etc.)
  - Client search
- **Status badges:** Red (suspended), Green (unsuspended)
- **Reason:** Text or predefined reason codes
- **Actions:** Unsuspend, Delete, View client

### 28. **Disk Usage Summary** 🟡 PRIORITY 4
**Тип:** Data Table or progress bars
- **Table columns:**
  - Service # | Client | Product | Used Space | Total Space | % Used | Status
  - Secondary: Updated Date, Warnings
- **Progress bars:** Visual representation of % used
- **Filters:**
  - Status (Normal, Warning, Critical)
  - % usage range (0-50%, 50-80%, 80-100%)
  - Product type
  - Client search
- **Color coding:**
  - Green (< 70%)
  - Yellow (70-90%)
  - Red (> 90%)
- **Warnings:** Alert if > 90% used

---

## 🎫 SUPPORT & TICKETS REPORTS (5 отчётов)

### 29. **Support Ticket Replies** 🟡 PRIORITY 4
**Тип:** Data Table
- **Table columns:**
  - Month | Total Tickets | Admin Replies | Client Replies | Avg Response Time | Avg Resolution Time | Total Comments
  - Secondary: Satisfaction Score, Close Rate
- **Filters:**
  - Date Range (by month)
  - Department (All, Sales, Support, etc.)
  - Status (Resolved, Open, Closed)
- **Metrics:** 
  - Avg response time (hours/days)
  - Avg resolution time (days)
  - Reply count
- **Trends:** % change from previous period

### 30. **Ticket Feedback Scores** 🟡 PRIORITY 4
**Тип:** Generate Report with Filters
- **Filters:**
  - Date Range picker
  - Ticket Open Date picker
  - Compare By (Date / Staff Member / Department)
  - Status (All, Resolved, Closed)
- **Generate button:** Blue button to generate report
- **Output:** Could be Chart (scatter/gauge) or Table with scores
- **Ratings:** 1-5 star scale
- **Filter submit:** Button at bottom "Generate Report"

### 31. **Ticket Feedback Comments** 🟡 PRIORITY 4
**Тип:** Data Table
- **Table columns:**
  - Ticket # | Client | Date | Staff Member | Rating | Comment Summary | Sentiment
  - Secondary: Full comment, Category
- **Filters:**
  - Date Range (feedback date)
  - Rating (1-5 stars, or ranges)
  - Staff member selector
  - Department
  - Sentiment (Positive, Neutral, Negative - if available)
- **Comments:** Expandable rows to show full comment text
- **Sentiment colors:** Green (positive), Gray (neutral), Red (negative)

### 32. **Ticket Ratings Reviewer** 🟡 PRIORITY 4
**Тип:** Data Table or Dashboard cards
- **Table columns (alternative):**
  - Staff Member | Avg Rating | # Ratings | Satisfaction % | Ratings 5★ | Ratings 4★ | Ratings 3★ | Ratings 2★ | Ratings 1★
- **Or Dashboard style:**
  - Cards for each staff member showing:
    - Name
    - Average rating (visual stars)
    - Total ratings
    - Satisfaction percentage (gauge)
- **Filters:**
  - Date Range (rating date)
  - Staff member
  - Rating range
- **Sorting:** By avg rating DESC
- **Comparative view:** Compare multiple staff members

### 33. **Ticket Tags** 🟡 PRIORITY 4
**Тип:** Data Table
- **Table columns:**
  - Tag | # Tickets | % of Total | Avg Resolution Time | Avg Satisfaction | Active Tickets
  - Secondary: Last Used, Created Date
- **Filters:**
  - Date Range (tag created/used date)
  - Department
  - Min/Max usage
- **Percentages:** % of all tickets that use this tag
- **Sorting:** By # Tickets DESC

---

## 📊 PERFORMANCE & MARKETING REPORTS (3 отчёта)

### 34. **Daily Performance** ✅ (Already covered above as #1)

### 35. **Monthly Orders** 🟡 PRIORITY 4
**Тип:** Line Chart + Data Table
- **Chart:** Line chart showing orders over time
- **Y-axis:** # of Orders
- **X-axis:** Months
- **Table columns:**
  - Month | Total Orders | Completed | Cancelled | Failed | Avg Order Value | Total Revenue
  - Secondary: Growth %, Customer Count
- **Filters:**
  - Date Range (by month)
  - Status (All, Completed, Cancelled, Failed)
  - Product type
  - Min/Max order value
- **Tooltip:** Shows exact values on hover
- **Trend:** % change from previous month

### 36. **Promotions Usage** 🟡 PRIORITY 4
**Тип:** Data Table
- **Table columns:**
  - Promo Code | Description | Status | Uses | Total Discount | Avg Discount per Use | Revenue Generated | ROI
  - Secondary: Created Date, Expiry Date, Max Uses
- **Filters:**
  - Date Range (used date)
  - Status (Active, Expired, Used Up, Disabled)
  - Min/Max discount
  - Promo type (% off, $ off, Free shipping, etc.)
- **ROI:** (Revenue Generated - Total Discount) / Cost to run promo
- **Sorting:** By ROI DESC
- **Status badges:** Green (active), Red (expired), Gray (disabled)

### 37. **Server Revenue Forecasts** ✅ (Already covered above as #8)

---

## 🎨 Common UI Patterns

### Chart Library
- **Types used:** Area Charts, Bar Charts, Line Charts, Pie Charts, Maps, Progress Bars
- **Colors:** 
  - Primary: Blues, Greens
  - Success: Green (#48BB78)
  - Warning: Yellow/Orange (#ED8936)
  - Error: Red (#E53E3E)
  - Gray/Neutral: #E2E8F0, #CBD5E0
- **Legend:** Usually positioned right or bottom, colored dots with labels
- **Tooltip:** On hover shows values, formatted with currency/percentage as needed
- **Responsive:** Charts adapt to container width

### Table Patterns
- **Header:** White/light background, text-muted color, uppercase labels
- **Rows:** Alternating white/gray rows, hover state with subtle background
- **Pagination:** Bottom of table, usually 25/50/100 items per page
- **Sorting:** Click column header to sort ascending/descending
- **Filters:** Above table in collapsible section or sidebar
- **Sticky header:** Header stays visible when scrolling
- **Mobile:** Tables scroll horizontally on small screens

### Filter Patterns
- **Types:**
  - Date Range picker (from - to)
  - Select dropdown (single or multi)
  - Checkbox group
  - Text search (autocomplete)
  - Number range (min - max with sliders)
  - Status multi-select
- **Apply/Clear buttons:** "Apply Filters" and "Clear Filters"
- **Reset:** Clear button resets all filters

### Status Indicators
- **Colors:**
  - Green bullet • or Green badge for Success/Active/Paid
  - Yellow bullet • or Yellow badge for Warning/Pending
  - Red bullet • or Red badge for Error/Overdue/Failed
  - Gray bullet • or Gray badge for Inactive/Disabled
- **Badge styles:** Rounded pill shape with colored background and text

### Export Options
- **Formats:** PDF, CSV, Excel
- **Placement:** Usually right side of header or in menu
- **Functionality:**
  - PDF: Formatted report with logo, title, charts, tables
  - CSV/Excel: Raw data in spreadsheet format
  - Settings: Option to select which columns to export

### Date Range Picker
- **Single picker:** From date only
- **Double picker:** From date - To date (on same line usually)
- **Presets:** "Last 7 days", "This month", "Last month", "This year", "Custom range"
- **Format:** Displayed as "YYYY-MM-DD" or locale-specific

### Currency/Formatting
- **Currency:** Amounts prefixed with $ / € / £ / ֏
- **Numbers:** Thousands separator (comma or space)
- **Decimals:** 2 decimal places for currency, 0-2 for percentages
- **Percentages:** 0-100% range with % symbol

---

## 🔄 Report Generation Flow

1. **User visits report page**
2. **See description and default filters** (usually last 30 days or current month)
3. **Optionally modify filters**
4. **Click "Apply Filters" or auto-generate**
5. **See Chart + Table**
6. **Option to Export (PDF/CSV/Excel)**

For some reports (like Ticket Feedback Scores), there's explicit "Generate Report" button.

---

## 📋 Database Schema Implications

For each report, we need to track:

```sql
-- Aggregated data (for performance)
ReportCache
  - reportType (string)
  - period (month/week/day)
  - filters (JSON)
  - chartData (JSON)
  - tableData (JSON)
  - generatedAt (datetime)
  - expiresAt (datetime)

-- Transaction-level data (for detail reports)
-- Already exists: Transactions, Invoices, BillableItems, etc.

-- Aggregated Billing metrics
BillingMetrics
  - period
  - totalRevenue
  - totalInvoices
  - paidInvoices
  - unpaidInvoices
  - overdueInvoices
  - averageInvoiceValue
  - collectionRate

-- Support metrics
SupportMetrics
  - period
  - totalTickets
  - resolvedTickets
  - avgResponseTime
  - avgResolutionTime
  - satisfactionScore

-- Client metrics
ClientMetrics
  - period
  - newClientsCount
  - churnedClientsCount
  - totalClientsActive
  - totalRevenue
  - retentionRate

-- Domain/Service metrics
ServiceMetrics
  - period
  - activeServices
  - renewalsDue
  - expiredServices
  - totalRevenue
  - suspendedCount
```

---

## ✅ Implementation Roadmap

### Phase 1: Core Reports (Week 1-2)
1. Daily Performance
2. Annual Income Report
3. Income Forecast
4. New Customers

### Phase 2: Billing Reports (Week 3-4)
5. Aging Invoices
6. Monthly Transactions
7. Income by Product
8. Top 10 Clients by Income

### Phase 3: Extended Billing (Week 5-6)
9. Sales Tax Liability
10. Server Revenue Forecasts
11. VAT MOSS
12. Credits Reviewer

### Phase 4: Client Reports (Week 7-8)
13. Clients by Country (with Map)
14. Client Sources
15. Customer Retention Time
16. Clients list
17. Affiliates Overview

### Phase 5: Services & Support (Week 9-10)
18. Services list
19. Domains list
20. Product Suspensions
21. Support Ticket Replies
22. Ticket Feedback Scores

### Phase 6: Remaining & Polish (Week 11+)
23-37. Remaining reports by priority

---

## 🛠️ Technology Stack

- **Frontend Charts:** Chart.js or Recharts (Vue 3 compatible)
- **Frontend Maps:** Leaflet or MapBox (for Clients by Country)
- **Date picker:** Vue3-datepicker or similar
- **Export:** pdfkit (PDF), SheetJS (Excel/CSV)
- **State management:** Pinia (reports store)
- **Caching:** Redis (cache generated reports)
- **Backend:** CQRS with Wolverine queries
- **Database:** EF Core with aggregation queries

