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
                --startup-project src/Innovayse.API/Innovayse.API.csproj \
                --connection "Host=postgres;Port=5432;Database=${POSTGRES_DB:-innovayse_dev};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-postgres}"
            success "Migrations applied"
            ;;
        reset)
            warn "This will DROP and recreate the database!"
            read -rp "Are you sure? [y/N] " confirm
            if [[ "$confirm" =~ ^[Yy]$ ]]; then
                dc exec api dotnet ef database drop --force \
                    --project src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj \
                    --startup-project src/Innovayse.API/Innovayse.API.csproj \
                    --connection "Host=postgres;Port=5432;Database=${POSTGRES_DB:-innovayse_dev};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-postgres}"
                dc exec api dotnet ef database update \
                    --project src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj \
                    --startup-project src/Innovayse.API/Innovayse.API.csproj \
                    --connection "Host=postgres;Port=5432;Database=${POSTGRES_DB:-innovayse_dev};Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD:-postgres}"
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
