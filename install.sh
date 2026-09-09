#!/usr/bin/env bash
# =============================================================================
# Hostpanel — One-command installer for Ubuntu 22.04 / 24.04
# Usage: bash install.sh
# =============================================================================
set -euo pipefail

# ── Colors ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
BLUE='\033[0;34m'; CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

info()    { echo -e "${BLUE}[info]${NC}  $*"; }
success() { echo -e "${GREEN}[ok]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[warn]${NC}  $*"; }
error()   { echo -e "${RED}[error]${NC} $*"; exit 1; }
step()    { echo -e "\n${BOLD}${CYAN}▶ $*${NC}"; }

# ── Root check ────────────────────────────────────────────────────────────────
[[ $EUID -ne 0 ]] && error "Run as root: sudo bash install.sh"

# ── Banner ────────────────────────────────────────────────────────────────────
echo -e "${BOLD}"
echo "  ██╗  ██╗ ██████╗ ███████╗████████╗██████╗  █████╗ ███╗   ██╗███████╗██╗     "
echo "  ██║  ██║██╔═══██╗██╔════╝╚══██╔══╝██╔══██╗██╔══██╗████╗  ██║██╔════╝██║     "
echo "  ███████║██║   ██║███████╗   ██║   ██████╔╝███████║██╔██╗ ██║█████╗  ██║     "
echo "  ██╔══██║██║   ██║╚════██║   ██║   ██╔═══╝ ██╔══██║██║╚██╗██║██╔══╝  ██║     "
echo "  ██║  ██║╚██████╔╝███████║   ██║   ██║     ██║  ██║██║ ╚████║███████╗███████╗"
echo "  ╚═╝  ╚═╝ ╚═════╝ ╚══════╝   ╚═╝   ╚═╝     ╚═╝  ╚═╝╚═╝  ╚═══╝╚══════╝╚══════╝"
echo -e "${NC}"
echo -e "  ${CYAN}Hosting Panel Installer${NC} — github.com/innovayse/hostpanel"
echo ""

# ── Collect configuration ─────────────────────────────────────────────────────
step "Configuration"

read -rp "  Domain (e.g. hostpanel.example.com): " DOMAIN
[[ -z "$DOMAIN" ]] && error "Domain is required"

read -rp "  SSL email (for Let's Encrypt): " SSL_EMAIL
[[ -z "$SSL_EMAIL" ]] && error "Email is required"

read -rp "  Install path [/var/www/hostpanel]: " INSTALL_DIR
INSTALL_DIR="${INSTALL_DIR:-/var/www/hostpanel}"

echo ""
read -rp "  Seed demo data? (y/n) [y]: " SEED_DEMO
SEED_DEMO="${SEED_DEMO:-y}"

echo ""
echo -e "  ${BOLD}Summary:${NC}"
echo "    Domain:       https://$DOMAIN"
echo "    Admin panel:  https://$DOMAIN/admin"
echo "    Install path: $INSTALL_DIR"
echo "    Demo data:    $SEED_DEMO"
echo ""
read -rp "  Proceed? (y/n): " CONFIRM
[[ "$CONFIRM" != "y" && "$CONFIRM" != "Y" ]] && echo "Aborted." && exit 0

# ── Generate secrets ──────────────────────────────────────────────────────────
generate_password() { tr -dc 'A-Za-z0-9!@#$%' < /dev/urandom | head -c 24; }
generate_secret()   { tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 48; }

PG_PASS=$(generate_password)
RMQ_PASS=$(generate_password)
JWT_SECRET=$(generate_secret)

# ── Install dependencies ──────────────────────────────────────────────────────
step "Installing system dependencies"

export DEBIAN_FRONTEND=noninteractive
apt-get update -qq

# Docker
if ! command -v docker &>/dev/null; then
  info "Installing Docker..."
  curl -fsSL https://get.docker.com | sh
  systemctl enable --now docker
  success "Docker installed"
else
  success "Docker already installed"
fi

# Nginx
if ! command -v nginx &>/dev/null; then
  info "Installing Nginx..."
  apt-get install -y -qq nginx
  systemctl enable --now nginx
  success "Nginx installed"
else
  success "Nginx already installed"
fi

# Certbot
if ! command -v certbot &>/dev/null; then
  info "Installing Certbot..."
  apt-get install -y -qq certbot python3-certbot-nginx
  success "Certbot installed"
else
  success "Certbot already installed"
fi

# Git
if ! command -v git &>/dev/null; then
  apt-get install -y -qq git
fi

# ── Clone repository ──────────────────────────────────────────────────────────
step "Cloning repository"

if [[ -d "$INSTALL_DIR/.git" ]]; then
  info "Repository already exists, pulling latest..."
  git -C "$INSTALL_DIR" pull origin main
else
  git clone https://github.com/innovayse/hostpanel.git "$INSTALL_DIR"
fi
success "Repository ready at $INSTALL_DIR"

