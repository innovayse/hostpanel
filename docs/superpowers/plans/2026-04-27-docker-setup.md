# Docker Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Containerize the Innovayse stack (API, client, admin, PostgreSQL, RabbitMQ, MailHog) for development with hot-reload and production with Nginx reverse proxy.

**Architecture:** Docker Compose with multi-stage Dockerfiles. Dev compose mounts source volumes for hot-reload. Prod compose builds optimized images and routes traffic through Nginx. manage.sh updated to delegate to `docker compose`.

**Tech Stack:** Docker, Docker Compose, Nginx, PostgreSQL 17, RabbitMQ 3, MailHog, .NET 9 SDK, Node 22

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `docker/api.Dockerfile` | Create | Multi-stage: dev (dotnet watch) + prod (publish) |
| `docker/client.Dockerfile` | Create | Multi-stage: dev (yarn dev) + prod (node SSR) |
| `docker/admin.Dockerfile` | Create | Multi-stage: dev (npm run dev) + prod (nginx static) |
| `docker/nginx.Dockerfile` | Create | Prod reverse proxy image |
| `docker/nginx.conf` | Create | Nginx routing config |
| `docker-compose.yml` | Create | Dev stack with hot-reload |
| `docker-compose.prod.yml` | Create | Prod stack with built images |
| `.env.example` | Create | Environment variable template |
| `.dockerignore` | Create | Build context exclusions |
| `scripts/manage.sh` | Modify | Route commands through docker compose |
| `.gitignore` | Modify | Add `.env` |

---

### Task 1: Create .dockerignore and .env.example

**Files:**
- Create: `.dockerignore`
- Create: `.env.example`
- Modify: `.gitignore`

- [ ] **Step 1: Create `.dockerignore`**

```dockerignore
**/node_modules
**/bin
**/obj
**/.nuxt
**/.output
**/dist
**/.git
**/.logs
**/.pids
**/.codegraph
**/.gitnexus
**/.claude
**/.playwright-mcp
**/wg0.conf
*.png
docs/
rules/
```

- [ ] **Step 2: Create `.env.example`**

```env
# PostgreSQL
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=innovayse_dev

# RabbitMQ
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest

# JWT
JWT_SECRET=change-this-to-a-32-char-min-secret-key-here
JWT_ISSUER=innovayse-api
JWT_AUDIENCE=innovayse-clients

# SMTP (dev: mailhog on port 1025, prod: real SMTP)
SMTP_HOST=mailhog
SMTP_PORT=1025
SMTP_USER=
SMTP_PASSWORD=

# CORS (comma-separated origins)
CORS_ORIGINS=http://localhost:3000,http://localhost:5173

# Client (Nuxt)
API_URL=http://api:5148
NUXT_PUBLIC_BASE_URL=http://localhost:3000

# Nginx (prod only)
DOMAIN=yourdomain.com
ADMIN_DOMAIN=admin.yourdomain.com
```

- [ ] **Step 3: Add `.env` to `.gitignore`**

Append to the existing `.gitignore`:

```
# Docker environment
.env
```

- [ ] **Step 4: Copy `.env.example` to `.env`**

Run: `cp .env.example .env`

- [ ] **Step 5: Commit**

```bash
git add .dockerignore .env.example .gitignore
git commit -m "chore: add .dockerignore and .env.example for Docker setup"
```

---

### Task 2: Create API Dockerfile

**Files:**
- Create: `docker/api.Dockerfile`

- [ ] **Step 1: Create `docker/` directory**

Run: `mkdir -p docker`

- [ ] **Step 2: Create `docker/api.Dockerfile`**

