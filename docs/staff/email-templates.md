---
title: Email templates
---

# Email templates <Label type="admin" />

## How templates work

Each email template has a **slug** that identifies it in the code. One version is stored per language (English, Russian and Armenian). Some templates are created at start-up; others are created on first use. If a version does not exist for the client's language, the email is sent in English.

Creating a version for a slug and language that already exists returns `EMAIL_TEMPLATE_ALREADY_EXISTS`. A template that does not exist returns `EMAIL_TEMPLATE_NOT_FOUND`.

<Callout type="tip">
Templates you have edited are never overwritten on upgrade. Your changes are safe.
</Callout>

## The Email Templates screen

Go to **Settings → Email Templates**. Subtitle: "Email Templates."

**Columns:** Name, Subject, Languages, Edit.

## Edit a template

<Steps>
<li>

Find the template row and click **Edit**.

</li>
<li>

Select the language tab you want to edit. A tab marked as missing has no stored version; saving it creates one.

</li>
<li>

Edit the **Subject**, **Description** and **Body (HTML)**.

</li>
<li>

Click **Save**.

</li>
</Steps>

## Placeholders

Placeholders use the Liquid template language. A variable is written as `{{ name }}` or `{{ object.field }}`, with names in lowercase and underscores. Every template has access to the brand and site variables:

```liquid
{{ brand.primary }}       — primary brand colour
{{ brand.primary_dark }}  — dark variant of the primary colour
{{ brand.on_primary }}    — text colour on the primary background
{{ brand.accent }}        — accent colour
{{ brand.logo_url }}      — URL of the brand logo
{{ site.name }}           — the name of the site
```

Example using a condition and a loop:

```liquid
{% if invoice.total > 0 %}
  <p>Invoice #{{ invoice.id }} for {{ invoice.total }} {{ invoice.currency }}</p>
{% endif %}

{% for item in items %}
  <li>{{ item.name }}</li>
{% endfor %}
```

## Templates and their placeholders

### Account

<SettingTable :settings="[
  { name: 'welcome', type: 'string', default: '', description: 'Sent to a new client after account creation. Placeholders: client.email' },
  { name: 'user-invite', type: 'string', default: '', description: 'Sent when a user is invited to a client account. Placeholders: invite_link, client_name, first_name' },
  { name: 'user-email-verification', type: 'string', default: '', description: 'Local accounts only. Sent to verify a new email address. Placeholders: verification_link' },
  { name: 'user-password-reset', type: 'string', default: '', description: 'Local accounts only. Sent when a password reset is requested. Placeholders: reset_link' },
  { name: 'user-email-change', type: 'string', default: '', description: 'Local accounts only. Sent to confirm an email address change. Placeholders: confirmation_link' },
  { name: 'user-email-changed-notice', type: 'string', default: '', description: 'Local accounts only. Sent to the old address after an email address is changed. Placeholders: new_email' },
]" />

### Billing

<SettingTable :settings="[
  { name: 'invoice-created', type: 'string', default: '', description: 'Sent to the client when an invoice is created. Placeholders: invoice.id, invoice.total, invoice.currency' },
  { name: 'payment-received', type: 'string', default: '', description: 'Sent to the client when a payment is recorded. Placeholders: invoice.id, invoice.total, invoice.transaction_id' },
  { name: 'invoice-renewal-reminder', type: 'string', default: '', description: 'Sent when a renewal reminder is due. Placeholders: invoice.id, invoice.total, invoice.currency, invoice.due_date, invoice.url' },
  { name: 'gateway-payment-stalled-admin', type: 'string', default: '', description: 'Sent to staff when a gateway payment has been pending too long. Placeholders: invoice.id, client.id, module, pending_hours' },
  { name: 'gateway-payment-credited-admin', type: 'string', default: '', description: 'Sent to staff when a gateway payment took money the invoice could not record and the amount was added to the client credit balance or held for manual resolution. Placeholders: invoice.id, client.id, module, reference, amount, currency, credited' },
  { name: 'gateway-payment-without-reference-admin', type: 'string', default: '', description: 'Sent to staff when a gateway reports a payment as paid but sends no payment reference. Nothing is recorded; the operator must find and record it by hand. Placeholders: invoice.id, client.id, module, amount, currency' },
]" />

### Services

<SettingTable :settings="[
  { name: 'service-provisioned', type: 'string', default: '', description: 'Sent to the client when a service is provisioned. Placeholders: service.id, service.provisioning_ref, service.domain, service.username' },
  { name: 'service-termination-failed-admin', type: 'string', default: '', description: 'Sent to staff when service termination fails. Placeholders: service.id, reason' },
]" />

### Support tickets

<SettingTable :settings="[
  { name: 'ticket-created', type: 'string', default: '', description: 'Sent to the department when a ticket is opened. Placeholders: ticket.id, ticket.subject' },
  { name: 'ticket-replied', type: 'string', default: '', description: 'Sent to the client (staff reply) or department (client reply). Placeholders: ticket.id, ticket.subject' },
  { name: 'ticket-closed', type: 'string', default: '', description: 'Sent to the client when a ticket is closed. Placeholders: ticket.id, ticket.subject' },
]" />

### Domains

<SettingTable :settings="[
  { name: 'domain-registered', type: 'string', default: '', description: 'Sent to the client when a domain is registered. Placeholders: domain.id, domain.name, domain.registrar, domain.expires_at' },
  { name: 'domain-transferred', type: 'string', default: '', description: 'Sent to the client when a domain transfer completes. Placeholders: domain.id, domain.name, domain.registrar, domain.expires_at' },
  { name: 'domain-renewed', type: 'string', default: '', description: 'Sent to the client when a domain is renewed. Placeholders: domain.id, domain.name, domain.new_expires_at' },
  { name: 'domain-expiring', type: 'string', default: '', description: 'Sent as a domain approaches its expiry date. Placeholders: domain.id, domain.name, domain.expires_at, domain.days_until_expiry' },
  { name: 'domain-expired', type: 'string', default: '', description: 'Sent when a domain passes its expiry date. Placeholders: domain.id, domain.name, domain.linked_service_id' },
  { name: 'domain-registration-failed', type: 'string', default: '', description: 'Sent to the client when domain registration fails. Placeholders: domain.name, invoice.id' },
  { name: 'domain-registration-failed-admin', type: 'string', default: '', description: 'Sent to staff when domain registration fails. Placeholders: domain.name, client.id, invoice.id, reason' },
  { name: 'domain-renewal-failed-admin', type: 'string', default: '', description: 'Sent to staff when domain renewal fails. Placeholders: domain.name, client.id, invoice.id, reason' },
]" />

## Mail delivery

Templates are sent through the SMTP settings configured in the [Configuration reference](/operators/configuration).

Clients can see the emails sent to them in the **Emails** section of the client portal.
