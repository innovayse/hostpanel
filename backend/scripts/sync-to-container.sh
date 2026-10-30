#!/usr/bin/env bash
# Sync backend/src and backend/tests into a scratch container's /work.
#
# Why a script: `tar -cf - | docker exec tar -xf -` adds and overwrites but never DELETES, so a file
# removed on the laptop lives on inside the container. That has cost this migration several hours —
# a deleted controller left behind makes every request match two endpoints (AmbiguousMatchException),
# and a deleted test file keeps failing to compile against code that no longer exists. Wiping /work
# wholesale is not the answer either: it takes obj/project.assets.json with it and forces a full
# restore (~4 minutes).
#
# So: copy everything, then delete inside the container exactly those .cs/.resx/.csproj files that no
# longer exist on the laptop, leaving obj/ and bin/ untouched.
#
# Usage: scripts/sync-to-container.sh [container]   (default: hp-sdk).  Run from backend/.
set -euo pipefail

container="${1:-hp-sdk}"

# --exclude=.git: src/Innovayse.SDK is a git submodule whose .git file points into this checkout's
# .git directory, which the container does not have; the build needs only the sources beside it.
tar --exclude=bin --exclude=obj --exclude=plugins --exclude=.git -cf - src tests |
    docker exec -i "$container" bash -lc "cd /work && tar -xf - && find src tests -name '*.cs' | xargs -r sed -i 's/\r\$//'"

# The laptop's file list, as /work-relative paths.
find src tests \( -name '*.cs' -o -name '*.resx' -o -name '*.csproj' \) \
    -not -path '*/obj/*' -not -path '*/bin/*' |
    sed 's#\\#/#g' | sort > /tmp/hp-local-files.txt

docker exec "$container" bash -lc \
    "cd /work && find src tests \( -name '*.cs' -o -name '*.resx' -o -name '*.csproj' \) \
        -not -path '*/obj/*' -not -path '*/bin/*' | sort" > /tmp/hp-remote-files.txt

# Present in the container, absent on the laptop => deleted since the last sync.
comm -13 /tmp/hp-local-files.txt /tmp/hp-remote-files.txt > /tmp/hp-stale-files.txt

stale_count=$(wc -l < /tmp/hp-stale-files.txt | tr -d ' ')
if [ "$stale_count" != "0" ]; then
    echo "removing $stale_count stale file(s) from $container:"
    sed 's/^/  /' /tmp/hp-stale-files.txt
    # shellcheck disable=SC2002
    cat /tmp/hp-stale-files.txt | docker exec -i "$container" bash -lc "cd /work && xargs -r rm -f"
fi

echo "synced to $container"
