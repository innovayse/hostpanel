---
title: Clients, users and contacts
---

# Clients, users and contacts <Label type="admin" />

## The client list

Open **Clients** from the main menu to see every client account.

**Filters:** Client / Company Name, Email Address, Phone Number, Status.

**Columns:** ID, Name, Email, Company, 2FA, Joined, Status. A **User Deleted** badge appears when the underlying user account has been removed.

**Client statuses**

| Status | What it blocks |
|--------|----------------|
| Active | Nothing blocked |
| Inactive | Cannot place new orders or log in |
| Suspended | Cannot place orders, pay online, or make account changes |
| Closed | Account is permanently closed; no actions are allowed |

## Add a client

Go to **Clients → Add New Client**.

<Steps>
<li>

**Account Owner** — choose **Create a new user** to create a brand-new login, or **Associate with an existing user** to link the client record to a user who already has an account.

</li>
<li>

**Profile Details** — first name, last name, company name, phone number.

</li>
<li>

**Address & Billing** — street address, city, state, postcode, country, **Currency** (cannot be changed once the client has been invoiced), **Payment Method**, and **Billing Contact**.

</li>
<li>

**Email Notifications** — choose which system emails this client receives.

</li>
<li>

**Settings** — tick the options that apply:

- **Overdue Notices** — send payment overdue emails
- **Tax Exempt** — no tax rate can be set on their invoices
- **Separate Invoices** — one invoice per order line
- **Disable CC Processing** — skip automatic card charges
- **Marketing Emails Opt-in** — include in marketing campaigns
- **Status Update** — notify the client of service status changes
- **Allow Single Sign-On** — allow SSO login for this account

</li>
<li>

**Admin Notes** — private notes visible only to staff.

</li>
<li>

Click **Create Client**.

</li>
</Steps>

<Callout type="warning">
Once a client has been invoiced, their currency cannot be changed (<code>CLIENT_CURRENCY_ALREADY_INVOICED</code>). Choose it carefully when creating the account.
</Callout>

## The client summary

Open a client and click **Summary**. The page shows:

- **Client Information** — contact and billing details at a glance.
- **Other Information** — account settings and notes.
- **Invoices / Billing** — invoice counters and financial totals: Gross Revenue, Client Expenses, Net Income, Credit Balance.
- **Products / Services** — active and past services.
- **Recent Emails** — last few emails sent to the client.
- **Admin Notes** — staff-only notes.
- **Current Quotes** — open quotes.

Quick links at the top right: **Create Invoice**, **Add Billable Item**, **Create New Quote**, **Open New Ticket**.

## Profile

Click **Profile** to edit the client's contact details, billing address, currency, payment method, and account settings. Click **Save Changes** to apply.

## Users of a client account

Click the **Users** tab. Each client account has one **Owner** and may have additional **Users**.

- **+ Add User** — choose **Search Existing** to link a user who already has a login, or **Invite by Email** to send an invitation.
- **Transfer Ownership** — pass the Owner role to another user on the account.
- **Remove** — remove a user from the account.

The owner cannot be removed until ownership is transferred to someone else (`CLIENT_OWNER_CANNOT_BE_REMOVED`). You cannot transfer ownership to someone who is already the owner (`CLIENT_TRANSFER_SAME_OWNER`).

## Contacts

Click the **Contacts** tab to manage additional email contacts for this client. Contacts receive copies of emails but cannot log in.

- **Add** — fill in the contact form and save.
- **Edit** — update an existing contact.
- **Delete** — remove the contact permanently.

Two contacts on the same client account cannot share an email address (`CONTACT_EMAIL_IN_USE`).

## All users

Go to **Clients → Manage Users** to see every user across all client accounts. From this screen you can:

- **Send Password Reset Email** — sends a reset link to the user's email address. <Label type="local" /> Local accounts only; not available for SSO users.
- **Change Password** — set a new password directly. <Label type="local" /> Local accounts only.

## Suspend, deactivate, and close

| Action | Allowed when | Error if not allowed |
|--------|-------------|----------------------|
| Suspend | Active or Inactive | `CLIENT_SUSPEND_NOT_ALLOWED` |
| Deactivate | Any status except Suspended and Closed (no-op on Inactive) | `CLIENT_DEACTIVATE_NOT_ALLOWED` |
| Activate | Inactive or Suspended | `CLIENT_ACTIVATE_NOT_ALLOWED` |
| Close | Active, Inactive, or Suspended | `CLIENT_CLOSE_NOT_ALLOWED` |

A closed account cannot be reopened through the admin panel (`CLIENT_ACTIVATE_NOT_ALLOWED`).