# ── Create .env ───────────────────────────────────────────────────────────────
step "Creating .env"

cat > "$INSTALL_DIR/.env" <<EOF
# PostgreSQL
POSTGRES_USER=hostpanel
POSTGRES_PASSWORD=${PG_PASS}
POSTGRES_DB=hostpanel_prod

# RabbitMQ
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USER=hostpanel
RABBITMQ_PASSWORD=${RMQ_PASS}

# JWT
JWT_SECRET=${JWT_SECRET}
JWT_ISSUER=innovayse-api
JWT_AUDIENCE=innovayse-clients

# SMTP (configure after install)
# SMTP_ENCRYPTION names one of tls (STARTTLS, port 587), ssl (implicit TLS, port 465) or
# none (a local catcher). The API refuses to start without it rather than guess from the
# port, so it is filled in to match the 587 above and edited alongside the host.
SMTP_HOST=
SMTP_PORT=587
SMTP_USERNAME=
SMTP_PASSWORD=
SMTP_ENCRYPTION=tls
SMTP_FROM=noreply@${DOMAIN}
SMTP_FROM_NAME=${DOMAIN}

# Domains
DOMAIN=${DOMAIN}
ADMIN_DOMAIN=admin.${DOMAIN}
EOF

chmod 600 "$INSTALL_DIR/.env"
success ".env created"


# ── Fix Dockerfile for duplicate plugin.json ──────────────────────────────────
step "Patching api.Dockerfile"

sed -i 's|RUN dotnet publish src/Innovayse.API/Innovayse.API.csproj -c Release -o /out --no-restore$|RUN dotnet publish src/Innovayse.API/Innovayse.API.csproj -c Release -o /out --no-restore /p:ErrorOnDuplicatePublishOutputFiles=false|' \
  "$INSTALL_DIR/docker/api.Dockerfile"
success "api.Dockerfile patched"

# ── Build Docker images ────────────────────────────────────────────────────────
step "Building Docker images (this takes ~5-10 minutes)"

cd "$INSTALL_DIR"
# docker-compose.server.yml ships with the repo: it disables the bundled nginx and
# publishes the services on loopback for the host nginx to proxy.
COMPOSE="docker compose -f docker-compose.prod.yml -f docker-compose.server.yml --env-file .env"
$COMPOSE build
success "Images built"

# ── Start services ─────────────────────────────────────────────────────────────
step "Starting services"

$COMPOSE up -d
info "Waiting for database to be ready..."
sleep 10
success "Services started"

# ── Run database migrations ────────────────────────────────────────────────────
step "Running database migrations"

# Fix permissions for SDK container
chmod -R 777 "$INSTALL_DIR/backend/"

COMPOSE_PROJECT=$(basename "$INSTALL_DIR")
NETWORK="${COMPOSE_PROJECT}_default"

# Resolved from compose, not guessed: these services declare container_name, so the
# <project>-<service>-<index> form does not apply to them.
DB_CONTAINER=$($COMPOSE ps -q hostpanel-db)
API_CONTAINER=$($COMPOSE ps -q hostpanel-api)
API_IMAGE=$($COMPOSE images -q hostpanel-api | head -1)

# The address the API and the migration container dial is the service name, which is a
# DNS alias on the compose network whatever the container ends up being called.
DB_HOST=hostpanel-db

docker run --rm \
  --network "$NETWORK" \
  -v "$INSTALL_DIR/backend:/app" \
  -w /app \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  sh -c "dotnet tool install --global dotnet-ef && \
         export PATH=\"\$PATH:/root/.dotnet/tools\" && \
         dotnet restore Innovayse.Backend.sln -q && \
         dotnet-ef database update --project src/Innovayse.API/Innovayse.API.csproj \
         --connection 'Host=${DB_HOST};Port=5432;Database=hostpanel_prod;Username=hostpanel;Password=${PG_PASS}'" 2>&1

success "Migrations applied"

# ── Restart API ────────────────────────────────────────────────────────────────
step "Restarting API"

docker restart "$API_CONTAINER"
sleep 8
success "API restarted"

# ── Seed superadmin ────────────────────────────────────────────────────────────
step "Creating superadmin account"

# Wait for API to be ready
for i in {1..15}; do
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5148/api/health 2>/dev/null || echo "000")
  [[ "$STATUS" == "200" ]] && break
  sleep 2
done

ADMIN_PASS=$(generate_password)

