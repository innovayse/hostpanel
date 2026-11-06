# Changelog

All notable changes to HostPanel are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Billing: credit notes. Staff can issue a credit note against a paid invoice from the new Credit notes tab of the invoice, for all of it or part, with the tax following the invoice at the same rate. The value goes either to the client's credit balance (a new credit history line, "credit note") or back through the payment gateway as one refund of the invoice, so the money moves once and is counted once in the reports. A note can never credit more than the invoice charged less every earlier refund and every earlier credit note, also when two are issued at the same moment, and it is refused on a credit top-up and on an unpaid invoice. A note is numbered `CN-0001` and so on from its own counter, is never deleted and can only be voided with a reason (a note paid out as a gateway refund cannot be voided). Staff get a Credit Notes list, a page per note and a PDF; the client gets a Credit notes page in the client area, a PDF in their language (English, Russian or Armenian) and an email when a note is issued. The numbering is a cautious default that has not been checked against any local law and cannot promise there are no gaps; an operator must check what their jurisdiction requires. There is no tax authority integration.

- Billing: client wallet. A client has a Credit page in the client area with their balance, the history of every movement of it and an Add funds page. Adding funds raises a one-line top-up invoice in the client's billing currency, between a minimum and a maximum set in System Settings (`billing.credit.topup_min`, `billing.credit.topup_max`), paid through a bank transfer or an installed gateway. The balance rises exactly once when the invoice is paid, however the payment is confirmed or replayed, and the client is emailed the new balance. A top-up fulfils nothing, takes no credit and is refunded by hand only, which takes the refunded credit off the balance. The client can also apply credit to their own invoice. Every movement of the balance is recorded on the client ledger with its type, the signed change and the balance it left (new movement, change and balance columns on `Transactions` and an `IsCreditTopUp` column on `Invoices`; older entries are not itemised), and staff get a Credit tab on the client profile with the history and add or deduct with a reason. Ledger entries of the history cannot be edited or deleted, and credit that covers a whole invoice now settles it and fulfils its order.

- Billing: manual bank transfer. A client can choose it on the checkout and invoice pages: the invoice moves to Payment Pending and shows the bank details (account holder, bank, IBAN, SWIFT, instructions) with copy buttons and a reference built from the invoice number, and the client is emailed the same details. Staff get a notice, and a Pending Bank Transfers page under Billing lists each transfer to confirm (the invoice is paid like any other payment, so the receipt, the ledger and the order fulfilment follow) or reject with a note the client reads (the invoice is open again). A second confirm of the same transfer is refused. The bank details, including the new SWIFT field, are edited in the Bank Transfer integration settings. Uploading a payment receipt is not part of this change.

- Admin panel: an Upcoming Renewals page under Billing lists the services and domains renewing in the next 30, 60 or 90 days. A row switches automatic renewal on or off and raises that client's renewal invoice by hand. A third list shows paid domain renewal lines left claimed after the registrar gave no clear answer; staff release one after checking the registrar, and the release is written to the client's activity log.

### Fixed

- Billing: raising renewal invoices for one client now runs under a per-client database lock, so the daily run and a staff click (or two instances) can no longer both pass the duplicate check and bill the same renewal twice.

- Client portal: choosing another account in the header account menu, or "Add another account", started the sign-in with the wrong application and callback, so the person landed on a page that does not exist and kept the old account. The header widget is now served from the portal itself, so both go through the portal's own sign-in and end back in the portal as the chosen account.
- Admin panel: the theme script in `index.html` was blocked by the Content-Security-Policy on every load. It is now a file served from the panel's own origin, and the policy stays free of `unsafe-inline` for scripts.
- Admin panel: when the identity provider signs a person out everywhere, the API must be registered with it as the receiver (`/api/auth/backchannel-logout`) for this client, otherwise the admin session outlives the sign-out (see the Docker README).
- Admin panel: the container refused to start with "DOCS_URL must be empty or an http(s) address" when `DOCS_URL` (or `CLIENT_BASE_URL`) was left empty, the value the settings document as allowed. An empty value is accepted again and a malformed one is still refused.
- Plugins: a plugin was skipped at start with "Entry point type not found" because the published image stamped the plugin SDK assembly with the image version (0.0.0.0 in a default build) instead of its own, so no plugin built against the SDK could load. The image version no longer reaches the SDK, and the log now says why an entry point type could not be loaded.

## [0.4.0] - 2026-11-05

Highlights since 0.3.1:

