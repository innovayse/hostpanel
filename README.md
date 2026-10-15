<p align="center">
  <img src="logo.svg" alt="HostPanel" height="64">
</p>

<h1 align="center">HostPanel</h1>

<p align="center">
  <b>Self-hosted billing and automation for web hosting companies.</b><br>
  An open-source alternative to WHMCS, built with ASP.NET Core 9 and Vue 3.
</p>

<p align="center">
  <a href="LICENSE"><img alt="License: AGPL v3" src="https://img.shields.io/badge/license-AGPL--3.0-blue"></a>
  <a href="https://github.com/innovayse/hostpanel/stargazers"><img alt="GitHub stars" src="https://img.shields.io/github/stars/innovayse/hostpanel?style=flat"></a>
  <a href="https://github.com/innovayse/hostpanel/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22"><img alt="Good first issues" src="https://img.shields.io/github/issues/innovayse/hostpanel/good%20first%20issue?label=good%20first%20issues&color=7057ff"></a>
  <a href="https://github.com/innovayse/hostpanel/discussions"><img alt="Discussions" src="https://img.shields.io/github/discussions/innovayse/hostpanel"></a>
  <a href="https://discord.gg/NfqTkyTgD"><img alt="Discord" src="https://img.shields.io/badge/chat-on%20Discord-5865F2?logo=discord&logoColor=white"></a>
  <a href="https://github.com/innovayse/hostpanel/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/innovayse/hostpanel/actions/workflows/ci.yml/badge.svg"></a>
</p>

---

## Why HostPanel

If you sell hosting or domains, you probably run WHMCS: closed source, priced per client,
and written in PHP you are not allowed to change. HostPanel covers the same job (client
portal, billing, provisioning, domains, support tickets) with code you can read, run on
your own server and extend.

- **You own it.** AGPL-3.0, self-hosted with Docker, no per-client licence fee.
- **Modern stack.** ASP.NET Core 9 + PostgreSQL on the backend, Nuxt 4 / Vue 3 on the front.
- **Built to be extended.** Payment gateways, registrars and provisioning panels are
  plugins behind small interfaces, with an SDK for writing your own.
- **Migration path.** Import clients and data from an existing WHMCS installation.

## Live demo

