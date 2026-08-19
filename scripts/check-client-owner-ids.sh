#!/usr/bin/env sh
#
# Reports what `clients.UserId` currently points at, so the SSO cut-over can be
# checked before and after it happens.
#
# `clients.UserId` holds whatever this deployment's identity provider calls a
# person: the local `AspNetUsers.Id` when this product owns its users, the SSO
# subject when the SSO does. The migration RepointClientsAtSsoSubjects rewrites
# the first into the second, and this says whether that has happened yet.
#
# Read-only. Safe to run against production at any time, including mid-window.
#
# Usage:
#   scripts/check-client-owner-ids.sh [container]
#
# `container` defaults to hostpanel-db. Run it on the host that holds the
# database, or over ssh:
#
#   ssh prod 'sh -s' < scripts/check-client-owner-ids.sh
#
# The SQL travels on stdin rather than in a -c argument. It is quoted three times
# over before it arrives — ssh, sh -lc, psql — and every layer wanted its own
# escaping of the string literals; sending it as a file leaves it alone.
set -eu

CONTAINER="${1:-hostpanel-db}"

docker exec -i "$CONTAINER" sh -lc \
  'psql -U "$POSTGRES_USER" -d "${POSTGRES_DB:-hostpanel}" -X -q -f -' <<'SQL'
SELECT
  count(*)                                                                          AS client_rows,
  count(*) FILTER (WHERE c."UserId" IN (SELECT "Id" FROM "AspNetUsers"))             AS owner_is_a_local_id,
  count(*) FILTER (WHERE c."UserId" IN (SELECT "SsoSubjectId" FROM "AspNetUsers"
                                        WHERE "SsoSubjectId" IS NOT NULL))           AS owner_is_an_sso_subject,
  count(*) FILTER (WHERE c."UserId" NOT IN (SELECT "Id" FROM "AspNetUsers")
                     AND c."UserId" NOT IN (SELECT "SsoSubjectId" FROM "AspNetUsers"
                                            WHERE "SsoSubjectId" IS NOT NULL))       AS owner_is_nobody
FROM clients c;

SELECT
  count(*)                                                        AS accounts,
  count(*) FILTER (WHERE u."SsoSubjectId" IS NOT NULL)            AS linked_to_the_sso,
  -- Read through to_jsonb rather than naming the column. This has to run against a
  -- database that has not had the deploy yet, and naming DeletedAt directly makes
  -- the whole statement a parse error there. A missing column reads as 0 deleted,
  -- which is what a database without soft delete in fact holds.
  count(*) FILTER (WHERE to_jsonb(u) ->> 'DeletedAt' IS NOT NULL) AS deleted
FROM "AspNetUsers" u;
SQL

# Reading the result
#
#   owner_is_a_local_id > 0        the rewrite has not run yet. Deploying in SSO
#                                  mode now disconnects this many customers from
#                                  their clients, services and invoices.
#
#   owner_is_an_sso_subject > 0    the rewrite has run.
#
#   owner_is_nobody > 0            rows whose owner exists in neither store.
#                                  These are orphans from before deleting a user
#                                  kept the row, and the migration leaves them
#                                  alone. Unrelated to the cut-over: the count
#                                  should be the same before and after.
#
# A standalone deployment reads owner_is_a_local_id = client_rows and
# linked_to_the_sso = 0, and stays that way. The migration is a no-op there.
