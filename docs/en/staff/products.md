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

Type part of a name, SKU or slug in the search box to find a product. The **Filters** button narrows the list by group, type and status (active or hidden); applied filters show as chips you can remove. The list is paged, 20 products to a page, in the order you set with the sort order, and the search, the filters and the page stay in the address, so a link or a reload opens the same list. Search treats `%` and `_` as ordinary characters.

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

**Product Type**: choose the type card that matches the service.

</li>
<li>

**Product Group**: select the group this product belongs to. If the group does not exist yet, it can be created through the API for now (`PRODUCT_GROUP_NOT_FOUND`).

</li>
<li>

**Product Name**: the name shown to customers.

</li>
<li>

**Slug (URL)**: the URL path for the product page on the storefront (e.g. `/products/starter`).

</li>
<li>

**Package Name**: the hosting package used when an account is created on the server. Required for provisioned types; leave blank for Add-on, Digital and Other (`PRODUCT_PACKAGE_MISSING` if the provisioner expects it but it is missing).

</li>
<li>

**Server Group**: the group of servers the provisioner may use. "None (auto-select by module)" lets the module choose. See [Servers and provisioning](/servers/provisioning).

</li>
<li>

**Description**: a short description shown on the storefront.

</li>
<li>

**Sort order**: position inside the group, lowest first. Products with the same number are sorted by name.

</li>
<li>

**Recommended plan**: tick to show a "Recommended" badge on the storefront. Only one product per group can be recommended; saving this clears the badge from the others.

</li>
<li>

**SKU**: optional unique code used by fulfilment plugins to identify the product (letters, digits, hyphens, underscores or dots; at most 64 characters).

</li>
<li>

**Metadata**: optional JSON object passed to the fulfilment plugin (at most 16 KB).

</li>
<li>

**Grace period (days)**: days a service stays usable after its due date before it is suspended.

</li>
<li>

