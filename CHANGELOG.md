# Changelog

All notable changes to HostPanel are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Client portal SSH keys: a "Deploy automatically from GitHub or GitLab" guide with a ready GitHub Actions workflow and GitLab CI job, filled in with the account's host, port and username (en, ru, hy).
- SSH key agent 1.2.0: optional jailed SSH enrolment (`jail_enrol`). The first key HostPanel adds jails the account (jailkit tree, home bind-mounted by a systemd unit, `sftp-ssh` group, bash) and removing the last managed key withdraws it; off by default.
- `PanelNetwork:HostAddresses` (`PANEL_HOST_ADDRESSES`): reach a control panel's API over a VPN address while requests and TLS keep its hostname, so customer links and certificate checks are unchanged.
- Storefront content: testimonials, partners, projects and FAQs, with English, Russian and Armenian translations, served from `/api/public/{testimonials,partners,projects,faqs}`.
- Admin pages under "Storefront content" to create, edit and order the storefront content.
- Storefront content ships empty: operators enter testimonials, partners, projects and FAQs in the admin; the repository carries no site content.
- Demo mode seeds a fixed, maximal sample data set that is reset on schedule.
- SSH key management for hosting services: customers add and revoke ED25519 public keys for their hosting account from the client portal (Services → SSH Keys); HostPanel installs them on the account's server and never stores or accepts a private key. Off on every server until an administrator enables it.
- SSH key agent for managed servers (`backend/agent/ssh-key-agent`): administrators connect a server from Servers → SSH with a per-server management key, a one-command installer whose download is checked against a SHA256 before it runs, a pinned host key and a test that must pass before the feature can be enabled. Profiles for CWP and plain Linux; see the agent README for the network requirements, including `PerSourcePenaltyExemptList` on OpenSSH 9.8 and later.
- Suspending a service takes its SSH keys off the server and unsuspending puts the same keys back; terminating a service revokes them. Control panels suspend by locking the password, which does not stop a key from logging in, so suspend through HostPanel rather than in the panel directly.
- Servers → SSH shows ready-made firewall lines (CSF, firewalld, ufw) and the sshd `PerSourcePenaltyExemptList` line that let only this HostPanel reach the agent port, once `SSH_AGENT_SOURCE_ADDRESSES` (`SshAgent__SourceAddresses`) names the address HostPanel connects from. A failed host key read now says whether the endpoint did not answer (network or VPN), refused the port (nothing listening or a rejecting firewall) or hung up before the SSH greeting (sshd turning HostPanel away), and shows it next to the button.
- Invoices can be downloaded as a PDF from the admin invoice page and the client portal, from `GET /api/billing/{id}/pdf` and `GET /api/me/invoices/{id}/pdf`; drafts are admin-only and stamped DRAFT. An invoice with more lines than `InvoicePdf__MaxLines` (500 by default) is refused, at most `InvoicePdf__MaxConcurrentRenders` (4 by default) render at once across the process, and downloads are sent with `Cache-Control: private, no-store`, through the client portal as well (#247).

### Changed

- The plugin SDK (`Innovayse.SDK`) is MIT licensed, so a plugin may be free or paid, open or closed source; the rest of the repository stays AGPL-3.0-or-later. The package ships with its license text and readme inside, and its readme shows how to pin a version and write a minimal plugin.
- The plugin SDK moved to its own repository, [innovayse/hostpanel-sdk](https://github.com/innovayse/hostpanel-sdk), with its history, and is included here as a git submodule at the same path (`backend/src/Innovayse.SDK`). Clone with `git clone --recurse-submodules`, or run `git submodule update --init` after a plain clone. The SDK package is now built and published from that repository; this repository no longer publishes it.
- Gateway payment reconciliation no longer gives up on a payment that is still pending after 24 hours. It asks the gateway again with a doubling wait (5 minutes at first, at most 6 hours, set in `ScheduledJobs`) until the gateway gives a final answer, and staff are mailed once per payment when it stays pending for 24 hours.
- Automatic termination of a long suspension waits `billing.renewal.terminate_after_suspended_days` (default 14) instead of a fixed 14 days, and staff are mailed when the hosting account cannot be removed; the service stays suspended and the next run retries it.
- Recurring jobs may run as long as their lease (`ScheduledJobs:LeaseMinutes`) instead of being cancelled after one minute.
- A production host loads message handler code generated when the image is built instead of compiling every handler on its first use, which keeps the API's memory flat after a restart.
- A client no longer reads an invoice that is still a draft through any invoice route; it answers like an invoice that does not exist.
- Billing cycles and domain actions are enums end to end. The API sends and accepts the billing cycle of a service or order line as `Monthly` or `Annual` (the old `annually` spelling is no longer accepted on the wire) and a domain action as `Register`, `Transfer` or `Renew`; stored values written in the older spellings are still read, and the release rewrites them to `Monthly` or `Annual`. The TLD price table now lists its extensions as an array of entries that each carry their `tld`. Importing a service or order that names a cycle other than monthly or annual is skipped and logged instead of being stored on a guessed cycle.
- Recurring billing engine: a renewal invoice is raised 14 days before a service or domain falls due (`billing.renewal.invoice_days_before`), reminders go out 7, 3 and 1 days before (`billing.renewal.reminder_days`), and paying the invoice is what extends the service or renews the domain, exactly once. Credit is applied to renewal invoices automatically (`billing.renewal.auto_apply_credit`). The next period is raised only after the previous one is paid, at most once per daily run; a period that already started (a back-dated renewal date) gets the normal payment window, due 14 days after it is raised, instead of being born overdue.
- Overdue suspension: any overdue invoice -- renewal or not -- gets a grace period after its due date (`billing.renewal.suspend_after_overdue_days`, default 3), then only the services on its lines are suspended. An overdue invoice with no service lines now suspends nothing (it used to suspend every service of the client). Paying lifts a suspension made for an overdue invoice once no other overdue invoice covers the service, and the daily renewal run lifts any that were missed; a suspension made by an operator, or for an expired domain, is never lifted by a payment.
- An operator who unsuspends a service that is still on an invoice overdue past the grace sees it suspended again at the next 09:00 run, unless the service has `OverrideAutoSuspend` set or a `SuspendUntil` date in the future.
- Cancelling a renewal invoice does not stop the renewal: the period is invoiced again on the next daily run while it is due. To stop a service renewing, set `AutoTerminateEndOfCycle` or open a cancellation request until the admin auto-renew toggle lands. A service skipped that way whose renewal date has already passed is running unbilled; a Warning says so on every run, and one Warning per run counts auto-renewing services that have no renewal date at all (`NextRenewalAt` empty) and are never billed.
- Domains are no longer renewed for free: auto-renewal now bills the client, and the registrar is called only once the invoice is paid. A failed or unconfirmed registrar renewal alerts staff (`Notifications__AdminEmail`, falling back to `Notifications__ContactEmail`). A client renew order takes the domain off any open renewal invoice first, returning that invoice's credit; an overdue invoice that bills the domain with other items, or a renewal invoice with a payment in progress (PaymentPending) or in Collections, has to be settled first (`RENEWAL_INVOICE_OPEN`, 409). While that renew order is unpaid the daily run does not bill the domain again. An auto-renewing domain still gets the expiry warnings unless its renewal invoice is open and renewal reminders are on.
- Rollout note: on first deploy, (1) any service whose `NextRenewalAt` is already in the past gets a renewal invoice immediately, due 14 days later; (2) at the first 09:00 run every Active service on an invoice -- including invoices raised before this release -- that is already overdue by more than 3 days is suspended. Review those before deploying, and set `OverrideAutoSuspend`/`SuspendUntil` or settle the invoices where a suspension is not wanted:

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
- Hero stats and badge, contact channels and the site name are read from settings (`portal.stats.*`, `portal.hero.badge`, `portal.contact.*`, `portal.site.name`); sections with no content are hidden.
- The demo site name is "Hostpanel Demo".
- The fallback site name is "Hostpanel"; no operator brand is hard-coded.
- Innovayse.Auth is upgraded to 3.1.0.
- Orders, quotes and transactions now record the currency their amounts are in, and the admin lists show each amount with it (#461).
- The admin transactions totals add every currency up in the base currency and no longer show 0 across all clients (#461).
- API changes integrators must follow:
  - Paged lists answer `{ items, pageNumber, pageSize, totalCount, totalPages }`.
  - The liveness probe is `GET /health` (it was `/api/health`).
  - `GET /api/products` and `GET /api/products/groups` are for admins only. The public catalog is `GET /api/public/products` and `GET /api/public/product-groups`.
  - A client edits its own profile with `PATCH /api/clients/me`, which takes only the fields a client may change.
  - The knowledge base article list is paged, and its categories have their own endpoint.
  - The admin service detail is saved through six separate `PUT` endpoints, one per section, instead of one large update.
  - A refund takes a `refundType` value from a fixed list and no longer takes a `transactionId`.
  - A 429 answer carries the standard error body (`HOSTPANEL_RATE_LIMITED`), like every other error.
  - New error codes, among them `INVOICE_HAS_PAYMENTS`, `INVOICE_PAYMENT_EXCEEDS_DUE`, `INVOICE_CREDIT_EXCEEDS_DUE`, `INVOICE_AMOUNT_STILL_DUE`, `INVOICE_REFUND_EXCEEDS_PAID`, `INVOICE_REFUND_CONFLICT`, `INVOICE_NOT_DELETABLE`, `TRANSACTION_AMOUNT_REQUIRED`, `SERVICE_STATUS_CHANGE_NOT_ALLOWED`, `SERVICE_AUTOMATION_INCONSISTENT`, `SERVICE_NOT_TERMINATED`, `PAYMENT_METHOD_UNAVAILABLE`, `PAYMENT_INTENT_MISMATCH`, `PAYMENT_INTENT_ALREADY_USED`, `GATEWAY_PAYMENT_MISMATCH`, `GATEWAY_REFUND_REFUSED`, `CARD_REFUND_REFUSED` and the `REGISTRAR_*` codes.
- An invoice that already has a payment recorded can no longer be cancelled (`INVOICE_HAS_PAYMENTS`).
- A payment or credit larger than what the invoice still owes is refused.
- Marking an invoice paid is allowed only when nothing is left to pay (`INVOICE_AMOUNT_STILL_DUE`); otherwise record the payment.
- Every `Cors__AllowedOrigins__N` entry must be an absolute origin such as `https://host.example.com`, or the API refuses to start.
- The anonymous order endpoints are rate limited. The limits are set under `RateLimit` (see [docker/README.md](docker/README.md)).
- Accepting an order (`POST /api/orders/{id}/accept`) answers 200 with the lines that could not be fulfilled, so staff see what still needs attention.
- Deleting or cancelling an order also cancels the order's open invoice.
- Service status changes follow a fixed table: only an Active service can be suspended, only a Suspended service can be unsuspended, and a Terminated service cannot be terminated again. Each change asks the hosting panel exactly once, and when the panel refuses the API answers 502 and the service keeps its status.
- A service billed by hand (`AutoRenew` off) is never suspended or terminated automatically.
- A new order's service is set up only after its first invoice is paid.
- Imported services and domains send no notifications.
- An account is locked for a while after repeated failed sign-ins, and sign-in answers 423 while it is locked.
- Forgot password always answers 200, whether or not the address belongs to an account.
- Trusted proxies are configured under `RateLimit` with `KnownProxies` and `KnownNetworks`, and `TrustedProxyCount` defaults to 0, so a forwarded client address is believed only from a proxy you list.
- Sign-on tokens are accepted only when they were issued to the configured client.
- The setup token is required in every mode, and the setup status no longer returns `tokenRequired`.
- Every card payment carries an attempt id, so a retried or duplicated callback is matched to the right attempt.
- Admin API shapes are nested: servers, domains, services, invoices, tickets, slides, reports and clients answer and accept grouped objects instead of flat records. Integrators using the admin API must follow the new shapes in the API reference.
- Account switching stays a `GET` request.
- Configuration that must now be set: `ENCRYPTION_KEY` in every environment, `SESSION_ENCRYPTION_KEY` for the client app sessions, the trusted proxy counts under `RateLimit`, `EDGE_SCHEME`, and absolute `Cors:AllowedOrigins` entries, which also decide which return URLs a payment may come back to. In Production the API refuses to start while a secret still holds a placeholder value.
- The Reseller role no longer reaches staff routes.
- A client's status is enforced: a client that is not active is refused where the action needs an active account.
- Email templates are kept per language, and an email is sent in the recipient's language when a template exists for it.
- Migration tool: the import now expects the updated source addon, so operators must update the addon on the source system before running a new import.
- Migration tool: the source addon reads product prices from the source's pricing table in its default currency, and a cycle the source does not sell is imported without a price instead of at zero. An order's lines are read from the services, service addons and domains placed with it.
- Migration tool: the source addon download works in the published image. The build ships the addon beside the API, and `MigrationPlugin__Path` can point the download at another folder; a missing folder answers `MIGRATION_PLUGIN_NOT_FOUND` (404).

### Removed

- The previous operator's sample storefront content.
- `POST /api/clients`. Clients are created through registration.
- The slide image upload endpoint.

### Fixed

- SSH key agent 1.2.2: jails also get `rsync`, `tar`, `gzip` and `unzip`, so a CI job can deploy a site over SSH.
- SSH key agent 1.2.1: jails get `uname`, `id`, `hostname`, `dircolors`, `/etc/bashrc` and `/etc/profile.d/colorls.sh` (`jail_extras`), so customer logins no longer print `/usr/bin/uname: No such file or directory` and `ll` works; existing jails get them on the next key.
- The client setup wizard now provisions on a server from the product's server group; it picked from every server of the module, so a product tied to one server was set up on the default one.
- A signed-in customer's first order now grants them the Client role. An SSO sign-up held none, so after ordering every portal page answered 403 and the portal called them "not a customer account"; an email sent as the display name is no longer stored as their first and last name.
- Free orders ($0 total) are settled at checkout and fulfilled at once; they used to wait on a payment that never came, so their services were never created and checkout still asked for a payment method's payment step.
- A CWP7 connection test refused by the panel (wrong API key, address not allowed) now logs CWP7's reason instead of a JSON conversion error.
- A partial payment no longer marks an invoice Paid. The invoice stays Unpaid (or Overdue) and takes further payments until they cover the total; only then is it Paid and are its order fulfilled, renewals applied and receipt sent. Every way of paying the rest (hosted gateway, card, order card payment, bulk mark-paid) now charges what is still due, never the whole total again, and the invoice API sends `amountDue`, which the client portal shows on its pay button and payment page. An invoice whose payments end up covering it after a credit or a removed line is settled at once; marking a paid invoice unpaid records a reversal so the whole total is due again. Payments above what is due settle the invoice and are not turned into credit automatically. Two payments saved at the same moment that together cover an invoice are settled by the next daily renewals run.
- Recurring jobs no longer stop for good after one failed run. A handler that threw had the next run it scheduled discarded, even from a `finally`, so a single database outage ended the daily chain of renewals, overdue suspension, reminders, gateway reconciliation and every other job until the next restart. Every job now logs a failed or timed-out run and schedules the next one regardless. The domain expiry check, which ran only once after each start, now runs daily, so expired domains are marked and expiry warnings are sent again. An admin's TLD "sync now" no longer adds another daily sync chain per click.
- Client portal sessions no longer end when the access token expires. The tokens are kept on the server, in the Redis the stack already runs (`NUXT_SESSION_REDIS`, the same value as `REDIS_CONNECTION`), and the browser holds one small opaque, httpOnly session cookie. A refresh token larger than the 4 KB a browser keeps per cookie used to be dropped silently, which signed people out after about fifteen minutes. Signing out deletes the session on the server, and a back-channel logout ends every portal session of the person from the API (`PortalSessions__RedisConnection`). People signed in before the upgrade sign in once more.
- A failed silent sign-in now shows a sign-in prompt on the page instead of a plain guest page, and an `authuser` address on a link is passed to the identity provider as `login_hint`.
- A paid domain registration or transfer is no longer faked or crashed when the registrar is not configured or refuses it. Name.am and Namecheap now answer an unconfigured integration with a failure for every purchase and every change to a registrar-held domain (nameservers, lock, WHOIS privacy, DNS records, contacts, auto-renew), so a paid order is refunded automatically and a change is not saved when the registrar never made it. Domains with no registrar reference are still managed locally, except their contacts, which live only at the registrar and are refused (`DOMAIN_NO_REGISTRAR_REF`). A Namecheap `ERROR` answer to a registration, transfer or renewal is a clean refusal, so a paid renewal is retried rather than parked as an unknown outcome, and only `Registered="true"`/`Transfer="true"` count as confirmed. A Name.am 4xx to a registration or transfer is a refusal as well.
- Registrar purchases are safer to retry and no longer send blank or leaked data. A registration or transfer that a registrar refuses as "domain unavailable" is first checked against the account, and counts as placed when the account already holds the name (or has the transfer in progress), so a retry after a timeout no longer refunds a domain we own. A refused purchase now says why: `REGISTRAR_DOMAIN_UNAVAILABLE` (409) or `REGISTRAR_INSUFFICIENT_FUNDS` (503), and an incomplete registrant contact is refused before anything is ordered (`REGISTRAR_CONTACT_VALIDATION_FAILED`, 422). Registrations and transfers now carry the client's own details as the registrant instead of an empty contact. Namecheap commands are sent as a POST form, so the API key and account name never appear in a URL. A Name.am answer counts as a purchase only when it is a list of purchased items, renewals use the renew plan, and a Name.am access token is kept once per connection instead of once per request. Hosts, lock, privacy, DNS, contact and nameserver changes that fail at either registrar now answer with a code instead of an error carrying the registrar's text, and the account e-mail is no longer logged at login.
- A domain renewal no longer reports success when nothing was renewed at the registrar. An unconfigured Name.am or Namecheap integration, a Name.am error response (an error body, an unrecognised body or a 4xx), or a Namecheap answer without `Renew="true"` now fails the renewal, so a paid renewal stays unapplied, is retried daily and alerts staff. A domain with no registrar reference is refused (`DOMAIN_NO_REGISTRAR_REF`) instead of being extended locally; a paid renewal of such a domain is closed and staff are alerted to review a refund. When the registrar reports no new expiry, the current expiry is extended, so an early renewal keeps its remaining days.
- The admin menu no longer "thinks" before a page opens on a slow connection. The admin image now compresses its bundle and lets browsers keep content-hashed assets for good; a screen's code starts downloading when the pointer reaches its link (and, on a cold page load, in parallel with the session check, which itself now asks the mode and the session together); and a thin progress bar shows at the top as soon as a navigation has been waiting for its screen.
- A signed-in caller's first order no longer fails with a server error when it races the client record created on registration; the record is now saved once and the order continues with it.
- Focus styling follows `:focus-visible`.
- The billing toggle is fixed.
- Order numbers are issued from a database sequence.

### Security

- The demo runs on its own network and cannot reach production services.
- General security hardening across the API, the client portal and the admin panel.
- Several authorization, input validation and session handling issues were fixed across the API and both web apps. Upgrading is recommended.
- The client portal now enforces the permissions of linked users on the server: every portal action requires the permission it belongs to, the account owner keeps full access, and a refusal is a 403 with `CLIENT_PERMISSION_DENIED` (#467). The portal reads the real permissions and hides what the user cannot use.

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

[Unreleased]: https://github.com/innovayse/hostpanel/compare/v0.3.1...HEAD
[0.3.1]: https://github.com/innovayse/hostpanel/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/innovayse/hostpanel/releases/tag/v0.3.0
[0.1.0]: https://github.com/innovayse/hostpanel/releases/tag/v0.1.0