```dockerfile
# ── Dev target ────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dev

WORKDIR /app

# Restore dependencies (cached layer)
COPY backend/Innovayse.Backend.sln ./Innovayse.Backend.sln
COPY backend/src/Directory.Build.props ./src/Directory.Build.props
COPY backend/src/Innovayse.Domain/Innovayse.Domain.csproj ./src/Innovayse.Domain/
COPY backend/src/Innovayse.Application/Innovayse.Application.csproj ./src/Innovayse.Application/
COPY backend/src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj ./src/Innovayse.Infrastructure/
COPY backend/src/Innovayse.API/Innovayse.API.csproj ./src/Innovayse.API/
COPY backend/src/Innovayse.SDK/Innovayse.SDK.csproj ./src/Innovayse.SDK/
COPY backend/src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj ./src/Innovayse.Providers.CWP/
RUN dotnet restore Innovayse.Backend.sln

ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 5148

ENTRYPOINT ["dotnet", "watch", "run", \
    "--project", "src/Innovayse.API/Innovayse.API.csproj", \
    "--no-launch-profile", \
    "--", "--urls", "http://0.0.0.0:5148"]

# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY backend/Innovayse.Backend.sln ./Innovayse.Backend.sln
COPY backend/src/Directory.Build.props ./src/Directory.Build.props
COPY backend/src/Innovayse.Domain/Innovayse.Domain.csproj ./src/Innovayse.Domain/
COPY backend/src/Innovayse.Application/Innovayse.Application.csproj ./src/Innovayse.Application/
COPY backend/src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj ./src/Innovayse.Infrastructure/
COPY backend/src/Innovayse.API/Innovayse.API.csproj ./src/Innovayse.API/
COPY backend/src/Innovayse.SDK/Innovayse.SDK.csproj ./src/Innovayse.SDK/
COPY backend/src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj ./src/Innovayse.Providers.CWP/
RUN dotnet restore Innovayse.Backend.sln

COPY backend/ .
RUN dotnet publish src/Innovayse.API/Innovayse.API.csproj -c Release -o /out --no-restore

# ── Prod target ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS prod

WORKDIR /app
COPY --from=build /out .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5148

ENTRYPOINT ["dotnet", "Innovayse.API.dll", "--urls", "http://0.0.0.0:5148"]
```

- [ ] **Step 3: Verify Dockerfile syntax**

Run: `docker build --check -f docker/api.Dockerfile .`
Expected: No syntax errors (or warnings only).

- [ ] **Step 4: Commit**

```bash
git add docker/api.Dockerfile
git commit -m "chore: add API Dockerfile with dev and prod stages"
```

---

### Task 3: Create Client Dockerfile

**Files:**
- Create: `docker/client.Dockerfile`

- [ ] **Step 1: Create `docker/client.Dockerfile`**

```dockerfile
# ── Dev target ────────────────────────────────────────────────────────────────
FROM node:22-alpine AS dev

RUN corepack enable && corepack prepare yarn@1.22.22 --activate

WORKDIR /app

COPY client/package.json client/yarn.lock ./
RUN yarn install --frozen-lockfile

EXPOSE 3000

CMD ["yarn", "dev", "--host", "0.0.0.0"]

# ── Build stage ───────────────────────────────────────────────────────────────
FROM node:22-alpine AS build

RUN corepack enable && corepack prepare yarn@1.22.22 --activate

WORKDIR /app

COPY client/package.json client/yarn.lock ./
RUN yarn install --frozen-lockfile

COPY client/ .
RUN yarn build

# ── Prod target ───────────────────────────────────────────────────────────────
FROM node:22-alpine AS prod

WORKDIR /app

COPY --from=build /app/.output .output/

ENV NUXT_HOST=0.0.0.0
ENV NUXT_PORT=3000
EXPOSE 3000

CMD ["node", ".output/server/index.mjs"]
```

- [ ] **Step 2: Commit**

```bash
git add docker/client.Dockerfile
git commit -m "chore: add client Dockerfile with dev and prod stages"
```

---

### Task 4: Create Admin Dockerfile

**Files:**
- Create: `docker/admin.Dockerfile`

- [ ] **Step 1: Create `docker/admin.Dockerfile`**

```dockerfile
# ── Dev target ────────────────────────────────────────────────────────────────
FROM node:22-alpine AS dev

WORKDIR /app

COPY admin/package.json admin/package-lock.json ./
RUN npm ci

EXPOSE 5173

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]

# ── Build stage ───────────────────────────────────────────────────────────────
FROM node:22-alpine AS build

WORKDIR /app

COPY admin/package.json admin/package-lock.json ./
RUN npm ci

COPY admin/ .
RUN npm run build-only

# ── Prod target ───────────────────────────────────────────────────────────────
FROM nginx:alpine AS prod

COPY --from=build /app/dist /usr/share/nginx/html

# SPA fallback: serve index.html for all routes
RUN printf 'server {\n\
    listen 80;\n\
    root /usr/share/nginx/html;\n\
    index index.html;\n\
    location / {\n\
        try_files $uri $uri/ /index.html;\n\
    }\n\
}\n' > /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
```

- [ ] **Step 2: Commit**

```bash
git add docker/admin.Dockerfile
git commit -m "chore: add admin Dockerfile with dev and prod stages"
```