**Digital service**: turn it on for a product that counts as an electronic service for VAT (hosting, a licence, a domain that counts as one). A consumer in another EU member state is invoiced for such a product at the VAT rate of their own country; see [VAT rates and digital services](/staff/billing#vat-rates-and-digital-services). It is not the same as the product type Digital.

</li>
<li>

**Max active services**: how many services of the product may be live at once (not terminated). An order that is placed but not yet fulfilled holds a place too, and an unpaid order keeps holding it until it is cancelled, so cancel an order that will not be paid to give its place back. When every place is taken the product is sold out: it is left off the storefront and a new order is refused (`ORDER_PRODUCT_SOLD_OUT`). Leave the field empty or 0 for no limit. Lowering the cap below the number already taken leaves the existing services alone and only stops new orders until places are free.

</li>
<li>

**One per client**: a client who already has a live service of the product, or an open order for it, cannot order it again, and one order cannot name it twice (`ORDER_PRODUCT_ONE_PER_CLIENT`).

</li>
<li>

**Hidden**: tick to set the product Inactive and hide it from the order form. An inactive product cannot be ordered (`PRODUCT_NOT_AVAILABLE`).

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

There is no setup fee field. Clients billed in a non-base currency pay the price you enter for that currency directly, exchange rates are not applied to product prices.

**Update Product Prices** on the Currencies screen opens a picker where you choose which currencies to recalculate. The selected currencies have their product prices overwritten by converting from the base currency at the stored exchange rate.

## What you can change after creation

You can edit the name, description, slug, package name, sort order, recommended flag, hidden flag and pricing at any time. The product type and group cannot be changed after the product is created.

## The plan specification

In the product list, open the row actions menu for a product and click **Edit specification** to open the Specification dialog.

- **Add a line**: enter a Feature name and a value.
- **Remove a line**: click the remove button on the row.

Feature names line up across products in a comparison table, so use the same spelling for the same feature across products. A name can only appear once per product; a duplicate is refused (`PRODUCT_FEATURE_LABEL_DUPLICATE`). The comparison table on the storefront stays hidden until at least one product in the group has a specification line.

## Recommended and hidden products

- **Recommended**: only one product per group can carry the Recommended badge. Setting it on a product clears it from all others in the same group.
- **Hidden**: sets the product Inactive and removes it from the storefront order form. An inactive product cannot be ordered by anyone (`PRODUCT_NOT_AVAILABLE`).

## Product bundles

A **product bundle** sells several products together as one offer, for example a hosting plan with a mailbox, optionally with a domain registration, at a discount. The bundle is a recipe for an order: the client orders the bundle, and the order contains one line per product, plus a domain line when the bundle includes a domain.

Go to **Settings → Product bundles**. The list shows each bundle's name, its products, the included domain, the discount and its status.

### What a bundle is made of

- **Products**: one or more catalogue products. Exactly one is the **primary product**; the bundle needs one, and no more. A product can appear in a bundle only once. A domain product cannot be a component (`PRODUCT_BUNDLE_DOMAIN_PRODUCT_NOT_ALLOWED`).
- **Domain registration**: optional, and not a component. The price of a domain depends on the TLD the client picks, so the bundle records only that a registration is included and for how many **years** (1 to 10). The client chooses the domain name when ordering, and the registration period is always the bundle's: the client cannot shorten it or buy a longer one at the bundle price. The domain line is priced from the <a href="./tld-pricing">TLD pricing</a> when the order is placed.
- **Discount**: see below.

### Create a bundle

Go to **Settings → Product bundles → Create bundle** and fill in the form.

<Steps>
<li>

**Name** and **Description**: what the client sees for the bundle. The name is required.

</li>
<li>

**Sort order**: position in the list, lowest first.

</li>
<li>

**Products**: click **Add a product**, pick a product from the list and repeat for each product. Use the radio button on a row to mark the **primary product**. A product already picked on another row is not offered again.

</li>
<li>

**Includes a domain registration**: switch on to include a domain, then set the **Years of registration**.

</li>
<li>

**Discount**: choose a type and, unless it is "No discount", the amount.

</li>
<li>

Click **Save**. A new bundle is created active.

</li>
</Steps>

When you edit a bundle, an **Active** switch appears. Saving replaces the bundle's details and products. A refused save shows the reason in the dialog, for example `PRODUCT_BUNDLE_PRIMARY_INVALID` (not exactly one primary product), `PRODUCT_BUNDLE_ITEM_DUPLICATE` (a product twice), `PRODUCT_BUNDLE_ITEMS_REQUIRED` (no product), `PRODUCT_BUNDLE_DOMAIN_TERM_INVALID` (the years do not fit) or `PRODUCT_BUNDLE_DISCOUNT_INVALID` (the discount type, amount and currency do not agree).

### Discount types

| Type | Meaning |
|------|---------|
| No discount | The bundle costs what its parts cost |
| Percentage | A percentage from 0 to 100 |
| Fixed amount | A fixed sum in a currency you choose, the discount currency |

### How the discount is applied

- The discount is taken off the order lines when the order is placed, the domain line included, so the order, its invoice and the services it creates all carry the final price.
- A **percentage** comes off every line. A **fixed amount** is spread over the lines in proportion to their prices; the shares are rounded cumulatively to the currency's smallest unit, so the line discounts add up exactly to the total discount. The discount is capped at the subtotal, so a line never goes below zero. When the client is billed in a currency other than the discount currency, the fixed amount is converted at the stored exchange rate first.
- The bundle discount applies **on top of** the client group discount: the prices it works on already carry the group's discount, so a client in a discounted group keeps it inside a bundle.
- Tax is computed on the discounted line.
- Invoice lines name the bundle: each line's description ends with the bundle's name in brackets.

### Inactive bundles, deleting and orders

An inactive bundle is hidden from the storefront and cannot be ordered (`PRODUCT_BUNDLE_NOT_AVAILABLE`). Deleting a bundle does not touch the orders placed from it: they are kept and lose the link to the bundle. The order's detail screen shows the **Product bundle** the order was placed from, while the bundle still exists.

## Not available yet

**Service Addons** and **Promotions** are placeholder screens and are not functional.
