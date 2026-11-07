---
title: Support tickets
description: "Work the Hostpanel ticket queue: departments, statuses and priorities, answering, closing, predefined replies and support reports."
keywords: "support tickets, ticket queue, departments, predefined replies, support reports"
---

# Support tickets <Label type="admin" />

## Departments

Four departments are created on a fresh install. Their contact addresses are read from these settings at first start:

<SettingTable :settings="[
  { name: 'DEPARTMENT_SUPPORT_EMAIL', type: 'string', default: 'support@example.com', description: 'Email address for the Technical Support department' },
  { name: 'DEPARTMENT_BILLING_EMAIL', type: 'string', default: 'billing@example.com', description: 'Email address for the Billing department' },
  { name: 'DEPARTMENT_SALES_EMAIL', type: 'string', default: 'sales@example.com', description: 'Email address for the Sales department' },
  { name: 'DEPARTMENT_GENERAL_EMAIL', type: 'string', default: 'hello@example.com', description: 'Email address for the General department' },
]" />

These settings are only read once, when the departments table is empty.

<Callout type="note">
There is no admin screen to create or edit departments yet. The API has endpoints for this. A ticket that references a department that no longer exists returns <code>DEPARTMENT_NOT_FOUND</code>.
</Callout>

## Ticket statuses and priorities

| Status | What it means |
|--------|---------------|
| Open | Ticket is open, waiting for a staff response |
| Awaiting Reply | Staff replied; waiting for the client |
| Closed | Ticket is closed |

**Priorities:** Low, Medium, High.

## The ticket list

Go to **Support → Support Tickets**. Subtitle: "Manage all client support requests."

**Filters:** All Active Tickets, Flagged, Open, Awaiting Reply, Closed.

**Search:** by ticket subject.

**Columns:** Subject, Department, Requestor, Status, Last Reply.

**Bulk actions** (under "With Selected:"): Close, Delete, Flag, Unflag.

<Callout type="warning">
Deleting a ticket cannot be undone.
</Callout>

A ticket that does not exist returns `TICKET_NOT_FOUND`.

## Answer a ticket

Click a ticket to open its detail page.

**Ticket Settings panel** (right side): Department, Priority, Status, Save Changes.

**Conversation** — the thread of messages between the client and staff.

<Steps>
<li>

Type a reply in the **Add Reply** field.

</li>
<li>

Click **Reply** to send it.

</li>
</Steps>

**Who receives email notifications:**

| Event | Recipient |
|-------|-----------|
| Ticket created | The department's email address (`ticket-created`) |
| Staff replies | The client (`ticket-replied`) |
| Client replies | The department's email address (`ticket-replied`) |
| Ticket closed | The client (`ticket-closed`) |

If the department has no email address configured, no notification is sent on ticket create or client reply.

## Close, reopen, delete

- **Close** — closes the ticket and emails the client. A closed ticket cannot receive replies (`TICKET_REPLY_NOT_ALLOWED`). Closing an already-closed ticket returns `TICKET_CLOSE_NOT_ALLOWED`.
- **Reopen** — moves a closed ticket back to Open.
- **Delete** — permanently deletes the ticket. This cannot be undone.

There is no automatic closing of old tickets.

## Open a ticket for a client

Go to **Support → Open New Ticket**.

<Steps>
<li>

Search for a client by name, email or ID and select them. **Name** and **Email Address** are filled from the client record.

</li>
<li>

Fill in **Subject**, **Department**, **Priority** and **Message**.

</li>
<li>

Click **Open Ticket**.

</li>
</Steps>

You can also open a ticket from a client's own **Tickets** tab.

## Predefined replies

Go to **Support → Predefined Replies**. Subtitle: "Manage predefined ticket reply templates."

**Categories** — organise replies into groups. Click **Add Category** and enter a Category Name. A category that has replies or sub-categories cannot be deleted (`PREDEFINED_REPLY_CATEGORY_IN_USE`). A predefined reply that does not exist returns `PREDEFINED_REPLY_NOT_FOUND`.

**Replies** — click **Add Predefined Reply** and fill in Name, Category and Content.

**Search** — search predefined replies by name or content.

## Support reports

Go to **Reports** and select the report you need:

| Report | What it answers |
|--------|----------------|
| Support Overview | Six metrics for a selected period: New Tickets, Client Replies, Staff Replies, Tickets Without Reply, Average First Response, and a breakdown of tickets submitted by hour |
| Ticket Tags Overview | Tag names and how many tickets carry each tag, for a date range |
| Ticket Feedback Scores | Average rating per staff member for a date range |
| Ticket Feedback Comments | Individual feedback comments submitted by clients |
| Support Ticket Replies | Breakdown of tickets handled per admin for a selected month |

## What is not built in

<Callout type="note">
Tickets are not created from incoming mail, and old tickets are not closed automatically.
</Callout>
