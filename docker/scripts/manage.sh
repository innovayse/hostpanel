#!/usr/bin/env bash
# =============================================================================
# HostPanel project manager
# Usage: ./docker/scripts/manage.sh <command> [service] [--native]
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
#   api       C# ASP.NET Core backend  (port API_PORT in docker/.env)
#   client    Nuxt 4 client portal     (port CLIENT_PORT in docker/.env)
#   admin     Vue 3 admin panel        (Vite dev server)
#   all       All services (default)
# =============================================================================

set -euo pipefail

# ── Paths ─────────────────────────────────────────────────────────────────────
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BACKEND="$ROOT/backend/src/Innovayse.Hostpanel.API"
CLIENT="$ROOT/client"
ADMIN="$ROOT/admin"
LOGS_DIR="$ROOT/.logs"
PIDS_DIR="$ROOT/.pids"
# Every port, database name and credential below comes from here, never from a literal in this script.
ENV_FILE="$ROOT/docker/.env"
# Where the services answer on this machine.
LOCAL_HOST="localhost"
# The admin's Vite dev server port: Vite's own default, not a docker/.env key (the dev compose file maps
# ADMIN_HOST_PORT onto it).
ADMIN_DEV_PORT=5173

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

# Prints the value of KEY in docker/.env and fails when the file or the key is missing or empty.
# The file is read, not sourced: values such as ALLOWED_HOSTS hold characters the shell would act on.
need_env() {
    local line
    if [[ ! -f "$ENV_FILE" ]]; then
        error "$ENV_FILE not found - copy docker/.env.example to docker/.env"
        exit 1
    fi
    line="$(grep -E "^$1=" "$ENV_FILE" | tail -n 1 | tr -d '\r' || true)"
    if [[ -z "${line#*=}" ]]; then
        error "$1 is not set in $ENV_FILE"
        exit 1
    fi
    printf '%s' "${line#*=}"
}

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
    docker compose -f "$ROOT/docker/docker-compose.shared.yml" -f "$ROOT/docker/docker-compose.dev.yml" "$@"
}

# Compose service key for a short name (api -> hostpanel-api).
cs() {
    case "$1" in
        api|client|admin) echo "hostpanel-$1" ;;
        *) echo "$1" ;;
    esac
}

dc_prod() {
    docker compose -f "$ROOT/docker/docker-compose.shared.yml" -f "$ROOT/docker/docker-compose.prod.yml" "$@"
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
        dc up -d --build "$(cs "$svc")"
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
        dc stop "$(cs "$svc")"
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
        dc restart "$(cs "$svc")"
    fi
    success "Restarted"
}

docker_status() {
    header "HostPanel Service Status (Docker)"
    dc ps
}

docker_logs() {
    local svc="${1:-}"
    if [[ -z "$svc" ]]; then
        error "Usage: manage.sh logs <api|client|admin|postgres|rabbitmq|mailhog>"
        exit 1
    fi
    dc logs -f "$(cs "$svc")"
}

docker_build() {
    local svc="${1:-all}"
    header "Building (Docker): $svc"
    if [[ "$svc" == "all" ]]; then
        dc build
    else
        dc build "$(cs "$svc")"
    fi
    success "Build complete"
}

docker_db() {
    local action="${1:-}"
    case "$action" in
        migrate)
            info "Restoring packages..."
            dc exec hostpanel-api dotnet restore
            info "Running EF Core migrations..."
            dc exec hostpanel-api sh -c 'dotnet ef database update --project src/Innovayse.Hostpanel.Infrastructure/Innovayse.Hostpanel.Infrastructure.csproj --startup-project src/Innovayse.Hostpanel.API/Innovayse.Hostpanel.API.csproj --connection "$ConnectionStrings__DefaultConnection"'
            success "Migrations applied"
            ;;
        reset)
            warn "This will DROP and recreate the database!"
            read -rp "Are you sure? [y/N] " confirm
            if [[ "$confirm" =~ ^[Yy]$ ]]; then
                dc exec hostpanel-api dotnet restore
                dc exec hostpanel-api sh -c 'dotnet ef database drop --force --project src/Innovayse.Hostpanel.Infrastructure/Innovayse.Hostpanel.Infrastructure.csproj --startup-project src/Innovayse.Hostpanel.API/Innovayse.Hostpanel.API.csproj --connection "$ConnectionStrings__DefaultConnection"'
                dc exec hostpanel-api sh -c 'dotnet ef database update --project src/Innovayse.Hostpanel.Infrastructure/Innovayse.Hostpanel.Infrastructure.csproj --startup-project src/Innovayse.Hostpanel.API/Innovayse.Hostpanel.API.csproj --connection "$ConnectionStrings__DefaultConnection"'
                success "Database reset complete"
            else
                info "Cancelled"
            fi
            ;;
        seed)
            info "Seeding database..."
            dc exec hostpanel-api dotnet run --project src/Innovayse.Hostpanel.API/Innovayse.Hostpanel.API.csproj --no-launch-profile -- --seed
            success "Seeding complete"
            ;;
        psql)
            info "Connecting to database..."
            dc exec hostpanel-db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
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
    dc exec hostpanel-api dotnet test --no-build -v minimal
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

