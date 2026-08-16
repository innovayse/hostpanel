# Innovayse

A modern, self-hosted web hosting management platform — an open-source alternative to WHMCS. Built with ASP.NET Core 9, Nuxt 4, and Vue 3.

> **License:** [Business Source License 1.1](LICENSE) — free to self-host, source-available, converts to GPL v2 on 2028-05-15.

---

## 🌐 Live Demo

**[https://hostpanel.innovayse.com](https://hostpanel.innovayse.com)**

| Role     | Email                    | Password  |
|----------|--------------------------|-----------|
| Admin    | superadmin@hostpanel.com | Admin123! |
| Reseller | reseller@hostpanel.com   | Admin123! |
| Client   | customer@hostpanel.com   | Admin123! |

---

## Features

- **Client Portal** — order services, manage domains, view invoices, open support tickets
- **Admin Panel** — full control over clients, products, billing, provisioning, and integrations
- **Billing** — invoices, payment gateways (Stripe, PayPal, bank transfer)
- **Domain Management** — registrar integrations (Namecheap, NameAm)
- **Hosting Provisioning** — cPanel/WHM and CWP support
- **Multi-language** — English, Russian, Armenian (hy)
- **Plugin SDK** — extend functionality via the `Innovayse.SDK`
- **WHMCS Migration** — import clients and data from existing WHMCS installations

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend API | ASP.NET Core 9, Wolverine, EF Core 8, PostgreSQL |
| Client Portal | Nuxt 4, Vue 3, TypeScript, Tailwind CSS, Pinia |
| Admin Panel | Vite, Vue 3, TypeScript, Tailwind CSS, shadcn-vue |
| Messaging | RabbitMQ + Wolverine outbox |
| Auth | ASP.NET Core Identity, JWT + Refresh Tokens |
| Email | MailKit (dev: MailHog) |
| Docs | Scalar |

---

## Requirements

- [Docker](https://docs.docker.com/get-docker/) and Docker Compose v2
- (For local development without Docker) .NET 9 SDK, **Node.js 22+**, yarn, PostgreSQL 17

> The client portal needs Node 22, not 20: Nuxt's `nitropack` pulls
> `rollup-plugin-visualizer@7`, which declares `engines.node >= 22`, and `yarn install`
> refuses on anything older. `docker/client.Dockerfile` already uses `node:22-alpine`.

---

## Quick Start

```bash
# 1. Clone the repository
git clone https://github.com/innovayse/hostpanel.git
cd innovayse

# 2. Copy and configure environment variables
cp .env.example .env
# Edit .env — set JWT_SECRET and any other required values

# 3. Start all services
docker compose up -d

# 4. Apply database migrations
docker compose exec api dotnet ef database update

# 5. Open in browser
#   Client portal:  http://localhost:3000
#   Admin panel:    http://localhost:5173
#   API docs:       http://localhost:5148/scalar
#   MailHog (dev):  http://localhost:8025
```

---

## Environment Variables

Copy `.env.example` to `.env` and fill in the values:

| Variable | Description | Default |
|----------|-------------|---------|
| `POSTGRES_USER` | PostgreSQL username | `postgres` |
| `POSTGRES_PASSWORD` | PostgreSQL password | `postgres` |
| `POSTGRES_DB` | Database name | `innovayse_dev` |
| `JWT_SECRET` | JWT signing key (min 32 chars) | **change this** |
| `RABBITMQ_USER` | RabbitMQ username | `guest` |
| `RABBITMQ_PASSWORD` | RabbitMQ password | `guest` |
| `SMTP_HOST` | SMTP server host | `mailhog` |
| `SMTP_PORT` | SMTP server port | `1025` |

For the client portal, copy `client/.env.example` to `client/.env`.

---

## Project Structure

```
innovayse/
├── backend/                  # ASP.NET Core solution
│   └── src/
│       ├── Innovayse.API/        # Controllers (thin, Wolverine dispatch)
│       ├── Innovayse.Application/ # CQRS handlers, validators, DTOs
│       ├── Innovayse.Domain/      # Entities, value objects, interfaces
│       ├── Innovayse.Infrastructure/ # EF Core, repositories, integrations
│       ├── Innovayse.Providers.CWP/  # CWP provisioning provider
│       └── Innovayse.SDK/         # Plugin SDK
├── client/                   # Nuxt 4 client portal
│   ├── pages/                    # Routes: data, SEO, business logic
│   ├── templates/                # Storefront designs — presentation only
│   │   ├── registry.ts               # name + slot → component
│   │   ├── aurora/                   # Default design
│   │   └── classic/                  # Original design
│   └── components/               # Shared component library
├── admin/                    # Vue 3 admin panel
├── docker/                   # Dockerfiles and nginx config
├── docker-compose.yml
└── .env.example
```

---

## Development

### Backend

```bash
cd backend
dotnet restore
dotnet run --project src/Innovayse.API
```

`appsettings.Development.json` is committed and holds only local values, so this runs
without copying a template first. It used to be an `appsettings.Development.example.json`
you had to copy — which was a quiet hazard, because the copy is gitignored nowhere and
went into the next commit along with whatever real credential had been filled into it.

Anything genuinely secret stays out of the repository. Use user-secrets rather than
editing a settings file:

```bash
dotnet user-secrets init
dotnet user-secrets set "Sso:Authority" "https://sso.example.com"
```

### Client Portal

```bash
cd client
yarn install
yarn dev        # http://localhost:3000
```

### Admin Panel

```bash
cd admin
npm install
npm run dev     # http://localhost:5173
```

---

## Storefront Templates

The public portal ships two designs and renders whichever one is selected. Both
produce identical SEO output — canonical, hreflang and schema.org live in
`client/pages/`, never in a template.

| Template | Description |
|---|---|
| `aurora` | Default. Dark and light modes, Armenian typography, live domain search |
| `classic` | The original storefront design |

**Choosing one.** In order of precedence:

1. The `portal.template` setting, editable in **Admin → Settings → Portal appearance**
2. The `NUXT_PUBLIC_PORTAL_TEMPLATE` environment variable
3. `aurora`

An unrecognised value falls back to `aurora` rather than leaving the site blank, so a
typo in the admin field cannot take the storefront down.

**Adding a template.** Create `client/templates/<name>/` with `layout/Header.vue`,
`layout/Footer.vue` and a `pages/` component for each route, then register the
loaders in `client/templates/registry.ts` and add the name to `TEMPLATE_NAMES` in
`client/templates/types.ts`. A unit test asserts every template implements every
slot, so a missing page fails the build rather than rendering nothing.

Two rules make templates safe to swap:

- **Templates render, pages decide.** A template component takes typed props and
  returns markup; the page above it owns fetching, SEO and business logic. That
  keeps both designs on one code path and one set of head tags.
- **Copy lives in i18n** under a top-level key named for the template
  (`aurora.hero.title`), registered in the `modules` array of
  `client/plugins/i18n.ts`. Locale files are not auto-discovered.

`aurora` follows the first rule throughout, with one exception: its
`pages/Checkout.vue` still carries the ordering flow, because splitting a working
payment path is only worth doing with a backend on hand to place a test order
against. It is `classic`'s script byte-for-byte with restyled markup, so the two
cannot drift apart.

`classic`'s pages are lift-and-shifts of the original route components and keep
their own data loading. That was the point: moving a 500-line page unchanged is
far safer than rewriting it, and the design it renders is the one being replaced.
New templates should follow `aurora`, not `classic`.

Related operator settings, all optional and hidden when empty:
`portal.contact.whatsapp`, `portal.contact.telegram`, `portal.contact.email`,
`portal.contact.phone`, `portal.chat.provider`, `portal.newsletter.action_url`,
`portal.legal.tax_id`, and `portal.social.{facebook,instagram,linkedin,youtube}`.

The header app launcher is off unless `portal.apps.enabled` is `true`, because
the apps it links to only exist in a deployment that runs them. Each entry then
needs a URL of its own — `portal.apps.{account,tasks,erp,hostpanel,sheets,mail,docs,calendar}`,
or the matching `NUXT_PUBLIC_*` variable — and entries without one are left out.

Every seeded setting is editable in **Admin → Settings**; the table takes one row
at a time.

---

## Integrations

Innovayse uses a pluggable provider model. Implement the relevant interface in `Innovayse.Domain` and register in Infrastructure:

| Interface | Purpose | Built-in Providers |
|-----------|---------|-------------------|
| `IPaymentGateway` | Payment processing | Stripe |
| `IRegistrarProvider` | Domain registration | Namecheap, NameAm |
| `IProvisioningProvider` | Hosting provisioning | cPanel/WHM, CWP |

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

---

## License

Copyright (c) 2024 Innovayse.
Licensed under the [Business Source License 1.1](LICENSE).
On 2028-05-15 this software will become available under the GNU GPL v2.0 or later.
