---
title: Products and pricing
description: "Create and price hosting products in Hostpanel: groups, types, billing cycles and prices per currency, plan specification and hidden products."
keywords: "products, product pricing, billing cycles, plan specification, product groups"
---

# Products and pricing <Label type="admin" />

<DemoLink path="/settings/products" />

Opening a product that does not exist returns `PRODUCT_NOT_FOUND`. Ordering a product that is not Active returns `PRODUCT_NOT_AVAILABLE`. If no base currency has been set up, price lookups return `BASE_CURRENCY_NOT_CONFIGURED`.

## How products are organised

Products are grouped under **product groups**. Each group has a name, a description and an Active/Inactive status. There is no screen to create or edit product groups yet; groups are created by the initial data migration and seeded on a fresh install.

Products inside a group appear together on the storefront.

## The Products screen

Go to **Settings → Products**. Subtitle: "Manage hosting plans and services available for purchase."

**Columns:** Name, Group, Type, Monthly price, Annual price (both in the base currency), a "+" count of other currencies the product is priced in, and Status.

## Product types

| Type | What it represents |
|------|--------------------|
| Shared Hosting | Shared web hosting plan |
| VPS | Virtual private server |
| Dedicated | Dedicated server |
| Email | Mailboxes and mail hosting |
| Add-on | An extra attached to another service |
| Backup | Backup storage and restore |
| Digital | A licence, credit pack or digital good delivered by a fulfilment plugin |
| Other | Any other or custom service |

<Callout type="note">
Domain and SSL are not offered as type cards on the create form. Domain pricing is configured on the <a href="./tld-pricing">TLD Pricing screen</a> and SSL products are created through the Other type.
</Callout>

## Create a product

Go to **Settings → Products → Create Product** and fill in the form.

<Steps>
<li>

**Product Type** — choose the type card that matches the service.

</li>
<li>

**Product Group** — select the group this product belongs to. If the group does not exist yet, it can be created through the API for now (`PRODUCT_GROUP_NOT_FOUND`).

</li>
<li>

**Product Name** — the name shown to customers.

</li>
<li>

**Slug (URL)** — the URL path for the product page on the storefront (e.g. `/products/starter`).

</li>
<li>

**Package Name** — the hosting package used when an account is created on the server. Required for provisioned types; leave blank for Add-on, Digital and Other (`PRODUCT_PACKAGE_MISSING` if the provisioner expects it but it is missing).

</li>
<li>

**Server Group** — the group of servers the provisioner may use. "None (auto-select by module)" lets the module choose. See [Servers and provisioning](/servers/provisioning).

</li>
<li>

**Description** — a short description shown on the storefront.

</li>
<li>

**Sort order** — position inside the group, lowest first. Products with the same number are sorted by name.

</li>
<li>

**Recommended plan** — tick to show a "Recommended" badge on the storefront. Only one product per group can be recommended; saving this clears the badge from the others.

</li>
<li>

**SKU** — optional unique code used by fulfilment plugins to identify the product (letters, digits, hyphens, underscores or dots; at most 64 characters).

</li>
<li>

**Metadata** — optional JSON object passed to the fulfilment plugin (at most 16 KB).

</li>
<li>

**Grace period (days)** — days a service stays usable after its due date before it is suspended.

</li>
<li>

**Hidden** — tick to set the product Inactive and hide it from the order form. An inactive product cannot be ordered (`PRODUCT_NOT_AVAILABLE`).

</li>
<li>

Click **Create Product**.

</li>
</Steps>

## Billing cycles and prices per currency

The pricing grid is part of the product form. It has one row per enabled currency. Each row shows:

| Column | Meaning |
|--------|---------|
| Enabled | Whether this currency is offered for this product |
| Currency | Currency code; the base currency is tagged "Base" |
| Monthly | Price per month |
| Quarterly | Price per three months |
| Semi-annual | Price per six months |
| Annual | Price per year |
| Biennial | Price per two years |

<Callout type="warning">
A blank price makes the product unsellable on that cycle in that currency. Attempting to order such a product returns <code>ORDER_PRODUCT_PRICE_UNAVAILABLE</code>.
</Callout>

There is no setup fee field. Clients billed in a non-base currency pay the price you enter for that currency directly — exchange rates are not applied to product prices.

**Update Product Prices** on the Currencies screen opens a picker where you choose which currencies to recalculate. The selected currencies have their product prices overwritten by converting from the base currency at the stored exchange rate.

## What you can change after creation

You can edit the name, description, slug, package name, sort order, recommended flag, hidden flag and pricing at any time. The product type and group cannot be changed after the product is created.

## The plan specification

In the product list, open the row actions menu for a product and click **Edit specification** to open the Specification dialog.

- **Add a line** — enter a Feature name and a value.
- **Remove a line** — click the remove button on the row.

Feature names line up across products in a comparison table, so use the same spelling for the same feature across products. A name can only appear once per product; a duplicate is refused (`PRODUCT_FEATURE_LABEL_DUPLICATE`). The comparison table on the storefront stays hidden until at least one product in the group has a specification line.

## Recommended and hidden products

- **Recommended** — only one product per group can carry the Recommended badge. Setting it on a product clears it from all others in the same group.
- **Hidden** — sets the product Inactive and removes it from the storefront order form. An inactive product cannot be ordered by anyone (`PRODUCT_NOT_AVAILABLE`).

## Not available yet

**Service Addons** and **Promotions** are placeholder screens and are not functional.