# Prints the PID listening on TCP port $1, or nothing when the port is free. ss on Linux, lsof where ss is
# missing (macOS), netstat under Git Bash on Windows.
port_owner() {
    local port="$1"
    if command -v ss >/dev/null 2>&1; then
        ss -ltnpH "sport = :${port}" 2>/dev/null | sed -n 's/.*pid=\([0-9][0-9]*\).*/\1/p' | head -n 1
    elif command -v lsof >/dev/null 2>&1; then
        lsof -t -iTCP:"${port}" -sTCP:LISTEN 2>/dev/null | head -n 1
    else
        netstat -ano 2>/dev/null | grep ":${port} " | grep "LISTENING" | awk '{print $NF}' | head -n 1
    fi
}

# Asks to stop the process that holds a port the API needs. Nothing is stopped without a typed yes, and
# only with an ordinary termination request: a process that ignores it is reported, never force-killed.
# $1 port, $2 pid
confirm_free_port() {
    local port="$1" pid="$2" answer=""
    warn "Port ${port} is in use by PID ${pid}"
    if [[ ! -t 0 ]]; then
        error "Cannot ask for confirmation without a terminal. Stop PID ${pid} yourself, or pick another API_PORT."
        exit 1
    fi
    read -rp "Ask PID ${pid} to stop so the API can use port ${port}? [y/N] " answer
    if [[ ! "$answer" =~ ^[Yy]$ ]]; then
        error "Port ${port} is busy; not starting the API."
        exit 1
    fi
    kill "$pid" 2>/dev/null || taskkill //PID "$pid" 2>/dev/null || true
    sleep 2
    if [[ -n "$(port_owner "$port")" ]]; then
        error "PID ${pid} is still holding port ${port}. Stop it yourself, then start again."
        exit 1
    fi
}

native_start_api() {
    if is_running "api"; then
        warn "api is already running (pid $(read_pid api))"
        return
    fi
    local api_port port_pid
    api_port="$(need_env API_PORT)" || exit 1
    port_pid="$(port_owner "$api_port")"
    if [[ -n "$port_pid" ]]; then
        confirm_free_port "$api_port" "$port_pid"
    fi
    info "Starting C# API on http://${LOCAL_HOST}:${api_port} ..."
    cd "$BACKEND"
    dotnet run --no-launch-profile \
        --urls "http://${LOCAL_HOST}:${api_port}" \
        > "$LOGS_DIR/api.log" 2>&1 &
    save_pid "api" $!
    success "api started (pid $!) — logs: .logs/api.log"
}

native_start_client() {
    if is_running "client"; then
        warn "client is already running (pid $(read_pid client))"
        return
    fi
    local client_port
    client_port="$(need_env CLIENT_PORT)" || exit 1
    info "Starting Nuxt client on http://${LOCAL_HOST}:${client_port} ..."
    cd "$CLIENT"
    NITRO_PORT="$client_port" npm run dev > "$LOGS_DIR/client.log" 2>&1 &
    save_pid "client" $!
    success "client started (pid $!) — logs: .logs/client.log"
}