---

### Task 5: Create Nginx Dockerfile and config (production)

**Files:**
- Create: `docker/nginx.conf`
- Create: `docker/nginx.Dockerfile`

- [ ] **Step 1: Create `docker/nginx.conf`**

```nginx
upstream api_backend {
    server api:5148;
}

upstream client_frontend {
    server client:3000;
}

upstream admin_frontend {
    server admin:80;
}

# Main site — client + API
server {
    listen 80;
    server_name ${DOMAIN};

    client_max_body_size 50m;

    # API requests
    location /api/ {
        proxy_pass         http://api_backend/api/;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }

    # Scalar API docs
    location /scalar/ {
        proxy_pass         http://api_backend/scalar/;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
    }

    # Everything else — Nuxt SSR
    location / {
        proxy_pass         http://client_frontend;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
    }
}

# Admin panel
server {
    listen 80;
    server_name ${ADMIN_DOMAIN};

    location / {
        proxy_pass         http://admin_frontend;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

- [ ] **Step 2: Create `docker/nginx.Dockerfile`**

```dockerfile
FROM nginx:alpine

# Remove default config
RUN rm /etc/nginx/conf.d/default.conf

COPY docker/nginx.conf /etc/nginx/templates/default.conf.template

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
```

Note: The `nginx:alpine` image auto-expands `${DOMAIN}` and `${ADMIN_DOMAIN}` environment variables from `/etc/nginx/templates/*.template` files at container startup.

- [ ] **Step 3: Commit**

```bash
git add docker/nginx.conf docker/nginx.Dockerfile
git commit -m "chore: add Nginx reverse proxy Dockerfile and config"
```

---

### Task 6: Create Development Docker Compose

**Files:**
- Create: `docker-compose.yml`

- [ ] **Step 1: Create `docker-compose.yml`**

```yaml
services:
  # ── Infrastructure ──────────────────────────────────────────────────────────
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-postgres}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-postgres}
      POSTGRES_DB: ${POSTGRES_DB:-innovayse_dev}
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-postgres}"]
      interval: 5s
      timeout: 3s
      retries: 5

  rabbitmq:
    image: rabbitmq:3-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER:-guest}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD:-guest}
    ports:
      - "5672:5672"
      - "15672:15672"
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  mailhog:
    image: mailhog/mailhog
    ports:
      - "1025:1025"
      - "8025:8025"

  # ── Application ─────────────────────────────────────────────────────────────
  api:
    build:
      context: .
      dockerfile: docker/api.Dockerfile
      target: dev
    ports:
      - "5148:5148"
    volumes:
      - ./backend:/app
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=${POSTGRES_DB:-innovayse_dev};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-postgres}"
      ASPNETCORE_ENVIRONMENT: Development
      Jwt__Secret: ${JWT_SECRET:-change-this-to-a-32-char-min-secret-key-here}
      Jwt__Issuer: ${JWT_ISSUER:-innovayse-api}
      Jwt__Audience: ${JWT_AUDIENCE:-innovayse-clients}
      Smtp__Host: mailhog
      Smtp__Port: 1025
      RabbitMQ__Host: rabbitmq
      RabbitMQ__User: ${RABBITMQ_USER:-guest}
      RabbitMQ__Password: ${RABBITMQ_PASSWORD:-guest}
      Cors__AllowedOrigins__0: http://localhost:3000
      Cors__AllowedOrigins__1: http://localhost:5173
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
      mailhog:
        condition: service_started

  client:
    build:
      context: .
      dockerfile: docker/client.Dockerfile
      target: dev
    ports:
      - "3000:3000"
    volumes:
      - ./client:/app
      - client_node_modules:/app/node_modules
    environment:
      API_URL: http://api:5148
      NUXT_PUBLIC_BASE_URL: http://localhost:3000
      SMTP_HOST: mailhog
      SMTP_PORT: 1025
    depends_on:
      - api

  admin:
    build:
      context: .
      dockerfile: docker/admin.Dockerfile
      target: dev
    ports:
      - "5173:5173"
    volumes:
      - ./admin:/app
      - admin_node_modules:/app/node_modules
    environment:
      VITE_API_BASE: /api
    depends_on:
      - api

volumes:
  pgdata:
  client_node_modules:
  admin_node_modules:
```

- [ ] **Step 2: Test dev compose starts**

Run: `docker compose up -d`
Expected: All 6 services start. Check with `docker compose ps`.

- [ ] **Step 3: Verify API connects to PostgreSQL**

Run: `docker compose logs api --tail 20`
Expected: Serilog startup logs, no connection errors.

- [ ] **Step 4: Verify client serves on port 3000**

Run: `curl -s -o /dev/null -w "%{http_code}" http://localhost:3000`
Expected: `200`

- [ ] **Step 5: Verify admin serves on port 5173**

Run: `curl -s -o /dev/null -w "%{http_code}" http://localhost:5173`
Expected: `200`

- [ ] **Step 6: Verify MailHog UI**

Run: `curl -s -o /dev/null -w "%{http_code}" http://localhost:8025`
Expected: `200`

- [ ] **Step 7: Verify RabbitMQ management UI**

Run: `curl -s -o /dev/null -w "%{http_code}" http://localhost:15672`
Expected: `200`

- [ ] **Step 8: Commit**

```bash
git add docker-compose.yml
git commit -m "chore: add development Docker Compose with hot-reload"
```

---

### Task 7: Create Production Docker Compose

**Files:**
- Create: `docker-compose.prod.yml`

- [ ] **Step 1: Create `docker-compose.prod.yml`**

```yaml
services:
  # ── Infrastructure ──────────────────────────────────────────────────────────
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 5s
      timeout: 3s
      retries: 5
    restart: unless-stopped

  rabbitmq:
    image: rabbitmq:3-management-alpine
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_USER}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_PASSWORD}
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  # ── Application ─────────────────────────────────────────────────────────────
  api:
    build:
      context: .
      dockerfile: docker/api.Dockerfile
      target: prod
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      ASPNETCORE_ENVIRONMENT: Production
      Jwt__Secret: ${JWT_SECRET}
      Jwt__Issuer: ${JWT_ISSUER}
      Jwt__Audience: ${JWT_AUDIENCE}
      Smtp__Host: ${SMTP_HOST}
      Smtp__Port: ${SMTP_PORT}
      Smtp__User: ${SMTP_USER}
      Smtp__Password: ${SMTP_PASSWORD}
      RabbitMQ__Host: rabbitmq
      RabbitMQ__User: ${RABBITMQ_USER}
      RabbitMQ__Password: ${RABBITMQ_PASSWORD}
      Cors__AllowedOrigins__0: https://${DOMAIN}
      Cors__AllowedOrigins__1: https://${ADMIN_DOMAIN}
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
    restart: unless-stopped

  client:
    build:
      context: .
      dockerfile: docker/client.Dockerfile
      target: prod
    environment:
      API_URL: http://api:5148
      NUXT_PUBLIC_BASE_URL: https://${DOMAIN}
    depends_on:
      - api
    restart: unless-stopped

  admin:
    build:
      context: .
      dockerfile: docker/admin.Dockerfile
      target: prod
    depends_on:
      - api
    restart: unless-stopped

  nginx:
    build:
      context: .
      dockerfile: docker/nginx.Dockerfile
    ports:
      - "80:80"
      - "443:443"
    environment:
      DOMAIN: ${DOMAIN}
      ADMIN_DOMAIN: ${ADMIN_DOMAIN}
    depends_on:
      - api
      - client
      - admin
    restart: unless-stopped

volumes:
  pgdata:
```

- [ ] **Step 2: Commit**

```bash
git add docker-compose.prod.yml
git commit -m "chore: add production Docker Compose with Nginx"
```

---

### Task 8: Update manage.sh for Docker

**Files:**
- Modify: `scripts/manage.sh`

- [ ] **Step 1: Replace the entire `scripts/manage.sh`**

Replace the full file with the Docker-aware version below. The script detects whether to use Docker or native mode. Docker is the default; pass `--native` to use the old behavior.

```bash
#!/usr/bin/env bash
# =============================================================================
# Innovayse — Project Manager
# Usage: ./scripts/manage.sh <command> [service] [--native]
#
# Commands:
#   start   [service]   Start one or all services
#   stop    [service]   Stop one or all services
#   restart [service]   Restart one or all services
#   status              Show running status of all services
#   logs    <service>   Tail logs for a service
#   build   [service]   Build one or all services
#   db      <action>    Database actions: migrate | seed | reset | psql
#   test                Run all tests
#   show                Show URLs and credentials
#   help                Show this help message
#
# Modes:
#   (default)           Docker Compose
#   --native            Run natively without Docker
#
# Services:
#   api       C# ASP.NET Core backend  (port 5148)
#   client    Nuxt 4 client portal     (port 3000)
#   admin     Vue 3 admin panel        (port 5173)
#   all       All services (default)
# =============================================================================

set -euo pipefail

# ── Paths ─────────────────────────────────────────────────────────────────────
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND="$ROOT/backend/src/Innovayse.API"
CLIENT="$ROOT/client"
ADMIN="$ROOT/admin"
LOGS_DIR="$ROOT/.logs"
PIDS_DIR="$ROOT/.pids"

# ── Colors ────────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# ── Helpers ───────────────────────────────────────────────────────────────────
info()    { echo -e "${BLUE}[info]${NC}  $*"; }
success() { echo -e "${GREEN}[ok]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[warn]${NC}  $*"; }
error()   { echo -e "${RED}[error]${NC} $*" >&2; }
header()  { echo -e "\n${BOLD}${CYAN}$*${NC}"; }

# ── Parse flags ───────────────────────────────────────────────────────────────
NATIVE=false
ARGS=()
for arg in "$@"; do
    case "$arg" in
        --native) NATIVE=true ;;
        *)        ARGS+=("$arg") ;;
    esac
done
set -- "${ARGS[@]+"${ARGS[@]}"}"

COMMAND="${1:-help}"
shift || true
SERVICE="${1:-all}"

# ── Compose helper ────────────────────────────────────────────────────────────
dc() {
    docker compose -f "$ROOT/docker-compose.yml" "$@"
}

dc_prod() {
    docker compose -f "$ROOT/docker-compose.prod.yml" "$@"
}

# =============================================================================
#  DOCKER MODE
# =============================================================================

docker_start() {
    local svc="${1:-all}"
    header "Starting (Docker): $svc"
    if [[ "$svc" == "all" ]]; then
        dc up -d --build
    else
        dc up -d --build "$svc"
    fi
    success "Services started"
    dc ps
}

docker_stop() {
    local svc="${1:-all}"
    header "Stopping (Docker): $svc"
    if [[ "$svc" == "all" ]]; then
        dc down
    else
        dc stop "$svc"
    fi
    success "Stopped"
}

docker_restart() {
    local svc="${1:-all}"
    header "Restarting (Docker): $svc"
    if [[ "$svc" == "all" ]]; then
        dc down
        dc up -d --build
    else
        dc restart "$svc"
    fi
    success "Restarted"
}

docker_status() {
    header "Innovayse Service Status (Docker)"
    dc ps
}

docker_logs() {
    local svc="${1:-}"
    if [[ -z "$svc" ]]; then
        error "Usage: manage.sh logs <api|client|admin|postgres|rabbitmq|mailhog>"
        exit 1
    fi
    dc logs -f "$svc"
}

docker_build() {
    local svc="${1:-all}"
    header "Building (Docker): $svc"
    if [[ "$svc" == "all" ]]; then
        dc build
    else
        dc build "$svc"
    fi
    success "Build complete"
}

docker_db() {
    local action="${1:-}"
    case "$action" in
        migrate)
            info "Running EF Core migrations..."
            dc exec api dotnet ef database update \
                --project src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj \
                --startup-project src/Innovayse.API/Innovayse.API.csproj
            success "Migrations applied"
            ;;
        reset)
            warn "This will DROP and recreate the database!"
            read -rp "Are you sure? [y/N] " confirm
            if [[ "$confirm" =~ ^[Yy]$ ]]; then
                dc exec api dotnet ef database drop --force \
                    --project src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj \
                    --startup-project src/Innovayse.API/Innovayse.API.csproj
                dc exec api dotnet ef database update \
                    --project src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj \
                    --startup-project src/Innovayse.API/Innovayse.API.csproj
                success "Database reset complete"
            else
                info "Cancelled"
            fi
            ;;
        seed)
            info "Seeding database..."
            dc exec api dotnet run --project src/Innovayse.API/Innovayse.API.csproj --no-launch-profile -- --seed
            success "Seeding complete"
            ;;
        psql)
            info "Connecting to database..."
            dc exec postgres psql -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DB:-innovayse_dev}"
            ;;
        *)
            error "Unknown db action: $action"
            echo "  Available: migrate | reset | seed | psql"
            exit 1
            ;;
    esac
}

docker_test() {
    header "Running Tests (Docker)"
    dc exec api dotnet test --no-build -v minimal
    success "Tests complete"
}

# =============================================================================
#  NATIVE MODE (fallback)
# =============================================================================

mkdir -p "$LOGS_DIR" "$PIDS_DIR"

pid_file() { echo "$PIDS_DIR/$1.pid"; }

save_pid() {
    local name=$1 pid=$2
    echo "$pid" > "$(pid_file "$name")"
}

read_pid() {
    local file
    file="$(pid_file "$1")"
    [[ -f "$file" ]] && cat "$file" || echo ""
}

is_running() {
    local pid
    pid="$(read_pid "$1")"
    [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null
}

kill_service() {
    local name=$1
    local pid
    pid="$(read_pid "$name")"
    if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
        kill "$pid" 2>/dev/null && success "Stopped $name (pid $pid)"
    else
        warn "$name is not running"
    fi
    rm -f "$(pid_file "$name")"
}

native_start_api() {
    if is_running "api"; then
        warn "api is already running (pid $(read_pid api))"
        return
    fi
    local stale
    stale=$(pgrep -f "Innovayse.API" 2>/dev/null || true)
    if [[ -n "$stale" ]]; then
        warn "Killing stale API process(es): $stale"
        echo "$stale" | xargs kill -9 2>/dev/null || true
        sleep 1
    fi
    local port_pid
    port_pid=$(netstat -ano 2>/dev/null | grep ":5148 " | grep "LISTENING" | awk '{print $NF}' | head -1 || true)
    if [[ -n "$port_pid" ]]; then
        warn "Port 5148 in use by PID $port_pid — killing it"
        taskkill //F //PID "$port_pid" 2>/dev/null || kill -9 "$port_pid" 2>/dev/null || true
        sleep 1
    fi
    info "Starting C# API on http://localhost:5148 ..."
    cd "$BACKEND"
    dotnet run --no-launch-profile \
        --urls "http://localhost:5148" \
        > "$LOGS_DIR/api.log" 2>&1 &
    save_pid "api" $!
    success "api started (pid $!) — logs: .logs/api.log"
}

native_start_client() {
    if is_running "client"; then
        warn "client is already running (pid $(read_pid client))"
        return
    fi
    info "Starting Nuxt client on http://localhost:3000 ..."
    cd "$CLIENT"
    npm run dev > "$LOGS_DIR/client.log" 2>&1 &
    save_pid "client" $!
    success "client started (pid $!) — logs: .logs/client.log"
}

native_start_admin() {
    if is_running "admin"; then
        warn "admin is already running (pid $(read_pid admin))"
        return
    fi
    info "Starting Vue admin on http://localhost:5173 ..."
    cd "$ADMIN"
    npm run dev > "$LOGS_DIR/admin.log" 2>&1 &
    save_pid "admin" $!
    success "admin started (pid $!) — logs: .logs/admin.log"
}

native_dispatch_start() {
    local svc="${1:-all}"
    header "Starting (native): $svc"
    case "$svc" in
        api)    native_start_api ;;
        client) native_start_client ;;
        admin)  native_start_admin ;;
        all)    native_start_api; native_start_client; native_start_admin ;;
        *)      error "Unknown service: $svc"; exit 1 ;;
    esac
}

native_dispatch_stop() {
    local svc="${1:-all}"
    header "Stopping (native): $svc"
    case "$svc" in
        api)    kill_service "api" ;;
        client) kill_service "client" ;;
        admin)  kill_service "admin" ;;
        all)    kill_service "api"; kill_service "client"; kill_service "admin" ;;
        *)      error "Unknown service: $svc"; exit 1 ;;
    esac
}

native_dispatch_restart() {
    local svc="${1:-all}"
    native_dispatch_stop "$svc"
    sleep 1
    native_dispatch_start "$svc"
}

native_status() {
    header "Innovayse Service Status (native)"
    for svc in api client admin; do
        if is_running "$svc"; then
            echo -e "  ${GREEN}●${NC} ${BOLD}$svc${NC}  running  (pid $(read_pid "$svc"))"
        else
            echo -e "  ${RED}○${NC} ${BOLD}$svc${NC}  stopped"
        fi
    done

    echo ""
    info "Checking ports..."
    for port in 5148 3000 5173; do
        local result
        result=$(netstat -ano 2>/dev/null | grep ":${port} " | grep "LISTENING" | awk '{print $5}' | head -1 || true)
        if [[ -n "$result" ]]; then
            echo -e "  ${GREEN}●${NC} port $port  in use (pid $result)"
        else
            echo -e "  ${RED}○${NC} port $port  free"
        fi
    done
}

native_logs() {
    local svc="${1:-}"
    if [[ -z "$svc" ]]; then
        error "Usage: manage.sh logs <api|client|admin>"
        exit 1
    fi
    local logfile="$LOGS_DIR/$svc.log"
    if [[ ! -f "$logfile" ]]; then
        error "No log file found for $svc at $logfile"
        exit 1
    fi
    tail -f "$logfile"
}

native_build_api() {
    info "Building C# API..."
    cd "$ROOT/backend"
    dotnet build -c Release
    success "api build complete"
}

native_build_client() {
    info "Building Nuxt client..."
    cd "$CLIENT"
    npm run build
    success "client build complete"
}

native_build_admin() {
    info "Building Vue admin..."
    cd "$ADMIN"
    npm run build
    success "admin build complete"
}

native_dispatch_build() {
    local svc="${1:-all}"
    header "Building (native): $svc"
    case "$svc" in
        api)    native_build_api ;;
        client) native_build_client ;;
        admin)  native_build_admin ;;
        all)    native_build_api; native_build_client; native_build_admin ;;
        *)      error "Unknown service: $svc"; exit 1 ;;
    esac
}

native_db() {
    local action="${1:-}"
    case "$action" in
        migrate)
            info "Running EF Core migrations..."
            cd "$BACKEND"
            dotnet ef database update
            success "Migrations applied"
            ;;
        reset)
            warn "This will DROP and recreate the database!"
            read -rp "Are you sure? [y/N] " confirm
            if [[ "$confirm" =~ ^[Yy]$ ]]; then
                cd "$BACKEND"
                dotnet ef database drop --force
                dotnet ef database update
                success "Database reset complete"
            else
                info "Cancelled"
            fi
            ;;
        seed)
            info "Seeding database..."
            cd "$BACKEND"
            dotnet run --no-launch-profile -- --seed
            success "Seeding complete"
            ;;
        psql)
            info "Connecting to innovayse_dev..."
            PGPASSWORD=password psql -h localhost -U postgres -d innovayse_dev
            ;;
        *)
            error "Unknown db action: $action"
            echo "  Available: migrate | reset | seed | psql"
            exit 1
            ;;
    esac
}

native_test() {
    header "Running Tests (native)"
    info "C# unit tests..."
    cd "$ROOT/backend"
    dotnet test --no-build -v minimal
    success "Tests complete"
}

# =============================================================================
#  SHARED
# =============================================================================

show_info() {
    header "Innovayse — URLs & Credentials"
    echo ""
    echo -e "${BOLD}Services${NC}"
    echo -e "  ${CYAN}API${NC}          http://localhost:5148"
    echo -e "  ${CYAN}API Docs${NC}     http://localhost:5148/scalar/v1"
    echo -e "  ${CYAN}Client${NC}       http://localhost:3000"
    echo -e "  ${CYAN}Admin${NC}        http://localhost:5173"
    echo -e "  ${CYAN}MailHog${NC}      http://localhost:8025"
    echo -e "  ${CYAN}RabbitMQ${NC}     http://localhost:15672"
    echo ""
    echo -e "${BOLD}Credentials${NC}"
    echo -e "  ${YELLOW}Admin${NC}    admin@innovayse.com  /  Admin123!"
    echo -e "  ${YELLOW}Client${NC}   test@example.com     /  Password123!"
    echo ""
    echo -e "${BOLD}Database (PostgreSQL)${NC}"
    echo -e "  Host      localhost:5432"
    echo -e "  Database  innovayse_dev"
    echo -e "  User      postgres"
    echo -e "  Password  (from .env)"
    echo ""
    echo -e "  ${CYAN}psql${NC}  →  ./scripts/manage.sh db psql"
}

show_help() {
    echo -e "${BOLD}${CYAN}Innovayse Project Manager${NC}"
    echo ""
    echo -e "${BOLD}Usage:${NC}  ./scripts/manage.sh <command> [service] [--native]"
    echo ""
    echo -e "${BOLD}Commands:${NC}"
    echo "  start   [service]   Start services (default: all)"
    echo "  stop    [service]   Stop services  (default: all)"
    echo "  restart [service]   Restart services (default: all)"
    echo "  status              Show running status"
    echo "  logs    <service>   Tail logs for a service"
    echo "  build   [service]   Build services (default: all)"
    echo "  db      <action>    migrate | reset | seed | psql"
    echo "  test                Run test suite"
    echo "  show                Show URLs and credentials"
    echo "  help                Show this help"
    echo ""
    echo -e "${BOLD}Modes:${NC}"
    echo "  (default)           Docker Compose"
    echo "  --native            Run natively without Docker"
    echo ""
    echo -e "${BOLD}Services:${NC}  api | client | admin | postgres | rabbitmq | mailhog | all"
    echo ""
    echo -e "${BOLD}Examples:${NC}"
    echo "  ./scripts/manage.sh start"
    echo "  ./scripts/manage.sh start api"
    echo "  ./scripts/manage.sh logs api"
    echo "  ./scripts/manage.sh db migrate"
    echo "  ./scripts/manage.sh status"
    echo "  ./scripts/manage.sh start --native    # skip Docker"
}

# =============================================================================
#  DISPATCH
# =============================================================================

if [[ "$NATIVE" == true ]]; then
    case "$COMMAND" in
        start)   native_dispatch_start   "$SERVICE" ;;
        stop)    native_dispatch_stop    "$SERVICE" ;;
        restart) native_dispatch_restart "$SERVICE" ;;
        status)  native_status ;;
        logs)    native_logs            "$SERVICE" ;;
        build)   native_dispatch_build  "$SERVICE" ;;
        db)      native_db             "$SERVICE" ;;
        test)    native_test ;;
        show)    show_info ;;
        help|--help|-h) show_help ;;
        *)
            error "Unknown command: $COMMAND"
            echo ""
            show_help
            exit 1
            ;;
    esac
else
    case "$COMMAND" in
        start)   docker_start   "$SERVICE" ;;
        stop)    docker_stop    "$SERVICE" ;;
        restart) docker_restart "$SERVICE" ;;
        status)  docker_status ;;
        logs)    docker_logs    "$SERVICE" ;;
        build)   docker_build   "$SERVICE" ;;
        db)      docker_db     "$SERVICE" ;;
        test)    docker_test ;;
        show)    show_info ;;
        help|--help|-h) show_help ;;
        *)
            error "Unknown command: $COMMAND"
            echo ""
            show_help
            exit 1
            ;;
    esac
fi
```

- [ ] **Step 2: Verify script syntax**

Run: `bash -n scripts/manage.sh`
Expected: No output (no syntax errors).

- [ ] **Step 3: Test help output**

Run: `./scripts/manage.sh help`
Expected: Shows help with Docker and native modes documented.

- [ ] **Step 4: Commit**

```bash
git add scripts/manage.sh
git commit -m "feat: update manage.sh with Docker Compose support and --native fallback"
```

---

### Task 9: Smoke Test — Full Stack

- [ ] **Step 1: Ensure .env exists**

Run: `cp .env.example .env` (if not already done)

- [ ] **Step 2: Start the full stack**

Run: `./scripts/manage.sh start`
Expected: All 6 services start. `docker compose ps` shows them as `Up`.

- [ ] **Step 3: Wait for services to be ready**

Run: `sleep 15 && ./scripts/manage.sh status`
Expected: All services running.

- [ ] **Step 4: Test API health**

Run: `curl -s http://localhost:5148/scalar/v1 | head -5`
Expected: HTML response (Scalar API docs page).

- [ ] **Step 5: Test client**

Run: `curl -s -o /dev/null -w "%{http_code}" http://localhost:3000`
Expected: `200`

- [ ] **Step 6: Test admin**

Run: `curl -s -o /dev/null -w "%{http_code}" http://localhost:5173`
Expected: `200`

- [ ] **Step 7: Test MailHog**

Run: `curl -s -o /dev/null -w "%{http_code}" http://localhost:8025`
Expected: `200`

- [ ] **Step 8: Test RabbitMQ**

Run: `curl -s -o /dev/null -w "%{http_code}" http://localhost:15672`
Expected: `200`

- [ ] **Step 9: Test database connection**

Run: `./scripts/manage.sh db psql` then `\dt` then `\q`
Expected: Lists tables (or empty if no migrations yet).

- [ ] **Step 10: Run migrations**

Run: `./scripts/manage.sh db migrate`
Expected: Migrations applied successfully.

- [ ] **Step 11: Stop everything**

Run: `./scripts/manage.sh stop`
Expected: All services stopped.

- [ ] **Step 12: Final commit**

```bash
git add -A
git commit -m "chore: complete Docker setup — dev and prod compose, Dockerfiles, Nginx, manage.sh"
```
