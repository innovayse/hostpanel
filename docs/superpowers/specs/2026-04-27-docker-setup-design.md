# Docker Setup Design — Innovayse

## Overview

Containerize the entire Innovayse stack for both local development (with hot-reload) and production deployment. Replace the native `manage.sh` workflow with Docker Compose.

## Services

| Service | Image | Dev | Prod | Ports (dev) |
|---------|-------|-----|------|-------------|
| postgres | postgres:17-alpine | Yes | Yes | 5432 |
| rabbitmq | rabbitmq:3-management-alpine | Yes | Yes | 5672, 15672 (mgmt UI) |
| mailhog | mailhog/mailhog | Yes | No | 1025 (SMTP), 8025 (UI) |
| api | docker/api.Dockerfile | dotnet watch (volume mount) | Multi-stage .NET 9 build | 5148 |
| client | docker/client.Dockerfile | yarn dev (volume mount) | Multi-stage Node build, output served by Nuxt SSR | 3000 |
| admin | docker/admin.Dockerfile | npm run dev (volume mount) | Multi-stage Node build, static files served by nginx | 5173 |
| nginx | docker/nginx.Dockerfile | Not included | Reverse proxy | 80, 443 |

## File Structure

```
docker/
  api.Dockerfile           # Multi-stage: dev target (dotnet watch) + prod target (publish)
  client.Dockerfile        # Multi-stage: dev target (yarn dev) + prod target (yarn build)
  admin.Dockerfile         # Multi-stage: dev target (npm run dev) + prod target (npm run build + nginx)
  nginx.Dockerfile         # Production reverse proxy
  nginx.conf               # Nginx config for prod routing
.dockerignore              # Shared ignore file
docker-compose.yml         # Development stack (hot-reload, all services)
docker-compose.prod.yml    # Production stack (built images, no mailhog)
.env.example               # Template for all environment variables
```

## Dockerfiles

### api.Dockerfile

Two stages:
- **dev** target: `mcr.microsoft.com/dotnet/sdk:9.0` base, `WORKDIR /app`, volume-mount source, runs `dotnet watch run --urls http://0.0.0.0:5148`. Restores packages in build layer for caching.
- **prod** target: SDK stage builds with `dotnet publish -c Release -o /out`, then runtime stage `mcr.microsoft.com/dotnet/aspnet:9.0-alpine` copies output, exposes 5148.

### client.Dockerfile

Two stages:
- **dev** target: `node:22-alpine` base, installs yarn, `WORKDIR /app`, copies package.json + yarn.lock for dependency caching, runs `yarn dev --host 0.0.0.0`.
- **prod** target: Build stage runs `yarn build`, runtime stage uses `node:22-alpine`, copies `.output/` directory, runs `node .output/server/index.mjs` (Nuxt SSR).

### admin.Dockerfile

Two stages:
- **dev** target: `node:22-alpine` base, `WORKDIR /app`, copies package.json + package-lock.json for caching, runs `npm run dev -- --host 0.0.0.0`.
- **prod** target: Build stage runs `npm run build`, runtime stage uses `nginx:alpine`, copies `dist/` to nginx html root.

### nginx.Dockerfile (prod only)

Based on `nginx:alpine`, copies `nginx.conf`.

## Docker Compose — Development (`docker-compose.yml`)

```yaml
services:
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    ports: ["5432:5432"]
    volumes: [pgdata:/var/lib/postgresql/data]
    healthcheck: pg_isready

  rabbitmq:
    image: rabbitmq:3-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    ports: ["5672:5672", "15672:15672"]
    healthcheck: rabbitmq-diagnostics -q ping

  mailhog:
    image: mailhog/mailhog
    ports: ["1025:1025", "8025:8025"]

  api:
    build:
      context: .
      dockerfile: docker/api.Dockerfile
      target: dev
    ports: ["5148:5148"]
    volumes:
      - ./backend:/app
    environment:
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      ASPNETCORE_ENVIRONMENT: Development
      Smtp__Host: mailhog
      Smtp__Port: 1025
      RabbitMQ__Host: rabbitmq
    depends_on:
      postgres: { condition: service_healthy }
      rabbitmq: { condition: service_healthy }

  client:
    build:
      context: .
      dockerfile: docker/client.Dockerfile
      target: dev
    ports: ["3000:3000"]
    volumes:
      - ./client:/app
      - client_node_modules:/app/node_modules
    environment:
      NUXT_API_URL: http://api:5148
      NUXT_PUBLIC_BASE_URL: http://localhost:3000
    depends_on: [api]

  admin:
    build:
      context: .
      dockerfile: docker/admin.Dockerfile
      target: dev
    ports: ["5173:5173"]
    volumes:
      - ./admin:/app
      - admin_node_modules:/app/node_modules
    environment:
      VITE_API_BASE: http://localhost:5148/api
    depends_on: [api]

volumes:
  pgdata:
  client_node_modules:
  admin_node_modules:
```