native_start_admin() {
    if is_running "admin"; then
        warn "admin is already running (pid $(read_pid admin))"
        return
    fi
    info "Starting Vue admin on http://${LOCAL_HOST}:${ADMIN_DEV_PORT} ..."
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
    header "HostPanel Service Status (native)"
    for svc in api client admin; do
        if is_running "$svc"; then
            echo -e "  ${GREEN}●${NC} ${BOLD}$svc${NC}  running  (pid $(read_pid "$svc"))"
        else
            echo -e "  ${RED}○${NC} ${BOLD}$svc${NC}  stopped"
        fi
    done

    echo ""
    info "Checking ports..."
    local api_port client_port
    api_port="$(need_env API_PORT)" || exit 1
    client_port="$(need_env CLIENT_PORT)" || exit 1
    for port in "$api_port" "$client_port" "$ADMIN_DEV_PORT"; do
        local result
        result="$(port_owner "$port")"
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
            dotnet tool restore
            dotnet ef database update
            success "Migrations applied"
            ;;
        reset)
            warn "This will DROP and recreate the database!"
            read -rp "Are you sure? [y/N] " confirm
            if [[ "$confirm" =~ ^[Yy]$ ]]; then
                cd "$BACKEND"
                dotnet tool restore
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
            info "Connecting to the database..."
            local pg_port pg_user pg_db
            pg_port="$(need_env POSTGRES_PORT)" || exit 1
            pg_user="$(need_env POSTGRES_USER)" || exit 1
            pg_db="$(need_env POSTGRES_DB)" || exit 1
            psql -h "$LOCAL_HOST" -p "$pg_port" -U "$pg_user" -d "$pg_db"
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
    # Every port comes from docker/.env: the published host ports for the Docker stack, the in-container
    # ones for a native run (which has no published mapping).
    local api_port client_port admin_port db_port db_name db_user mailhog_port rabbit_port
    if [[ "$NATIVE" == true ]]; then
        api_port="$(need_env API_PORT)" || exit 1
        client_port="$(need_env CLIENT_PORT)" || exit 1
        admin_port="$ADMIN_DEV_PORT"
        db_port="$(need_env POSTGRES_PORT)" || exit 1
    else
        api_port="$(need_env API_HOST_PORT)" || exit 1
        client_port="$(need_env CLIENT_HOST_PORT)" || exit 1
        admin_port="$(need_env ADMIN_HOST_PORT)" || exit 1
        db_port="$(need_env POSTGRES_HOST_PORT)" || exit 1
    fi
    db_name="$(need_env POSTGRES_DB)" || exit 1
    db_user="$(need_env POSTGRES_USER)" || exit 1
    if [[ "$NATIVE" != true ]]; then
        mailhog_port="$(need_env MAILHOG_UI_HOST_PORT)" || exit 1
        rabbit_port="$(need_env RABBITMQ_MANAGEMENT_HOST_PORT)" || exit 1
    fi
    header "HostPanel URLs and credentials"
    echo ""
    echo -e "${BOLD}Services${NC}"
    echo -e "  ${CYAN}API${NC}          http://${LOCAL_HOST}:${api_port}"
    echo -e "  ${CYAN}API Docs${NC}     http://${LOCAL_HOST}:${api_port}/swagger"
    echo -e "  ${CYAN}Client${NC}       http://${LOCAL_HOST}:${client_port}"
    echo -e "  ${CYAN}Admin${NC}        http://${LOCAL_HOST}:${admin_port}"
    if [[ "$NATIVE" != true ]]; then
        echo -e "  ${CYAN}MailHog${NC}      http://${LOCAL_HOST}:${mailhog_port}"
        echo -e "  ${CYAN}RabbitMQ${NC}     http://${LOCAL_HOST}:${rabbit_port}"
    fi
    echo ""
    echo -e "${BOLD}Credentials${NC}"
    echo -e "  ${YELLOW}Admin, Client${NC}    seeded by DevDataSeeder (see its source for the dev logins)"
    echo ""
    echo -e "${BOLD}Database (PostgreSQL)${NC}"
    echo -e "  Host      ${LOCAL_HOST}:${db_port}"
    echo -e "  Database  ${db_name}"
    echo -e "  User      ${db_user}"
    echo -e "  Password  (from .env)"
    echo ""
    echo -e "  ${CYAN}psql${NC}  →  ./docker/scripts/manage.sh db psql"
}

show_help() {
    echo -e "${BOLD}${CYAN}HostPanel Project Manager${NC}"
    echo ""
    echo -e "${BOLD}Usage:${NC}  ./docker/scripts/manage.sh <command> [service] [--native]"
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
    echo "  ./docker/scripts/manage.sh start"
    echo "  ./docker/scripts/manage.sh start api"
    echo "  ./docker/scripts/manage.sh logs api"
    echo "  ./docker/scripts/manage.sh db migrate"
    echo "  ./docker/scripts/manage.sh status"
    echo "  ./docker/scripts/manage.sh start --native    # skip Docker"
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
