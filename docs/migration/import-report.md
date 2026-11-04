---
title: Import report and skip reasons
description: How to read the migration import report, what each status means, and how to fix every skip reason.
---

# Import report and skip reasons

<Label type="admin" />

Every record the import touches gets a log entry. Open a migration job and scroll to the
**Import Report** section to see the log.

## Reading the report

The report has five columns: **Type**, **Identifier**, **Status**, **Reason** and **Time**.

Filter by **Status** (All statuses, Imported, Skipped, Failed) and by **Type** (All types or one
entity) to focus on the records that need attention.

## Statuses

| Status | Meaning | Action needed |
|---|---|---|
| Imported | The record was created here. | None. |
| Skipped | The record was intentionally not created. | Check the reason column; most skips can be fixed and the job retried. |
| Failed | An unexpected error stopped this record. The detail is in the server log. | Check the server log, fix the problem and retry the job. |

## Skip reasons

To fix a skipped record: resolve the cause listed below, then run the job again. Records that
already exist are skipped on the next run; only the ones you fixed are created.

| Reason | Key | Which entities | Why | What to do |
|---|---|---|---|---|
| Already exists | `alreadyExists` | Clients, Contacts, Products, Services, Domains, Invoices, Tickets, Quotes, Orders, Transactions, Knowledgebase articles, Announcements, Network Issues, Downloads, Ticket Replies | The same record was found here (clients by email, contacts by email within the client, products by name, services by client and domain or client and panel username, domains by domain name, invoices by client and external id, tickets and quotes by client and subject, orders by order number, transactions by client and transaction id, knowledgebase articles by title, announcements by title and date, network issues by title and start date, downloads by title and category, ticket replies by ticket, author, role and message). | Nothing. The existing record is kept. |
| Client not found | `clientNotFound` | Contacts, Invoices, Services, Domains, Tickets, Ticket Replies, Orders, Transactions, Quotes | The client this record belongs to was not imported here. | Check whether the client was skipped or excluded from the job, fix the cause, and retry. |
| Account could not be created | `accountNotProvisioned` | Clients | The client's Hostpanel account could not be provisioned by the account provider. | Check the account provider configuration and retry. |
| Product group not found | `productGroupNotFound` | Products | The product group this product belongs to is missing from the source feed. | Check WHMCS: the group must exist and be exported. Fix the data in WHMCS and retry. |
| No products exist | `noProducts` | Services | No product exists here to attach the service to. | Create or import products first, then retry. |
| Product not found | `productNotFound` | Services, Orders | No product here carries the name this service or order refers to. | Create a product with a matching name, then retry. |
| Ticket not found | `ticketNotFound` | Ticket Replies | The parent ticket was not imported here. | Check whether the ticket was skipped or excluded, fix the cause, and retry. |
| Ticket is closed | `ticketClosed` | Ticket Replies | The parent ticket was imported but is closed. | Reopen the ticket, then retry. |
| Invoice not found | `invoiceNotFound` | Transactions | The invoice this transaction pays was not imported here. | Check whether the invoice was skipped or excluded, fix the cause, and retry. |
| Required date is missing | `dateMissing` | Domains, Invoices, Quotes, Transactions | The record carries no issue date, due date or other required date field. | The source record is incomplete. Update it on the source side and retry. |
| No department exists | `departmentNotFound` | Tickets | The ticket names a support department that does not exist here. | Create the matching department, then retry. |
| No download category exists | `downloadCategoryNotFound` | Downloads | The download category is missing from the source feed. | Check WHMCS: the category must exist and be exported. Fix the data in WHMCS and retry. |
| No client name or email to take one from | `nameMissing` | Clients | The client record has no first name and no email address to derive one from. | Update the source record and retry. |
| The billing cycle cannot be billed here | `billingCycleUnsupported` | Services, Orders | The service or order uses a billing cycle this version cannot bill. Billed cycles: Monthly, Quarterly, Semi-Annual, Annual, Biennial. | Use a supported billing cycle on the source side, or add the service manually. |
| The order has only domain or addon lines, with no product here | `noProductLines` | Orders | Every line in the order is a domain or a service addon; none matches a product here. | Nothing. Orders of this shape have no catalogue equivalent to import. |
| The currency is not a three-letter code | `currencyInvalid` | Transactions | The currency field is not a valid ISO 4217 code. | Correct the currency on the source side and retry. |
| Import failed | `importFailed` | All (counted as Failed) | An error occurred while saving this record. The detail is in the server log only. | Check the server log, fix the problem and retry the job. |

## Fixing skipped records

Most skip reasons have a clear fix:

1. Resolve the cause (create the missing department, fix the source record, etc.).
2. Run the job again, or create a new job for that entity type only.

Records that have a match key are skipped on the next run if they already exist, so only the records you fixed are created. See the retry notes in [Moving from WHMCS](./from-another-system).
