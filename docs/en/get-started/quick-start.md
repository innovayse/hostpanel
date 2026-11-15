---
title: Quick start
description: "Go from a bare Linux server to the first provisioned hosting service in Hostpanel: install, first administrator, server, product, payment method and a test order."
keywords: "quick start, install hostpanel, first service, provisioning, first order"
---

# Quick start

This page takes you from a bare Linux server to the first hosting account that Hostpanel creates for a customer. Each step is short and links to the full guide when you need the details.

## What you need

- A Linux server with Docker and the Compose v2 plugin, and two DNS host names that point at it: one for the client portal and one for the admin panel. The full list is in [Before you start](/operators/install#before-you-start).
- A control panel server to host the accounts, with its API credentials. The modules that create accounts automatically are cPanel, Plesk and CWP7, see [Servers and server groups](/servers/servers).
- An email address for the administrator account.

## 1. Install Hostpanel

Run the installer on the server:

```bash
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh | bash
```

It asks for the portal domain, the admin panel domain and the administrator email, starts the stack and waits for `/health`. At the end it prints both addresses and a one-time setup token. Keep the token for the next step. Flags, a setup behind your own TLS proxy and a manual docker compose install are in [Install Hostpanel](/operators/install).

## 2. Create the first administrator

Open the admin panel address. While nobody holds the Admin role, the **Initial setup** screen appears. Create your account, paste the setup token and click **Claim the administrator role**. If you lost the token, [First run](/operators/install#first-run) shows how to read it from the API log.

## 3. Set up mail

The installer leaves `SMTP_HOST` empty on purpose. Until you set a real relay in `.env` and restart the stack, no verification, password reset or invoice email is sent. See [Set up mail](/operators/install#set-up-mail).

## 4. Check the currency

Go to **Settings → Currencies**. Prices can only be looked up when a base currency is set up, otherwise the API answers `BASE_CURRENCY_NOT_CONFIGURED`. If the list has no base currency, add yours and make it the base. See [Currencies, renewals and jobs](/staff/currencies).

## 5. Add a server

<Steps>
<li>

Go to **Servers** and click **Add New Server**.

</li>
<li>

On the **General** tab enter the name, host name and IP address. On the **Details** tab choose the module and enter the credentials: for cPanel the WHM user and an API token, for CWP7 and Plesk the access hash.

</li>
<li>

Save, then click **Test Connection** on the server row. The badge must read **Online**.

</li>
</Steps>

For cPanel, first create the API token in WHM as described in [cPanel servers](/servers/cpanel). If you use more than one module, product orders need a server group, see [Server groups](/servers/servers#server-groups).

## 6. Create a product

Go to **Settings → Products → Create Product**. Choose the **Shared Hosting** type, a product group, a name and a slug. Set **Package Name** to the exact name of a package that exists on the server, and fill in the base currency price for at least one billing cycle. A blank price makes the product unsellable on that cycle. Leave **Hidden** off so customers can order it. The full form is described in [Products and pricing](/staff/products).

## 7. Turn on a payment method

Hostpanel ships with bank transfer, and every online gateway comes from a plugin. For a first test, bank transfer is enough. Go to **Integrations**, category **Payment Gateways**, click **Configure →** on **Bank Transfer**, fill in at least one of the account details, save and switch it on. Bank transfer is offered to customers only while it is on and has at least one detail filled in. Details and the plugin route are in [Payment gateways](/payments/gateways).

## 8. Place a test order

Open the client portal, order the product and check out with **Pay by bank transfer**, see [Ordering](/customers/ordering). Then, in the admin panel, open **Billing**, **Pending Bank Transfers** and click **Confirm**. The invoice is paid and the order is fulfilled.

## 9. Provision the service

A paid order creates a service in **Pending** status. Hostpanel does not create the hosting account at that moment. Do one of these:

- Main path: in the client portal, the customer clicks **Set Up My Service** after payment, or **Complete Setup** on the service later, and chooses the domain and credentials in the setup wizard, see [Ordering](/customers/ordering) and [Services](/customers/services). It is available after the first invoice is paid in full.
- In the admin panel, open the service and click **Create** in the **Module Commands** card. This works only once the service has a domain and a username. Without them the request is refused with `SERVICE_HOSTING_DETAILS_MISSING` (409), see [What happens on provisioning](/servers/provisioning#what-happens-on-provisioning).

When the panel confirms, the service becomes **Active** and the customer receives the `service-provisioned` email. If something fails, the error code shown is explained in [Provisioning](/servers/provisioning).

## Where to go next

- Moving existing customers over: [Migration checklist](/migration/fifteen-minutes).
- Back up the database and `.env` before you take real customers: [Back up and restore](/operators/backup).
- Day-to-day work: the [Staff guide](/staff/).
