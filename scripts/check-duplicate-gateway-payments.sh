#!/usr/bin/env sh
#
# Lists the ledger rows that would break the unique gateway payment reference, so they can be
# settled by an operator before the migration AddTransactionSource runs.
#
# That migration gives every ledger row a Source and then creates a unique index on the payment
# method and reference of the rows whose Source is GatewayPayment. A database that already holds two
# gateway payment rows under one method and one reference cannot take the index; the migration stops
# and names them. This script names them first, using the same rule the migration backfills with, so
# the answer is the same one.
#
# A row is a gateway payment when it is not linked to an imported invoice, it took money in, its
# reference is not empty, its method is not one staff record by hand (Manual, Bank Transfer, Credit
# Balance, No Charge) and its description is one of the three the gateway paths write
# ("Invoice #N Payment", "Gateway payment for Invoice #N ...", "Card payment for Invoice #N ...").
# It cannot tell a payment staff recorded by hand under a gateway's name from the gateway's own, so a
# row listed here may be a manual entry: if it is, give its reference a distinct value or delete the
# duplicate, then run this again. An empty result means the migration will run.
#
# Read-only. Safe to run against production at any time.
#
# Usage:
#   scripts/check-duplicate-gateway-payments.sh [container]
#
# `container` defaults to hostpanel-db. Run it on the host that holds the database, or over ssh:
#
#   ssh prod 'sh -s' < scripts/check-duplicate-gateway-payments.sh
#
# The SQL travels on stdin rather than in a -c argument, as in check-client-owner-ids.sh, because it
# is quoted several times over before it arrives.
set -eu

CONTAINER="${1:-hostpanel-db}"

docker exec -i "$CONTAINER" sh -lc \
  'psql -U "$POSTGRES_USER" -d "${POSTGRES_DB}" -X -q -f -' <<'SQL'
SELECT
  t."PaymentMethod",
  t."TransactionId",
  count(*)                                  AS "Rows",
  string_agg(t."Id"::text, ', ' ORDER BY t."Id") AS "Ids",
  min(t."Date")                             AS "FirstDate",
  max(t."Date")                             AS "LastDate"
FROM "Transactions" AS t
WHERE t."TransactionId" <> ''
  AND t."AmountIn" > 0
  AND t."PaymentMethod" NOT IN ('Manual', 'Bank Transfer', 'Credit Balance', 'No Charge')
  AND (t."Description" LIKE 'Invoice #% Payment'
       OR t."Description" LIKE 'Gateway payment for Invoice #%'
       OR t."Description" LIKE 'Card payment for Invoice #%')
  AND NOT EXISTS (SELECT 1 FROM "Invoices" AS i WHERE i."Id" = t."InvoiceId" AND i."ExternalId" IS NOT NULL)
GROUP BY t."PaymentMethod", t."TransactionId"
HAVING count(*) > 1
ORDER BY min(t."Id");
SQL

# Reading the result
#
#   no rows         no two gateway payment rows share a method and a reference. The migration runs.
#
#   one row per     the rows named in Ids share the method and reference. Resolve each group (keep one
#   group           payment, or correct the reference of a row that was a different payment) before
#                   the deploy that carries the migration. The deploy dumps the database first, and
#                   the migration itself refuses to run while a group is left.