Key dev details:
- Source code mounted as volumes for hot-reload.
- `node_modules` stored in named volumes (not mounted from host) to avoid platform mismatch.
- API uses `dotnet watch` for automatic recompilation.
- Services use container DNS names (e.g., `postgres`, `rabbitmq`, `mailhog`) for inter-service communication.
- PostgreSQL has a healthcheck; API waits for it before starting.

## Docker Compose — Production (`docker-compose.prod.yml`)

```yaml
services:
  postgres:
    # Same as dev but no exposed ports (internal only)

  rabbitmq:
    # Same but only 5672 exposed internally, no management UI port

  api:
    build:
      context: .
      dockerfile: docker/api.Dockerfile
      target: prod
    # No volume mounts, no ports exposed (nginx routes to it)
    environment:
      ConnectionStrings__DefaultConnection: ...
      ASPNETCORE_ENVIRONMENT: Production

  client:
    build:
      context: .
      dockerfile: docker/client.Dockerfile
      target: prod
    # Runs Nuxt SSR server internally

  admin:
    build:
      context: .
      dockerfile: docker/admin.Dockerfile
      target: prod
    # Static files served by nginx inside the container

  nginx:
    build:
      context: .
      dockerfile: docker/nginx.Dockerfile
    ports: ["80:80", "443:443"]
    depends_on: [api, client, admin]
```

No mailhog in production. Nginx is the only service exposing ports.

## Nginx Config (Production)

```nginx
# yourdomain.com → client (Nuxt SSR)
server {
    listen 80;
    server_name yourdomain.com;
    location /api/ { proxy_pass http://api:5148/api/; }
    location / { proxy_pass http://client:3000; }
}

# admin.yourdomain.com → admin (static)
server {
    listen 80;
    server_name admin.yourdomain.com;
    location / { proxy_pass http://admin:80; }
}
```

Domain names configurable via environment variables.

## Environment Variables (`.env.example`)

```env
# PostgreSQL
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=innovayse_dev

# RabbitMQ
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest

# JWT
JWT_SECRET=change-this-to-a-32-char-min-secret
JWT_ISSUER=innovayse-api
JWT_AUDIENCE=innovayse-clients

# SMTP (dev uses mailhog, prod uses real SMTP)
SMTP_HOST=mailhog
SMTP_PORT=1025
SMTP_USER=
SMTP_PASSWORD=

# Nginx (prod only)
DOMAIN=yourdomain.com
ADMIN_DOMAIN=admin.yourdomain.com

# Client
NUXT_PUBLIC_BASE_URL=http://localhost:3000
```

## manage.sh Updates

The script will detect whether Docker is available and route commands through `docker compose`:

- `./scripts/manage.sh start` → `docker compose up -d`
- `./scripts/manage.sh stop` → `docker compose down`
- `./scripts/manage.sh restart` → `docker compose restart`
- `./scripts/manage.sh logs <service>` → `docker compose logs -f <service>`
- `./scripts/manage.sh status` → `docker compose ps`
- `./scripts/manage.sh build` → `docker compose build`
- `./scripts/manage.sh db migrate` → `docker compose exec api dotnet ef database update`
- `./scripts/manage.sh db psql` → `docker compose exec postgres psql -U $POSTGRES_USER -d $POSTGRES_DB`
- `./scripts/manage.sh db reset` → drop + recreate via exec
- `./scripts/manage.sh test` → `docker compose exec api dotnet test`

The existing native commands remain as fallback if Docker is not installed (`--no-docker` flag).

## .dockerignore

```
**/node_modules
**/bin
**/obj
**/.nuxt
**/.output
**/dist
**/.git
**/.logs
**/.pids
*.md
docs/
```

## Backend Config Changes

The C# backend `appsettings.json` will keep its current defaults. Docker overrides config via environment variables using the ASP.NET Core convention (`ConnectionStrings__DefaultConnection`, `Jwt__Secret`, etc.). No hardcoded Docker-specific values in the codebase.

## Not In Scope

- SSL/TLS certificates (use certbot or external cert manager separately)
- CI/CD pipeline (separate concern)
- Kubernetes / Swarm orchestration
- Redis (may be added later)
- Monitoring / observability containers (Grafana, Prometheus)
