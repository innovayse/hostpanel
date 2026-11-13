---
title: Run Hostpanel locally
description: Start the full development stack on your machine with Docker Compose.
---

# Run Hostpanel locally

## What you need

- A container engine with Compose support
- About 8 GB of free memory
- Ports `3001`, `5174` and `5149` free on the host

## Clone the repository

The plugin SDK is a git submodule. Clone with `--recurse-submodules` to get it in one step:

```bash
git clone --recurse-submodules https://github.com/innovayse/hostpanel
```

If you already cloned without it:

```bash
git submodule update --init
```

## Start the stack

<Steps>
<li>

Change to the docker directory:

```bash
cd docker
```

</li>
<li>

Copy the example environment file:

```bash
cp .env.example .env
```

</li>
<li>

Start all services:

```bash
docker compose -f docker-compose.shared.yml -f docker-compose.dev.yml up -d
```

Services started: `hostpanel-db`, `hostpanel-api`, `hostpanel-client`, `hostpanel-admin`, plus RabbitMQ, Redis and MailHog.

</li>
<li>

Follow the API log to watch startup and migrations:

```bash
./scripts/manage.sh logs api
```

Wait for "Now listening on" before opening the apps.

</li>
</Steps>

## Open it

<SettingTable :settings="[
  { name: 'CLIENT_HOST_PORT', type: 'number', default: '3001', description: 'Client portal: http://localhost:3001' },
  { name: 'ADMIN_HOST_PORT', type: 'number', default: '5174', description: 'Admin panel: http://localhost:5174' },
  { name: 'API_HOST_PORT', type: 'number', default: '5149', description: 'API and OpenAPI page: http://localhost:5149/swagger' },
  { name: 'MAILHOG_UI_HOST_PORT', type: 'number', default: '8027', description: 'Local mail inbox UI: http://localhost:8027' },
]" />

The dev seed creates these accounts:

| Account | Email | Password env var |
|---------|-------|-----------------|
| Admin | `admin@example.test` | `DEV_SEED_ADMIN_PASSWORD` (default: `Admin123!`) |
| Client | `owner@example.test` and others | `DEV_SEED_CLIENT_PASSWORD` (default: `Test@12345`) |

## Hot reload

The API container mounts the source tree and rebuilds on file changes. File polling is enabled by default in `.env.example` to cover host filesystems that do not emit watch events reliably.

<Callout type="warning">
Never run <code>dotnet build</code>, <code>dotnet restore</code>, <code>npm install</code> or similar on the host while the dev stack is up. The container and the host will fight over the same files.
</Callout>

## Without containers

If you prefer to run the stack directly, you need .NET 9 SDK, Node.js 22+, yarn and PostgreSQL 17.

```bash
cd backend && dotnet run --project src/Innovayse.Hostpanel.API   # http://localhost:5148
cd client  && yarn install && yarn dev                            # http://localhost:3000
cd admin   && npm install && npm run dev                          # http://localhost:5173
```

Set the API's connection string, RabbitMQ and SMTP settings through environment variables, user secrets or `appsettings.Development.json` (git-ignored).

## Reset the data

Use the management script from the `docker/` directory:

```bash
./scripts/manage.sh db reset
```

This asks for confirmation, drops the database and applies all migrations. To seed development data afterwards:

```bash
./scripts/manage.sh db seed
```

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Port already in use | Change `API_HOST_PORT`, `CLIENT_HOST_PORT` or `ADMIN_HOST_PORT` in `.env` |
| Subnet clash with another network | Change `STACK_NETWORK_SUBNET` in `.env` (default `172.30.0.0/24`) |
