---
title: Domain extensions and registrars
description: "Manage domain extensions in Hostpanel: configure registrars, add or import TLDs, keep cost prices current and choose a registrar per domain."
keywords: "tld pricing, domain extensions, registrars, tld import, domain pricing"
---

# Domain extensions and registrars <Label type="admin" />

<DemoLink path="/settings/tld-pricing" />

## Registrars

Hostpanel ships two working registrar providers. Configure them under **Integrations**, category **Domain Registrars**.

### General domain registrar

Handles registration and management of common international extensions (.com, .net, .org and others).

**Settings:**

| Field | Required | Secret |
|-------|----------|--------|
| API Key | Yes | Yes |
| API Username | Yes | No |
| Whitelisted Client IP | Yes | No |

All three fields must be filled before the integration can be enabled.

### Country-code domain registrar

Handles registration and management of `.am` country-code extensions.

**Settings:**

| Field | Required | Secret |
|-------|----------|--------|
| Account Email | Yes | No |
| Account Password | Yes | Yes |
| API URL | No | No |
| Test Mode | No | No |

Account Email and Password must be filled before the integration can be enabled. Use Test Mode during setup to avoid live registrations.

<Callout type="note">
The Integrations list also shows entries for other registrar providers that have no working implementation. Do not configure or rely on those entries.
</Callout>

<Callout type="danger">
Never put registrar credentials in a screenshot or shared document. API keys and passwords give full account access.
</Callout>

### Test the registrar connection

On the integrations page for a registrar that supports connection probing, click **Test Connection**. The **Connection Status** panel shows:

- **Connection OK**: credentials are valid and the registrar API is reachable.
- **Connection Failed**: check the credentials and the whitelisted IP address.

The panel also shows the **Endpoint** the client called and the **Round trip** time.

An unconfigured registrar returns `REGISTRAR_NOT_CONFIGURED` when a domain operation is attempted.

## The TLD Pricing screen

Go to **Settings → TLD Pricing**. Subtitle: "Configure domain extension pricing and providers."

**Columns:** TLD, Provider, Status, Cost (1yr), Sell (1yr), Margin, Categories, Last Synced.

Use the search and filter controls to find a specific extension.

Attempting to look up a TLD configuration that does not exist returns `TLD_CONFIG_NOT_FOUND`. Adding a TLD that already exists returns `TLD_CONFIG_ALREADY_EXISTS`. Selecting a registrar provider that the system does not recognise returns `TLD_REGISTRAR_MODULE_UNKNOWN`.

## Add a TLD

Click **Add TLD** and fill in the form.

**General Settings:**
- **TLD**: the extension without the leading dot (e.g. `com`, `net`, `am`).
- **Registrar Provider**: which registrar handles this extension.
- **Cost Currency**: the currency the registrar charges you in.
- **Sell Currency**: the currency you charge clients in.
- **Enabled**: tick to make this TLD available for purchase.
- **Sort Order**: position on the storefront, lowest first.
- **Categories**: comma-separated labels used as storefront filter tabs (e.g. `Popular,Business`).

**Pricing tables**: fill in **Registration Pricing**, **Transfer Pricing** and **Renewal Pricing**. Each table has one row per period (1 to 10 years) with:
- **Period**: number of years.
- **Cost**: what the registrar charges you.
- **Sell**: what you charge the client.
- **Margin**: calculated automatically.

Clients billed in a currency other than the sell currency pay the sell price converted using the stored exchange rates. An order with no matching pricing row returns `ORDER_DOMAIN_PRICING_INVALID`.

Click **Create TLD** to save.

## Import TLDs from a registrar

Click **Import** on the TLD Pricing screen and choose a registrar provider. The screen shows an importing state while the list is fetched from the registrar. When complete, it shows: "Import complete: {imported} imported, {updated} updated."

## Keep cost prices up to date

Click **Sync Prices** on the TLD Pricing screen to pull the current cost prices from all registrar providers.

<Callout type="note">
Sync Prices only changes cost prices. Sell prices are never changed by a sync.
</Callout>

A daily job runs at **04:00 UTC** by default and updates cost prices and the **Last Synced** timestamp for all TLDs automatically.

## Which registrar handles a domain

When a new domain is ordered, the system matches the TLD configuration with the longest matching suffix. For example, if both `am` and `co.am` are configured, a `.co.am` domain uses the `co.am` configuration. If no suffix matches, the default provider is used.

An existing domain keeps the registrar it was registered with, regardless of TLD configuration changes.

## Categories on the storefront

The comma-separated categories on each TLD become the **Browse extensions by category** filter tabs in the client portal order flow.
