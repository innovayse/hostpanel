# Innovayse

A modern, self-hosted web hosting management platform — an open-source alternative to WHMCS. Built with ASP.NET Core 9, Nuxt 4, and Vue 3.

> **License:** [Business Source License 1.1](LICENSE) — free to self-host, source-available, converts to GPL v2 on 2028-05-15.

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
- (For local development without Docker) .NET 9 SDK, Node.js 20+, yarn, PostgreSQL 17

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
