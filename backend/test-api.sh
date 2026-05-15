#!/bin/bash
# Innovayse API curl test script
# Usage: ./test-api.sh
# Requires: curl, jq
# Make sure the API is running: cd backend/src/Innovayse.API && dotnet run

BASE="http://localhost:5148"
COOKIE_JAR="cookies.txt"
TOKEN=""

echo "=================================================="
echo "  Innovayse API Curl Tests"
echo "=================================================="

# Helper functions
pass() { echo "  PASS: $1"; }
fail() { echo "  FAIL: $1 (status: $2)"; }

check() {
    local name=$1
    local expected=$2
    local actual=$3
    if [ "$actual" -eq "$expected" ] 2>/dev/null; then
        pass "$name"
    else
        fail "$name" "$actual"
    fi
}

# ── AUTH ──────────────────────────────────────────────
echo ""
echo "── Auth ──────────────────────────────────────────"

# Register
echo "  -> Register"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/register" \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com","password":"Password123!","firstName":"Test","lastName":"User"}' \
    -c "$COOKIE_JAR")
check "POST /api/auth/register" 200 "$STATUS"

# Login
echo "  -> Login"
RESPONSE=$(curl -s -X POST "$BASE/api/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com","password":"Password123!"}' \
    -c "$COOKIE_JAR")
STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE/api/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"email":"test@example.com","password":"Password123!"}' \
    -c "$COOKIE_JAR")
TOKEN=$(echo "$RESPONSE" | jq -r '.accessToken // .token // empty')
check "POST /api/auth/login" 200 "$STATUS"
if [ -n "$TOKEN" ]; then
    echo "  -> Token: ${TOKEN:0:30}..."
else
    echo "  -> Token not received — protected endpoint tests will fail"
fi

AUTH_HEADER="Authorization: Bearer $TOKEN"

# ── CLIENT PROFILE ─────────────────────────────────────
echo ""
echo "── Client Profile ────────────────────────────────"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/clients/me" -H "$AUTH_HEADER")
check "GET /api/clients/me" 200 "$STATUS"

# ── DOMAINS ────────────────────────────────────────────
echo ""
echo "── Domains ───────────────────────────────────────"
DOMAIN_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/domains/check?name=example.com" -H "$AUTH_HEADER")
if [ "$DOMAIN_STATUS" -eq 200 ] || [ "$DOMAIN_STATUS" -eq 500 ] 2>/dev/null; then
    echo "  INFO: GET /api/domains/check -> $DOMAIN_STATUS (500 expected if registrar not configured)"
else
    fail "GET /api/domains/check" "$DOMAIN_STATUS"
fi

STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/me/domains" -H "$AUTH_HEADER")
check "GET /api/me/domains" 200 "$STATUS"

# ── BILLING ────────────────────────────────────────────
echo ""
echo "── Billing ───────────────────────────────────────"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/me/invoices" -H "$AUTH_HEADER")
check "GET /api/me/invoices" 200 "$STATUS"

# ── SERVICES ───────────────────────────────────────────
echo ""
echo "── Services ──────────────────────────────────────"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/me/services" -H "$AUTH_HEADER")
check "GET /api/me/services" 200 "$STATUS"

# ── SUPPORT ────────────────────────────────────────────
echo ""
echo "── Support ───────────────────────────────────────"
# NOTE: Tickets require DB — these will return 500 if PostgreSQL is not running
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/me/tickets" -H "$AUTH_HEADER")
if [ "$STATUS" -eq 200 ] 2>/dev/null; then
    pass "GET /api/me/tickets"
elif [ "$STATUS" -eq 500 ] 2>/dev/null; then
    echo "  INFO: GET /api/me/tickets -> 500 (DB not reachable — expected in dev without PostgreSQL)"
else
    fail "GET /api/me/tickets" "$STATUS"
fi

# Create ticket
TICKET_HTTP=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE/api/me/tickets" \
    -H "Content-Type: application/json" \
    -H "$AUTH_HEADER" \
    -d '{"subject":"Test Ticket","message":"This is a test ticket","departmentId":1,"priority":"Medium"}')
if [ "$TICKET_HTTP" -eq 201 ] || [ "$TICKET_HTTP" -eq 200 ] 2>/dev/null; then
    pass "POST /api/me/tickets"
else
    fail "POST /api/me/tickets" "$TICKET_HTTP"
fi

# ── KNOWLEDGEBASE (public) ─────────────────────────────
echo ""
echo "── Knowledgebase (public) ────────────────────────"
# NOTE: Knowledgebase requires DB — will return 500 if PostgreSQL is not running
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/knowledgebase")
if [ "$STATUS" -eq 200 ] 2>/dev/null; then
    pass "GET /api/knowledgebase (no auth)"
elif [ "$STATUS" -eq 500 ] 2>/dev/null; then
    echo "  INFO: GET /api/knowledgebase -> 500 (DB not reachable — expected without PostgreSQL)"
else
    fail "GET /api/knowledgebase (no auth)" "$STATUS"
fi

# ── ADMIN ──────────────────────────────────────────────
echo ""
echo "── Admin (requires Admin role) ───────────────────"

# Login as admin — update with actual admin credentials
ADMIN_RESPONSE=$(curl -s -X POST "$BASE/api/auth/login" \
    -H "Content-Type: application/json" \
    -d '{"email":"admin@innovayse.com","password":"Admin123!"}')
ADMIN_TOKEN=$(echo "$ADMIN_RESPONSE" | jq -r '.accessToken // .token // empty')
ADMIN_HEADER="Authorization: Bearer $ADMIN_TOKEN"

if [ -n "$ADMIN_TOKEN" ]; then
    STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/admin/dashboard/stats" -H "$ADMIN_HEADER")
    check "GET /api/admin/dashboard/stats" 200 "$STATUS"

    STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/admin/settings" -H "$ADMIN_HEADER")
    check "GET /api/admin/settings" 200 "$STATUS"

    STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/admin/email-templates" -H "$ADMIN_HEADER")
    check "GET /api/admin/email-templates" 200 "$STATUS"

    STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/clients" -H "$ADMIN_HEADER")
    check "GET /api/clients" 200 "$STATUS"

    STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/domains" -H "$ADMIN_HEADER")
    check "GET /api/domains" 200 "$STATUS"
else
    echo "  WARNING: Admin token not available — skipping admin tests"
    echo "           Create admin user first: POST /api/auth/register with admin credentials"
fi

# ── AUTH GUARDS ────────────────────────────────────────
echo ""
echo "── Auth guards ───────────────────────────────────"
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/clients/me")
check "GET /api/clients/me (no token) -> 401" 401 "$STATUS"

STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$BASE/api/admin/dashboard/stats" -H "$AUTH_HEADER")
check "GET /api/admin/dashboard/stats (client token) -> 403" 403 "$STATUS"

# Cleanup
rm -f "$COOKIE_JAR"

echo ""
echo "=================================================="
echo "  Done!"
echo "=================================================="