SETUP_RESULT=$(curl -s -X POST http://127.0.0.1:5148/api/auth/setup \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"superadmin@${DOMAIN}\",\"password\":\"${ADMIN_PASS}\",\"firstName\":\"Super\",\"lastName\":\"Admin\"}" 2>/dev/null)

# Confirm email in DB
docker exec "$DB_CONTAINER" psql -U hostpanel -d hostpanel_prod \
  -c "UPDATE \"AspNetUsers\" SET \"EmailConfirmed\" = true WHERE \"Email\" = 'superadmin@${DOMAIN}';" &>/dev/null

success "Superadmin created"

# ── Seed demo data (optional) ──────────────────────────────────────────────────
if [[ "$SEED_DEMO" == "y" || "$SEED_DEMO" == "Y" ]]; then
  step "Seeding demo data"

  SEED_CONTAINER="${COMPOSE_PROJECT}-api-run-seed"
  docker run --rm \
    --name "$SEED_CONTAINER" \
    --network "$NETWORK" \
    -e "ConnectionStrings__DefaultConnection=Host=${DB_HOST};Port=5432;Database=hostpanel_prod;Username=hostpanel;Password=${PG_PASS}" \
    -e "ASPNETCORE_ENVIRONMENT=Development" \
    -e "Jwt__Secret=${JWT_SECRET}" \
    -e "Jwt__Issuer=innovayse-api" \
    -e "Jwt__Audience=innovayse-clients" \
    -e "RabbitMQ__Host=rabbitmq" \
    -e "RabbitMQ__Port=5672" \
    -e "RabbitMQ__User=hostpanel" \
    -e "RabbitMQ__Password=${RMQ_PASS}" \
    -e "Smtp__Encryption=none" \
    "$API_IMAGE" \
    sh -c "timeout 30 dotnet Innovayse.API.dll --urls http://0.0.0.0:5148 2>&1 | grep -E 'Seed|seeded|INF|clients' || true" &>/dev/null || true

  # Confirm all demo user emails
  docker exec "$DB_CONTAINER" psql -U hostpanel -d hostpanel_prod \
    -c 'UPDATE "AspNetUsers" SET "EmailConfirmed" = true;' &>/dev/null

  success "Demo data seeded"
fi

# ── Configure Nginx ────────────────────────────────────────────────────────────
step "Configuring Nginx"

cat > "/etc/nginx/sites-available/${DOMAIN}" <<EOF
server {
    listen 80;
    server_name ${DOMAIN};

    client_max_body_size 50m;

    # Admin panel — strip /admin prefix before proxying
    location /admin/ {
        proxy_pass         http://127.0.0.1:5173/;
        proxy_http_version 1.1;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
    }

    location = /admin {
        return 301 /admin/;
    }

    # API
    location /api/ {
        proxy_pass         http://127.0.0.1:5148/api/;
        proxy_http_version 1.1;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
    }

    # API Docs
    location /scalar/ {
        proxy_pass         http://127.0.0.1:5148/scalar/;
        proxy_http_version 1.1;
        proxy_set_header   Host \$host;
    }

    # Nuxt SSR client (catch-all)
    location / {
        proxy_pass         http://127.0.0.1:3200;
        proxy_http_version 1.1;
        proxy_set_header   Host \$host;
        proxy_set_header   X-Real-IP \$remote_addr;
        proxy_set_header   X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto \$scheme;
        proxy_set_header   Upgrade \$http_upgrade;
        proxy_set_header   Connection "upgrade";
    }
}
EOF

ln -sf "/etc/nginx/sites-available/${DOMAIN}" "/etc/nginx/sites-enabled/${DOMAIN}"
nginx -t && systemctl reload nginx
success "Nginx configured"

# ── SSL Certificate ────────────────────────────────────────────────────────────
step "Obtaining SSL certificate"

if certbot --nginx -d "$DOMAIN" --non-interactive --agree-tos -m "$SSL_EMAIL" 2>&1; then
  success "SSL certificate obtained"
else
  warn "SSL failed — DNS may not be pointing to this server yet."
  warn "After DNS is set up, run: certbot --nginx -d ${DOMAIN} -m ${SSL_EMAIL} --agree-tos"
fi

# ── Done ───────────────────────────────────────────────────────────────────────
echo ""
echo -e "${BOLD}${GREEN}════════════════════════════════════════════════════${NC}"
echo -e "${BOLD}${GREEN}  ✓  Hostpanel installed successfully!${NC}"
echo -e "${BOLD}${GREEN}════════════════════════════════════════════════════${NC}"
echo ""
echo -e "  ${BOLD}URLs:${NC}"
echo -e "    Client:       ${CYAN}https://${DOMAIN}${NC}"
echo -e "    Admin panel:  ${CYAN}https://${DOMAIN}/admin${NC}"
echo -e "    API docs:     ${CYAN}https://${DOMAIN}/scalar${NC}"
echo ""
echo -e "  ${BOLD}Superadmin credentials:${NC}"
echo -e "    Email:    ${CYAN}superadmin@${DOMAIN}${NC}"
echo -e "    Password: ${CYAN}${ADMIN_PASS}${NC}"
echo ""
echo -e "  ${BOLD}Install path:${NC} ${INSTALL_DIR}"
echo ""
echo -e "  ${YELLOW}Save the credentials above — they won't be shown again!${NC}"
echo ""