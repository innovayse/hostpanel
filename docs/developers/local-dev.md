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

Services started: `hostpanel-db`, `hostpanel-api`, `hostpanel-client`, `hostpanel-admin`, plus the message queue, cache and local mail catcher.

</li>
<li>

Follow the API log to watch startup and migrations:

```bash
docker compose logs -f hostpanel-api
```

Wait for "Now listening on" before opening the apps.

</li>
</Steps>

## Open it

<SettingTable :settings="[
  { name: 'CLIENT_HOST_PORT', type: 'number', default: '3001', description: 'Client portal' },
  { name: 'ADMIN_HOST_PORT', type: 'number', default: '5174', description: 'Admin panel' },
  { name: 'API_HOST_PORT', type: 'number', default: '5149', description: 'API and OpenAPI page at /swagger' },
  { name: 'MAILHOG_UI_HOST_PORT', type: 'number', default: '8027', description: 'Local mail inbox UI. Shows all outgoing emails.' },
]" />

The dev seed creates these accounts:

| Account | Email | Password env var |
|---------|-------|-----------------|
| Admin | `admin@example.test` | `DEV_SEED_ADMIN_PASSWORD` (default: `Admin123!`) |
| Client | `owner@example.test` and others | `DEV_SEED_CLIENT_PASSWORD` (default: `Test@12345`) |

## Hot reload

The API container mounts the source tree and rebuilds on file changes. If the watcher misses changes (common on some host filesystems), set `DEV_FILE_POLLING=true` in `.env` and restart.

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

Drop the database inside the running container and restart to re-seed:

```bash
docker compose exec hostpanel-api dotnet ef database drop --force
docker compose restart hostpanel-api
```

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Port already in use | Change `API_HOST_PORT`, `CLIENT_HOST_PORT` or `ADMIN_HOST_PORT` in `.env` |
| Subnet clash with another network | Change `STACK_NETWORK_SUBNET` in `.env` (default `172.30.0.0/24`) |
| File watcher not picking up changes | Set `DEV_FILE_POLLING=true` in `.env` and restart the stack |