**[hostpanel.innovayse.com](https://hostpanel.innovayse.com)** (reset periodically)

| Role     | Email                    | Password  |
|----------|--------------------------|-----------|
| Admin    | superadmin@hostpanel.com | Admin123! |
| Reseller | reseller@hostpanel.com   | Admin123! |
| Client   | customer@hostpanel.com   | Admin123! |

## Features

- **Client portal**: order services, manage domains, pay invoices, open support tickets
- **Admin panel**: clients, products, billing, provisioning and integrations in one place
- **Billing**: invoices and online payments (Stripe, Inecobank). PayPal and bank transfer are
  [on the roadmap](https://github.com/innovayse/hostpanel/issues?q=is%3Aissue+is%3Aopen+gateway)
- **Domains**: registrar integrations for Namecheap and Name.am
- **Provisioning**: CWP (CentOS Web Panel), with cPanel/WHM [in progress](https://github.com/innovayse/hostpanel/issues/51)
- **Storefront templates**: three switchable designs, brandable from the admin panel
- **Multi-language**: English, Russian and Armenian
- **Plugin SDK**: extend it through `Innovayse.SDK`
- **WHMCS migration**: import clients and data from an existing installation

See the [open feature requests](https://github.com/innovayse/hostpanel/issues?q=is%3Aissue+is%3Aopen+label%3Aenhancement)
for what is coming next, and vote with a 👍 on the ones you need.

## Tech stack

| Layer | Technology |
|-------|-----------|
| Backend API | ASP.NET Core 9, Wolverine, EF Core, PostgreSQL |
| Client portal | Nuxt 4, Vue 3, TypeScript, Tailwind CSS, Pinia |
| Admin panel | Vite, Vue 3, TypeScript, Tailwind CSS |
| Messaging | RabbitMQ + Wolverine outbox |
| Auth | ASP.NET Core Identity, JWT in an httpOnly cookie |
| Email | MailKit (MailHog in development) |
| API docs | Scalar |

---

## Quick start

You need [Docker](https://docs.docker.com/get-docker/) with Compose v2. Nothing else, and no
account or token from us.

> **This runs a development stack** for trying HostPanel and working on it: the API runs
> under `dotnet watch` with your source tree mounted, and it seeds demo data. Do not expose
> it to the internet. See [Running in production](#running-in-production).

```bash
git clone https://github.com/innovayse/hostpanel.git
cd hostpanel
cp .env.example .env      # AUTH_MODE=local and ASPNETCORE_ENVIRONMENT=Development by default
docker compose up -d
docker compose logs -f hostpanel-api   # migrations and demo data run on startup
```

Then open:

| | URL |
|---|---|
| Client portal | http://localhost:3001 |
| Admin panel | http://localhost:5174 |
| API docs | http://localhost:5149/scalar |
| MailHog (dev mail) | http://localhost:8027 |

In Development the API seeds demo data on every start, including an admin account
`admin@hostpanel.com` / `Admin123!` and sample clients such as `customer@hostpanel.com`.

For local development without Docker you need the .NET 9 SDK, **Node.js 22+**, yarn and
PostgreSQL 17 (Nuxt's toolchain refuses Node 20). See [Development](#development).

### Running in production

A step-by-step production guide is still to be written (help is welcome); until then, ask in
[Q&A](https://github.com/innovayse/hostpanel/discussions/categories/q-a). What changes compared with the quick start:

- `ASPNETCORE_ENVIRONMENT=Production`: no demo data, no seeded accounts
- real secrets: `JWT_SECRET` (32+ characters), `ENCRYPTION_KEY` (`openssl rand -base64 32`),
  `POSTGRES_PASSWORD`, and a real SMTP server
- the `prod` build target (`docker-compose.prod.yml` builds it) instead of the dev target, and
  no Docker socket mounted into the API
- a reverse proxy with TLS in front of the client portal, admin panel and API

**First admin account.** On a fresh install with no admin, the API prints a one-time setup
token to its log. Open the admin panel, create your account, and paste the token when asked.
It proves you operate this server, and it stops working once setup is done.

## Configuration

Copy `.env.example` to `.env`. The file documents every variable; these are the ones you
will touch first:

| Variable | Description | Default |
|----------|-------------|---------|
| `AUTH_MODE` | `local` (standalone, this README) or `sso` (Innovayse SSO, see below) | `local` |
| `JWT_SECRET` | JWT signing key, at least 32 characters | **change this** |
| `ENCRYPTION_KEY` | Encrypts stored server passwords and API tokens. `openssl rand -base64 32` | dev-only key |
| `POSTGRES_PASSWORD` | PostgreSQL password | `postgres` |
| `SMTP_HOST` / `SMTP_PORT` | Outgoing mail | `mailhog` / `1025` |
| `SMTP_ENCRYPTION` | `tls`, `ssl` or `none`. Required: the API will not guess from the port | `none` |
| `SMTP_FROM` | Sender address on outgoing mail | `noreply@innovayse.com` |

For the client portal, copy `client/.env.example` to `client/.env`.

### Authentication modes

| | `local` (default) | `sso` |
|---|---|---|
| Who it is for | Anyone self-hosting | Innovayse's own deployments |
| Sign-in | Email and password (+ optional TOTP), against this database | Innovayse SSO |
| Admin panel | Email/password form, session in an httpOnly cookie | SSO button |
| Build requirement | None | `Innovayse.Auth` from a private registry |

`sso` mode depends on `Innovayse.Auth`, which is published to a private package registry.
A plain clone builds without it: the package is referenced only when
`NUGET_INNOVAYSE_CREDENTIALS` is set (or with `-p:InnovayseSso=true`), and a build without
it refuses to start in `sso` mode with a message saying so. Details are in
`backend/Directory.Build.props`.

---

## Project structure

```
hostpanel/
├── backend/                  # ASP.NET Core solution
│   ├── src/
│       ├── Innovayse.API/        # Controllers (thin, Wolverine dispatch)
│       ├── Innovayse.Application/ # CQRS handlers, validators, DTOs
│       ├── Innovayse.Domain/      # Entities, value objects, interfaces
│       ├── Innovayse.Infrastructure/ # EF Core, repositories, integrations
│       ├── Innovayse.Providers.CWP/  # CWP provisioning provider
│       ├── Innovayse.Providers.CWP7/ # CWP7 provisioning provider
│       ├── Innovayse.Providers.Inecobank/ # Inecobank payment gateway
│       └── Innovayse.SDK/         # Plugin SDK
│   └── tests/                    # xUnit suites (unit + Testcontainers integration)
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

The quickest loop is to let Docker run the infrastructure and run the API on your machine:

```bash
docker compose up -d hostpanel-db rabbitmq mailhog   # Postgres :5434, RabbitMQ :5673, MailHog :1026

cd backend
export ASPNETCORE_ENVIRONMENT=Development Auth__Mode=local \
  ConnectionStrings__DefaultConnection="Host=localhost;Port=5434;Database=innovayse_dev;Username=postgres;Password=postgres" \
  RabbitMQ__Host=localhost RabbitMQ__Port=5673 RabbitMQ__User=guest RabbitMQ__Password=guest \
  Smtp__Host=localhost Smtp__Port=1026 Smtp__Encryption=none
dotnet run --project src/Innovayse.API      # http://localhost:5148
```

Instead of exporting variables you can put the same keys in
`src/Innovayse.API/appsettings.Development.json` (it is gitignored, so nothing you put there
is committed) or in user-secrets (`dotnet user-secrets set "Jwt:Secret" "…"`).

Run the tests with `dotnet test`. The integration suite starts its own PostgreSQL through
Testcontainers, so it needs Docker running.

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

## Storefront templates and branding

The client portal ships three designs (`aurora`, `nova`, `classic`) and can be rebranded
from **Admin → Settings** without touching code. How to pick one, add your own, and every
branding setting are in [guides/storefront.md](guides/storefront.md).

---

## Integrations

HostPanel uses a pluggable provider model. The simplest way to add a provider is a plugin
project built on `Innovayse.SDK`: derive from its base class (for example
`ProvisioningProviderBase` in `Innovayse.SDK.Base`), the way `Innovayse.Providers.CWP` and
`Innovayse.Providers.Inecobank` do.

| Interface | Purpose | Built-in providers |
|-----------|---------|-------------------|
| `IPaymentGateway` | Payment processing | Stripe, Inecobank |
| `IRegistrarProvider` | Domain registration | Namecheap, Name.am |
| `IProvisioningProvider` | Hosting provisioning | CWP, CWP7, cPanel/WHM (in progress) |

A new payment gateway, registrar or control panel is one of the most useful things you can
contribute. Open an issue first so we can agree on the shape.

---

## Contributing

Contributions of every size are welcome: a typo, a translation, a bug report, a new
provider.

1. Look at [good first issues](https://github.com/innovayse/hostpanel/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22)
   or [help wanted](https://github.com/innovayse/hostpanel/issues?q=is%3Aissue+is%3Aopen+label%3A%22help+wanted%22)
2. Comment on the issue so nobody duplicates your work
3. Read [CONTRIBUTING.md](CONTRIBUTING.md) for setup, code style and the PR checklist

Questions and ideas go to [GitHub Discussions](https://github.com/innovayse/hostpanel/discussions).
Security problems go to the address in [SECURITY.md](SECURITY.md), not to a public issue.

## Community

- 💬 [Discussions](https://github.com/innovayse/hostpanel/discussions): questions, ideas, show what you built
- 🐛 [Issues](https://github.com/innovayse/hostpanel/issues): bugs and feature requests
- ⭐ Star the repo if HostPanel is useful to you. It helps other hosting companies find it.

---

## License

Copyright (c) 2024-2026 Innovayse LLC.

HostPanel is free software, licensed under the
[GNU Affero General Public License v3.0 or later](LICENSE). You may use, modify and
self-host it, including commercially. If you run a modified version as a network service,
the AGPL asks you to offer your users the source of that modified version.

Releases before this change were published under the Business Source License 1.1.