- Payment providers are external plugins now, built on `Hostpanel.Sdk` 3.1.0 and installed as signed packages. The core carries no card gateway, and no card plugin is publicly available yet (see the first upgrade note).
- Plugins can store customer credentials and the renewal run charges them. A plugin receives payment callbacks, ships its own icon, has its settings translated and offers a connection test.
- Admin client and service pages gained plugin widget slots (the widget body renderer is still to come), a plugin can import a service by its external id, and the admin client pages work on phones.
- Billing gained quarterly, semi-annual and biennial cycles and a recurring billing engine with overdue suspension. Invoices download as PDF.
- Single sign-on builds use the newer sign-on package (#822, #796).
- Documentation: an API reference, a guide to translating Hostpanel and working links between the pages.
- Plus 41 internal changes (continuous integration, tests, deploy tooling, branch syncs and fixes to card flows that this release removes) that are not listed below.

Upgrade notes, read before upgrading:

- The built in card gateways are removed in 0.4.0 and their plugins are not publicly available yet. If you take card payments, stay on 0.3.1 until a card plugin is available. Without one, no card payment method is offered at checkout. Bank transfer keeps working.
- Saved card customer references are kept. The migration moves them to the payment plugin id `stripe`, and they are used once a plugin with that id is installed. If your card plugin has another id, rewrite `GatewayModuleId` in `GatewayCustomers` to your plugin id. Until a matching plugin is installed, those clients are asked for their card again.
- Uploaded plugins must be signed packages, or an administrator must switch on "Allow unsigned plugins" (details below).
- Run `scripts/check-duplicate-gateway-payments.sh` before upgrading (details below). It works on a 0.3.x and on a 0.4.0 database.
- The database is renamed: every table, key, index and constraint is now PascalCase (columns already were), renamed in place with all data kept. Update your own SQL, reports and scripts to the new names. A dump taken before the upgrade restores only onto the previous version (#429, #430, #431, #438).
- Set the configuration that is now required before the first start: `ENCRYPTION_KEY` in every environment, `SESSION_ENCRYPTION_KEY` for the client app sessions, `EDGE_SCHEME`, the trusted proxy counts under `RateLimit`, and absolute `Cors:AllowedOrigins` entries. In Production the API refuses to start while a secret still holds a placeholder value (#571).
- After the upgrade, run the overdue invoice query from the rollout note below before the first 09:00 run, and decide which suspensions you do not want (#477).

### Added

- Signed plugin packages: an uploaded plugin carries a detached Ed25519 signature (`plugin.sig`), checked at install and at every start against the maintainers' keys and the keys an operator adds under `PluginSigning:OperatorKeys`. A production server refuses an unsigned, changed or unknown-key package unless an administrator turns on "Allow unsigned plugins" on the Plugins page (off by default, audited); Development still loads unsigned packages, and plugins shipped inside the server image are not affected. `backend/tools/Innovayse.Hostpanel.PluginSigner` signs a package (#638, #789, #847).
- Quarterly, semi-annual and biennial billing cycles beside monthly and annual: product prices per cycle in the admin form and list, the cycle choice on the storefront plan pages and at checkout, and renewal periods of 3, 6 and 24 months. Existing monthly and annual services, orders and plugin contracts are unchanged (#478, #722).
- Admin plugin slots: a plugin's widgets appear on the client summary page (`admin.client.panels`) and the service detail page (`admin.service.panels`). Each widget fails alone, with a neutral note, without breaking the page or the other widgets. An operator can bind a widget to products and hide it (#648, #703, #820, #823, #832, #843). The renderer of the widget body is still to come (#647).
- Client portal SSH keys: a "Deploy automatically from GitHub or GitLab" guide with a ready GitHub Actions workflow and GitLab CI job, filled in with the account's host, port and username (en, ru, hy) (#574).
- SSH key agent 1.2.0: optional jailed SSH enrolment (`jail_enrol`). The first key HostPanel adds jails the account (jailkit tree, home bind-mounted by a systemd unit, `sftp-ssh` group, bash) and removing the last managed key withdraws it; off by default (#572).
- `PanelNetwork:HostAddresses` (`PANEL_HOST_ADDRESSES`): reach a control panel's API over a VPN address while requests and TLS keep its hostname, so customer links and certificate checks are unchanged (#559).
- Storefront content: testimonials, partners, projects and FAQs, with English, Russian and Armenian translations, served from `/api/public/{testimonials,partners,projects,faqs}` (#315).
- Admin pages under "Storefront content" to create, edit and order the storefront content (#315).
- Storefront content ships empty: operators enter testimonials, partners, projects and FAQs in the admin; the repository carries no site content.
- SSH key management for hosting services: customers add and revoke ED25519 public keys for their hosting account from the client portal (Services → SSH Keys); HostPanel installs them on the account's server and never stores or accepts a private key. Off on every server until an administrator enables it (#496).
- SSH key agent for managed servers (`backend/agent/ssh-key-agent`): administrators connect a server from Servers → SSH with a per-server management key, a one-command installer whose download is checked against a SHA256 before it runs, a pinned host key and a test that must pass before the feature can be enabled. Profiles for CWP and plain Linux; see the agent README for the network requirements, including `PerSourcePenaltyExemptList` on OpenSSH 9.8 and later (#547).
- Suspending a service takes its SSH keys off the server and unsuspending puts the same keys back; terminating a service revokes them. Control panels suspend by locking the password, which does not stop a key from logging in, so suspend through HostPanel rather than in the panel directly (#547).
- Servers → SSH shows ready-made firewall lines (CSF, firewalld, ufw) and the sshd `PerSourcePenaltyExemptList` line that let only this HostPanel reach the agent port, once `SSH_AGENT_SOURCE_ADDRESSES` (`SshAgent__SourceAddresses`) names the address HostPanel connects from. A failed host key read now says whether the endpoint did not answer (network or VPN), refused the port (nothing listening or a rejecting firewall) or hung up before the SSH greeting (sshd turning HostPanel away), and shows it next to the button (#553, #556).
- Invoices can be downloaded as a PDF from the admin invoice page and the client portal, from `GET /api/billing/{id}/pdf` and `GET /api/me/invoices/{id}/pdf`; drafts are admin-only and stamped DRAFT. An invoice with more lines than `InvoicePdf__MaxLines` (500 by default) is refused, at most `InvoicePdf__MaxConcurrentRenders` (4 by default) render at once across the process, a download that finds no free render slot within `InvoicePdf__MaxRenderWaitSeconds` (10 by default) is answered with 503 and `Retry-After`, and downloads are sent with `Cache-Control: private, no-store`, through the client portal as well (#247, #472, #617, #660).
- Invoice PDFs print characters the bundled fonts lack, such as Arabic, Hebrew, CJK and emoji, as a replacement character, one per character, rather than as blanks (#541, #775).
- Digital products and external accounts: a product can be of the Digital type with a SKU, metadata and a grace period (admin product form and products API). Order and renewal lines carry a snapshot of the product, digital products skip provisioning and lapse with the subscription, staff can set or clear a client's external account, and the SKU is returned on service and invoice lines (#621, #629, #655, #659, #671, #681, #695, #697, #728, #729).
- Operators set a sort order and a featured flag on each product, and plans are ordered from Starter to Reseller (#386).
- Fulfilment plugins: plugins that fulfil a paid order are loaded and run after payment, recorded in the same save as the payment, can cap a refund before money moves, and staff see and retry fulfilment on an invoice (#622, #661, #666, #668, #669, #712, #763, #765).
- Identity plugins: the caller is resolved by an identity plugin first, plugin members reach the client portal and the order route, their client is created on the first order, and a failing identity plugin refuses cleanly (#665, #674, #709, #715, #747).
- Payment providers are external plugins on `Hostpanel.Sdk` 3.1.0: stored credentials charged for renewals, plugin callbacks, plugin icons, install from a registry, import of services by their external id, and settings that carry a translation and a connection test (#871, #774, #839, #870).
- Plugin runtime: manifest version 2 with hidden secret settings, calls routed through the host, a scoped read only host API and plugin data store, rate limits per plugin and caller, replies streamed as server sent events, a slot index, plugin pages and views, client plugin pages and slots, plugin SSH access, call timeouts, refused plugins and staged upgrades (#776, #781, #783, #784, #791, #801, #806, #813, #829, #830, #832, #843, #853).
- Plesk servers: hosting accounts are provisioned on them, a product with no server group goes onto any supported panel, and Login to Control Panel works only on an active service and for staff allowed to use it (#714, #718, #719, #732, #739, #773).
- Staff can add a service for a hosting account that already exists (#627).
- Admin panel: light, dark and system theme, a notice when a newer Hostpanel release is available, tables left aligned with row actions in a menu, and client pages that work on a phone (#329, #616, #620, #741).
- Storefront: a new logo, the classic template as the default and its redesign, home sections with toggles and a Locations entity, a configurable contact email and phone, header account menu and icons, and a Knowledge base footer link (#316, #348, #350, #365, #367, #386, #389, #394, #398, #400, #414, #416, #419, #457).
- CANCELLED and REFUNDED stamps on invoice PDFs (#554).
- Documentation website: install, upgrade, backup and rollback, configuration reference, guides for staff and customers, products and pricing, support desk, servers and SSH key agent, payment gateways and plugins, migration guide, architecture and local development, translating Hostpanel, an API reference generated from the OpenAPI document, and links between the docs, the demo and the app (#680, #799, #800, #804, #805, #807, #815, #817, #819, #833, #835, #838, #849, #850, #869, #872). The README is prepared for the community launch (#837).

### Changed

- Upgrade note: Saved card customers from earlier versions are copied to the new per-gateway table as customers of the plugin with the id `stripe`, because the only gateway that ever wrote them was that one. No card plugin is publicly available yet, so these references are kept and used once a plugin with that id is installed. If your card gateway plugin uses another id, install the plugin as `stripe`, or rewrite `GatewayModuleId` in `GatewayCustomers` to your plugin id before clients pay with a saved card; otherwise those clients are asked for their card again. The plugin assembly conflict check (`PLUGIN_ASSEMBLY_CONFLICT`) applies to every DLL in a package, shared third-party libraries included, so two plugins cannot ship a library of the same file name (#871).
- Single sign-on builds use the newer sign-on package. The first update signs every admin panel user out once, because the package now scopes its session cache keys per product. A sign-in callback with no matching attempt (a second tab, Back after signing in) takes a signed-in person to the dashboard and anyone else to the sign-in page instead of answering an error body, and two sign-ins started in one browser no longer spend each other (#822, #796).

- Before upgrading a production server that has uploaded plugins: plugins in the upload folder carry no signature yet, so after this release they are skipped at start (the log names each one) until the plugin is replaced by a signed package, or an administrator switches on "Allow unsigned plugins" on the Plugins page and the server is restarted. Plugins shipped inside the server image keep loading (#638, #789).
- Ledger transactions record where they came from (gateway payment, gateway refund, manual entry or import), and a gateway payment can be recorded only once per payment method and reference. Manual entries and imports may still share a reference. Before upgrading, `scripts/check-duplicate-gateway-payments.sh` lists existing gateway payment rows that share a reference; the upgrade stops and names them if any are left (#754).
- The plugin SDK is `Hostpanel.Sdk`, renamed from `Innovayse.SDK`: the package id, the assembly and every namespace (`Hostpanel.Sdk.Plugins.*`, `Hostpanel.Sdk.Base`) changed. A plugin rebuilds against the new package and updates its `using` lines; the host still refuses a package that ships either `Hostpanel.Sdk.dll` or the old `Innovayse.SDK.dll`. It is MIT licensed, so a plugin may be free or paid, open or closed source, while the rest of the repository stays AGPL-3.0-or-later. It lives in its own repository, [innovayse/hostpanel-sdk](https://github.com/innovayse/hostpanel-sdk), with its history, and is included here as a git submodule at `backend/src/Hostpanel.Sdk`. Clone with `git clone --recurse-submodules`, or run `git submodule update --init` after a plain clone. The package is built and published from that repository, ships with its license text and a readme that shows how to pin a version and write a minimal plugin, and this repository no longer publishes it. Stored plugin settings and data are unchanged (#623, #625, #846).
- Gateway payment reconciliation no longer gives up on a payment that is still pending after 24 hours. It asks the gateway again with a doubling wait (5 minutes at first, at most 6 hours, set in `ScheduledJobs`) until the gateway gives a final answer, and staff are mailed once per payment when it stays pending for 24 hours (#571).
- Automatic termination of a long suspension waits `billing.renewal.terminate_after_suspended_days` (default 14) instead of a fixed 14 days, and staff are mailed when the hosting account cannot be removed; the service stays suspended and the next run retries it (#571).
- Recurring jobs may run as long as their lease (`ScheduledJobs:LeaseMinutes`) instead of being cancelled after one minute (#571).
- A production host loads message handler code generated when the image is built instead of compiling every handler on its first use, which keeps the API's memory flat after a restart (#589, #592).
- A client no longer reads an invoice that is still a draft through any invoice route; it answers like an invoice that does not exist (#571).
- Billing cycles and domain actions are enums end to end. The API sends and accepts the billing cycle of a service or order line as `Monthly` or `Annual` (the old `annually` spelling is no longer accepted on the wire) and a domain action as `Register`, `Transfer` or `Renew`; stored values written in the older spellings are still read, and the release rewrites them to `Monthly` or `Annual`. The TLD price table now lists its extensions as an array of entries that each carry their `tld`. Importing a service or order that names a cycle other than monthly or annual is skipped and logged instead of being stored on a guessed cycle (#571).
- Recurring billing engine: a renewal invoice is raised 14 days before a service or domain falls due (`billing.renewal.invoice_days_before`), reminders go out 7, 3 and 1 days before (`billing.renewal.reminder_days`), and paying the invoice is what extends the service or renews the domain, exactly once. Credit is applied to renewal invoices automatically (`billing.renewal.auto_apply_credit`). The next period is raised only after the previous one is paid, at most once per daily run; a period that already started (a back-dated renewal date) gets the normal payment window, due 14 days after it is raised, instead of being born overdue (#477).
- Overdue suspension: any overdue invoice, renewal or not, gets a grace period after its due date (`billing.renewal.suspend_after_overdue_days`, default 3), then only the services on its lines are suspended. An overdue invoice with no service lines now suspends nothing (it used to suspend every service of the client). Paying lifts a suspension made for an overdue invoice once no other overdue invoice covers the service, and the daily renewal run lifts any that were missed; a suspension made by an operator, or for an expired domain, is never lifted by a payment (#477).
- An operator who unsuspends a service that is still on an invoice overdue past the grace sees it suspended again at the next 09:00 run, unless the service has `OverrideAutoSuspend` set or a `SuspendUntil` date in the future (#477).
- Cancelling a renewal invoice does not stop the renewal: the period is invoiced again on the next daily run while it is due. To stop a service renewing, set `AutoTerminateEndOfCycle` or open a cancellation request until the admin auto-renew toggle lands. A service skipped that way whose renewal date has already passed is running unbilled; a Warning says so on every run, and one Warning per run counts auto-renewing services that have no renewal date at all (`NextRenewalAt` empty) and are never billed (#477).
- Domains are no longer renewed for free: auto-renewal now bills the client, and the registrar is called only once the invoice is paid. A failed or unconfirmed registrar renewal alerts staff (`Notifications__AdminEmail`, falling back to `Notifications__ContactEmail`). A client renew order takes the domain off any open renewal invoice first, returning that invoice's credit; an overdue invoice that bills the domain with other items, or a renewal invoice with a payment in progress (PaymentPending) or in Collections, has to be settled first (`RENEWAL_INVOICE_OPEN`, 409). While that renew order is unpaid the daily run does not bill the domain again. An auto-renewing domain still gets the expiry warnings unless its renewal invoice is open and renewal reminders are on (#477, #488).
- Rollout note: on first deploy, (1) any service whose `NextRenewalAt` is already in the past gets a renewal invoice immediately, due 14 days later; (2) at the first 09:00 run every Active service on an invoice, including invoices raised before this release, that is already overdue by more than 3 days is suspended. Run the query below after the upgrade and before the first 09:00 run to list them, and set `OverrideAutoSuspend`/`SuspendUntil` or settle the invoices where a suspension is not wanted (#477):

  ```sql
  SELECT i."Id" AS "InvoiceId", i."ClientId", i."DueDate", it."ClientServiceId", s."OverrideAutoSuspend", s."SuspendUntil"
  FROM "Invoices" i
  JOIN "InvoiceItems" it ON it."InvoiceId" = i."Id"
  JOIN "ClientServices" s ON s."Id" = it."ClientServiceId"
  WHERE i."Status" = 'Overdue'
    AND i."DueDate" < now() - interval '3 days'
    AND s."Status" = 'Active'
  ORDER BY i."ClientId", i."Id";
  ```

  Set `Notifications__AdminEmail` (or rely on `Notifications__ContactEmail`) so renewal alerts reach a person.

  Services suspended before this release have no recorded suspension source, so no payment lifts them automatically (only an overdue suspension is lifted by payment). Review them by hand after deploying (`SELECT "Id", "ClientId" FROM "ClientServices" WHERE "Status" = 'Suspended' AND "SuspensionSource" IS NULL;`). Renewing a domain still lifts the suspension of its linked hosting service, as before. `billing.renewal.invoice_days_before` must be 1 to 90; a stored 0 is read as the default 14, with a warning in the log.
- Hero stats and badge, contact channels and the site name are read from settings (`portal.stats.*`, `portal.hero.badge`, `portal.contact.*`, `portal.site.name`); sections with no content are hidden (#315, #331).
- The demo site name is "Hostpanel Demo" (#331).
- The fallback site name is "Hostpanel"; no operator brand is hard-coded (#331).
- Orders, quotes and transactions now record the currency their amounts are in, and the admin lists show each amount with it (#461, #463).
- The admin transactions totals add every currency up in the base currency and no longer show 0 across all clients (#461, #463).
- API changes integrators must follow: (#571)
  - Paged lists answer `{ items, pageNumber, pageSize, totalCount, totalPages }`.
  - The liveness probe is `GET /health` (it was `/api/health`).
  - `GET /api/products` and `GET /api/products/groups` are for admins only. The public catalog is `GET /api/public/products` and `GET /api/public/product-groups`.
  - A client edits its own profile with `PATCH /api/clients/me`, which takes only the fields a client may change.
  - The knowledge base article list is paged, and its categories have their own endpoint.
  - The admin service detail is saved through six separate `PUT` endpoints, one per section, instead of one large update.
  - A refund takes a `refundType` value from a fixed list and no longer takes a `transactionId`.
  - A 429 answer carries the standard error body (`HOSTPANEL_RATE_LIMITED`), like every other error.
  - New error codes, among them `INVOICE_HAS_PAYMENTS`, `INVOICE_PAYMENT_EXCEEDS_DUE`, `INVOICE_CREDIT_EXCEEDS_DUE`, `INVOICE_AMOUNT_STILL_DUE`, `INVOICE_REFUND_EXCEEDS_PAID`, `INVOICE_REFUND_CONFLICT`, `INVOICE_NOT_DELETABLE`, `TRANSACTION_AMOUNT_REQUIRED`, `SERVICE_STATUS_CHANGE_NOT_ALLOWED`, `SERVICE_AUTOMATION_INCONSISTENT`, `SERVICE_NOT_TERMINATED`, `ORDER_PAYMENT_METHOD_UNAVAILABLE`, `GATEWAY_PAYMENT_MISMATCH`, `GATEWAY_REFUND_REFUSED` and the `REGISTRAR_*` codes.
- An invoice that already has a payment recorded can no longer be cancelled (`INVOICE_HAS_PAYMENTS`) (#571).
- A payment or credit larger than what the invoice still owes is refused (#571).
- Marking an invoice paid is allowed only when nothing is left to pay (`INVOICE_AMOUNT_STILL_DUE`); otherwise record the payment (#571).
- Every `Cors__AllowedOrigins__N` entry must be an absolute origin such as `https://host.example.com`, or the API refuses to start (#571).
- The anonymous order endpoints are rate limited. The limits are set under `RateLimit` (see [docker/README.md](docker/README.md)) (#571).
- Accepting an order (`POST /api/orders/{id}/accept`) answers 200 with the lines that could not be fulfilled, so staff see what still needs attention (#571).
- Deleting or cancelling an order also cancels the order's open invoice (#571).
- Service status changes follow a fixed table: only an Active service can be suspended, only a Suspended service can be unsuspended, and a Terminated service cannot be terminated again. Each change asks the hosting panel exactly once, and when the panel refuses the API answers 502 and the service keeps its status (#571).
- A service billed by hand (`AutoRenew` off) is never suspended or terminated automatically (#571).
- A new order's service is set up only after its first invoice is paid (#571).
- Imported services and domains send no notifications (#571).
- An account is locked for a while after repeated failed sign-ins, and sign-in answers 423 while it is locked (#571).
- Forgot password always answers 200, whether or not the address belongs to an account (#571).
- Trusted proxies are configured under `RateLimit` with `KnownProxies` and `KnownNetworks`, and `TrustedProxyCount` defaults to 0, so a forwarded client address is believed only from a proxy you list (#571).
- Sign-on tokens are accepted only when they were issued to the configured client (#571).
- The setup token is required in every mode, and the setup status no longer returns `tokenRequired` (#571).
- Every gateway payment carries an attempt id, so a retried or duplicated callback is matched to the right attempt (#571).
- Admin API shapes are nested: servers, domains, services, invoices, tickets, slides, reports and clients answer and accept grouped objects instead of flat records. Integrators using the admin API must follow the new shapes in the API reference (#571).
- Account switching stays a `GET` request (#571).
- Configuration that must now be set: `ENCRYPTION_KEY` in every environment, `SESSION_ENCRYPTION_KEY` for the client app sessions, the trusted proxy counts under `RateLimit`, `EDGE_SCHEME`, and absolute `Cors:AllowedOrigins` entries, which also decide which return URLs a payment may come back to. In Production the API refuses to start while a secret still holds a placeholder value (#571).
- The Reseller role no longer reaches staff routes (#571).
- A client's status is enforced: a client that is not active is refused where the action needs an active account (#571).
- Email templates are kept per language, and an email is sent in the recipient's language when a template exists for it (#571).
- Migration tool: the import now expects the updated source addon, so operators must update the addon on the source system before running a new import (#571).
- Migration tool: the source addon reads product prices from the source's pricing table in its default currency, and a cycle the source does not sell is imported without a price instead of at zero. An order's lines are read from the services, service addons and domains placed with it (#571).
- Migration tool: the source addon download works in the published image. The build ships the addon beside the API, and `MigrationPlugin__Path` can point the download at another folder; a missing folder answers `MIGRATION_PLUGIN_NOT_FOUND` (404) (#571).
- Messaging is durable: Wolverine uses the PostgreSQL outbox, so events survive a restart (#824).
- Production containers have memory and CPU limits and rotated logs (#664).
- Sign-in is stricter: each sign-in attempt keeps its own cookie, the sign-in sends a nonce and checks the issuer and ID token, and an issuer with a trailing slash is accepted (#711, #797, #812, #816).
- Currency rules: a client's currency can change once every invoice is cancelled but not while the client holds credit, only three letter currency codes are accepted, quotes round lines and totals to the currency minor units, and client summaries, billable items and the uninvoiced total show amounts in their own currency (#683, #734, #749, #751, #767, #769, #770, #772).
- A partially refunded invoice keeps its own state (#618). Free account services are moved to the monthly cycle (#586).
- The client area respects purchase permissions (#715), and the client dashboard counts every unclosed ticket (#688).
- Plugin neutrality: the core is kept free of plugin specific namespaces by an architecture test, and secrets stay out of the text form of every record (#685, #839, #836).
- The database is renamed: every table, key, index and constraint is now PascalCase (columns already were), renamed in place with all data kept. Update your own SQL, reports and scripts. A dump taken before the upgrade restores only onto the previous version (#429, #430, #431, #438).
- Demo mode seeds a fixed, maximal sample data set that is reset on schedule (#374).
- The client sign-on secret has one variable name: `CLIENT_HOSTPANEL_SECRET` feeds both the API and the client app, and `SSO_CLIENT_SECRET` is no longer used (#414).
- The font licence files for invoice PDFs ship in the image (#839).

### Removed

- The built-in embedded card flow: `POST /api/orders/{id}/create-payment-intent`, `POST /api/orders/{id}/confirm-payment`, `POST /api/me/invoices/{id}/pay` and the `api/me/payment-methods` endpoints, with the on-site card gateway port behind them. Card payments start through the redirect flow (`gateway-payment/start`), and methods stored for later charges go through the payment plugin's stored credential capability (`api/me/stored-payment-methods`). A gateway refund of an old ledger row recorded as a card payment with no hosted session behind it answers `INVOICE_NOT_GATEWAY_PAID`; refund it with a manual refund (#871).
- The built-in Stripe and Inecobank payment providers. HostPanel no longer carries a card or bank gateway in the core: the Stripe SDK, the `STRIPE_PUBLISHABLE_KEY` setting (and its `NUXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` mapping in the compose files), the catalogue entries, the `Innovayse.Providers.Inecobank` project and its tests are gone, and the default Content-Security-Policy no longer lists `js.stripe.com`, `api.stripe.com` or `m.stripe.network`. Card payments now come only from external payment plugins, and none is publicly available yet: an operator who takes card payments should stay on 0.3.1 until one is. When a card plugin is installed, add any script or connect origin its checkout needs to `EDGE_CLIENT_CSP_SOURCES`. Until a payment plugin is installed, card payments answer as not configured and only the manual bank transfer method stays available. Stored plugin settings and payment rows are untouched, and the plugins read them under the same slugs (#871).
- The sample storefront content that came with the repository (#315).
- `POST /api/clients`. Clients are created through registration (#571).
- The slide image upload endpoint (#571).

### Fixed

- Server selection follows the server group fill type: least full picks the lowest fill ratio, fill until full fills servers in name order (#839).
- The SSH key management dialog in the admin no longer answers 500 (#582).
- The phone country picker is styled and its search box starts empty (#469).
- The client area no longer logs a console error on the tickets page for a staff sign in (#438).
- Offline CC in the admin processes a payment and links to the invoice (#839).
- Retrying a migration import no longer duplicates announcements, network issues, downloads and ticket replies (#839).
- A quote total is the sum of the discounted lines rounded once to the currency minor unit, the same order an invoice uses, so two lines of 0.004 total 0.01. A migration recalculates the totals of quotes saved before (#751, #755).
- Accounts that staff link to a client (creating the client for an existing account, adding a user, transferring ownership) are now granted the Client role; before, the portal answered 403 to them and showed nothing. A one-time migration (`GrantClientRoleToLinkedUsers`) grants the role to accounts already linked to a client that lack it. It only adds rows, skips staff accounts (Admin, Reseller), and is not reversed on rollback (#705).
- A signed-in client whose currency differs from the base currency saw the storefront's prices (domain search, domain table, hosting plans, trials) in the base currency after a full page load: the server render, which does not know the client, was kept in the browser. Those reads are now keyed by the payer's currency, so the browser reads them again in the client's own (#662, #667).
- SSH key agent 1.2.2: jails also get `rsync`, `tar`, `gzip` and `unzip`, so a CI job can deploy a site over SSH (#573).
- SSH key agent 1.2.1: jails get `uname`, `id`, `hostname`, `dircolors`, `/etc/bashrc` and `/etc/profile.d/colorls.sh` (`jail_extras`), so customer logins no longer print `/usr/bin/uname: No such file or directory` and `ll` works; existing jails get them on the next key (#573).
- The client setup wizard now provisions on a server from the product's server group; it picked from every server of the module, so a product tied to one server was set up on the default one (#567).
- A signed-in customer's first order now grants them the Client role. An SSO sign-up held none, so after ordering every portal page answered 403 and the portal called them "not a customer account"; an email sent as the display name is no longer stored as their first and last name (#565, #569).
- Free orders ($0 total) are settled at checkout and fulfilled at once; they used to wait on a payment that never came, so their services were never created and checkout still asked for a payment (#563).
- A CWP7 connection test refused by the panel (wrong API key, address not allowed) now logs CWP7's reason instead of a JSON conversion error (#561).
- A partial payment no longer marks an invoice Paid. The invoice stays Unpaid (or Overdue) and takes further payments until they cover the total; only then is it Paid and are its order fulfilled, renewals applied and receipt sent. Every way of paying the rest (hosted gateway, bulk mark-paid) now charges what is still due, never the whole total again, and the invoice API sends `amountDue`, which the client portal shows on its pay button and payment page. An invoice whose payments end up covering it after a credit or a removed line is settled at once; marking a paid invoice unpaid records a reversal so the whole total is due again. Payments above what is due settle the invoice and are not turned into credit automatically. Two payments saved at the same moment that together cover an invoice are settled by the next daily renewals run (#540).
- Recurring jobs no longer stop for good after one failed run. A handler that threw had the next run it scheduled discarded, even from a `finally`, so a single database outage ended the daily chain of renewals, overdue suspension, reminders, gateway reconciliation and every other job until the next restart. Every job now logs a failed or timed-out run and schedules the next one regardless. The domain expiry check, which ran only once after each start, now runs daily, so expired domains are marked and expiry warnings are sent again. An admin's TLD "sync now" no longer adds another daily sync chain per click (#498).
- Client portal sessions no longer end when the access token expires. The tokens are kept on the server, in the Redis the stack already runs (`NUXT_SESSION_REDIS`, the same value as `REDIS_CONNECTION`), and the browser holds one small opaque, httpOnly session cookie. A refresh token larger than the 4 KB a browser keeps per cookie used to be dropped silently, which signed people out after about fifteen minutes. Signing out deletes the session on the server, and a back-channel logout ends every portal session of the person from the API (`PortalSessions__RedisConnection`). People signed in before the upgrade sign in once more (#495).
- A failed silent sign-in now shows a sign-in prompt on the page instead of a plain guest page, and an `authuser` address on a link is passed to the identity provider as `login_hint` (#495).
- A paid domain registration or transfer is no longer faked or crashed when the registrar is not configured or refuses it. Name.am and Namecheap now answer an unconfigured integration with a failure for every purchase and every change to a registrar-held domain (nameservers, lock, WHOIS privacy, DNS records, contacts, auto-renew), so a paid order is refunded automatically and a change is not saved when the registrar never made it. Domains with no registrar reference are still managed locally, except their contacts, which live only at the registrar and are refused (`DOMAIN_NO_REGISTRAR_REF`). A Namecheap `ERROR` answer to a registration, transfer or renewal is a clean refusal, so a paid renewal is retried rather than parked as an unknown outcome, and only `Registered="true"`/`Transfer="true"` count as confirmed. A Name.am 4xx to a registration or transfer is a refusal as well (#492).
- Registrar purchases are safer to retry and no longer send blank or leaked data. A registration or transfer that a registrar refuses as "domain unavailable" is first checked against the account, and counts as placed when the account already holds the name (or has the transfer in progress), so a retry after a timeout no longer refunds a domain we own. A refused purchase now says why: `REGISTRAR_DOMAIN_UNAVAILABLE` (409) or `REGISTRAR_INSUFFICIENT_FUNDS` (503), and an incomplete registrant contact is refused before anything is ordered (`REGISTRAR_CONTACT_VALIDATION_FAILED`, 422). Registrations and transfers now carry the client's own details as the registrant instead of an empty contact. Namecheap commands are sent as a POST form, so the API key and account name never appear in a URL. A Name.am answer counts as a purchase only when it is a list of purchased items, renewals use the renew plan, and a Name.am access token is kept once per connection instead of once per request. Hosts, lock, privacy, DNS, contact and nameserver changes that fail at either registrar now answer with a code instead of an error carrying the registrar's text, and the account e-mail is no longer logged at login (#492).
- A domain renewal no longer reports success when nothing was renewed at the registrar. An unconfigured Name.am or Namecheap integration, a Name.am error response (an error body, an unrecognised body or a 4xx), or a Namecheap answer without `Renew="true"` now fails the renewal, so a paid renewal stays unapplied, is retried daily and alerts staff. A domain with no registrar reference is refused (`DOMAIN_NO_REGISTRAR_REF`) instead of being extended locally; a paid renewal of such a domain is closed and staff are alerted to review a refund. When the registrar reports no new expiry, the current expiry is extended, so an early renewal keeps its remaining days (#488).
- The admin menu no longer "thinks" before a page opens on a slow connection. The admin image now compresses its bundle and lets browsers keep content-hashed assets for good; a screen's code starts downloading when the pointer reaches its link (and, on a cold page load, in parallel with the session check, which itself now asks the mode and the session together); and a thin progress bar shows at the top as soon as a navigation has been waiting for its screen (#359).
- A signed-in caller's first order no longer fails with a server error when it races the client record created on registration; the record is now saved once and the order continues with it (#304).
- The billing toggle is fixed (#332).
- Order numbers are issued from a database sequence (#307).
- The client UiButton no longer animates its outline (#341).
- The entrance reveal on the storefront is staggered with Tailwind classes (#376).
- The admin checks the redirect target after sign in without a control character pattern (#361).
- Admin lists and forms: tickets reload on every visit, ticked invoices and billable items clear when the list reloads or a filter changes, the Renew Domain action is locked while a domain is pending, the income forecast is right for the first months of the year, line up of invoice summary values, invoice detail currency formatting and profile form field errors (#737, #687, #740, #689, #686, #672, #670, #677).
- Invoice edits save, and two factor sign-in is hardened (#473). Quotes keep line discounts when they become an invoice (#738).
- A replayed gateway payment is never refunded, staff hear about credited gateway payments, and staff are alerted once when a gateway reports a payment paid without a reference (#730, #736).
- Admin sign-in returns to the requested page, tab bars show no stray scrollbar, and views are polished in both themes (#358, #583, #355). A signed-in person is let through when a sign-in callback has no matching attempt (#796).
- Storefront and demo fixes: the classic light and dark themes, legal pages, the catalogue, the home page family card on the production build, the domain check on the production site, the hosting page error state, header active item and contact settings, demo data and palette, billing toggle, mobile drawer and widget polling (#344, #346, #349, #354, #352, #356, #403, #421, #430, #432, #434, #443, #450, #438, #380).

### Security

- Integration secrets are no longer sent to the browser in plain text: secret settings and integration fields are write only, and the API says only whether a value is set (#430).
- Saving integration settings accepts only that integration's own declared keys, and each value is checked against its type (#839).
- A refused client plugin call no longer reveals which plugins are installed: an unknown plugin, a disabled one, an undeclared route and a missing permission all answer the same 404 (#829).
- The demo network is pinned to a fixed address range, and the demo cannot reach production services (#309, #311).
- General security hardening across the API, the client portal and the admin panel (#571).
- Several authorization, input validation and session handling issues were fixed across the API and both web apps. Upgrading is recommended (#571).
- The client portal now enforces the permissions of linked users on the server: every portal action requires the permission it belongs to, the account owner keeps full access, and a refusal is a 403 with `CLIENT_PERMISSION_DENIED` (#467). The portal reads the real permissions and hides what the user cannot use (#469).
- Accounts without the Admin role are kept out of the admin panel (#597).

## [0.3.1] - 2026-10-18

### Fixed

- Client image now publishes for linux/arm64 (native arm64 runners).

## [0.3.0] - 2026-10-18

### Added

- One-command install script with automatic HTTPS.
- Docker Hub images, published per surface as `innovayse/hostpanel:api-<version>`,
  `innovayse/hostpanel:client-<version>` and `innovayse/hostpanel:admin-<version>`.
- Demo mode for public showcase instances.
- Backchannel logout, so signing out of the identity provider ends the HostPanel session too.

### Changed

- Docker layout reorganised to follow the repository rules.
- Configuration is read from environment variables only.
- The client and admin frontend images are configurable at runtime, so one image serves any deployment.

### Fixed

- Test-host leaks in the backend test suite.
- The client portal now sends the CSRF header on state-changing requests.

## [0.1.0] - 2026-05-15

### Added

- Initial open-source release under AGPL-3.0: API, client portal and admin panel.
- Issue templates and a pull request template.

### Fixed

- Backend sources were tracked as a submodule; they are now part of the repository.
- Repository URLs in the README and clone instructions.

[Unreleased]: https://github.com/innovayse/hostpanel/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/innovayse/hostpanel/compare/v0.3.1...v0.4.0
[0.3.1]: https://github.com/innovayse/hostpanel/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/innovayse/hostpanel/releases/tag/v0.3.0
[0.1.0]: https://github.com/innovayse/hostpanel/releases/tag/v0.1.0
