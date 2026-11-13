#!/usr/bin/env bash
# Removes hostpanel-ssh-key-agent from ONE server. Run as root on that server.
#
#   ./uninstall.sh [--purge] [--dry-run]
#
#   --purge    also remove /etc/hostpanel-ssh-key-agent (the config). The audit log is always kept.
#   --dry-run  print what would change and exit.
#
# Idempotent. Order matters: the management key goes first, so HostPanel loses access before
# anything else changes. Customers' authorized_keys are NOT touched, keys HostPanel installed
# keep working until revoked; list them with:
#   grep -l 'hostpanel:' /home/*/.ssh/authorized_keys
set -euo pipefail

MGMT_USER="${HOSTPANEL_SSH_MGMT_USER:-hpsshkeys}"
PURGE=0
DRY=0
for arg in "$@"; do
  case "$arg" in
    --purge) PURGE=1 ;;
    --dry-run) DRY=1 ;;
    *) echo "Unknown argument: $arg" >&2; exit 1 ;;
  esac
done
[[ $EUID -eq 0 ]] || { echo "Run as root." >&2; exit 1; }

run() { if [[ $DRY -eq 1 ]]; then echo "would: $*"; else "$@"; fi; }

if id "$MGMT_USER" >/dev/null 2>&1; then
  HOME_DIR="$(getent passwd "$MGMT_USER" | cut -d: -f6)"
  run rm -f "$HOME_DIR/.ssh/authorized_keys"
  # End any live session of the management user before removing it.
  run pkill -KILL -u "$MGMT_USER" || true
fi
run rm -f /etc/sudoers.d/hostpanel-ssh-key-agent
[[ $DRY -eq 1 ]] || visudo -c >/dev/null
if id "$MGMT_USER" >/dev/null 2>&1; then
  run userdel -r "$MGMT_USER"
fi
run rm -f /usr/local/sbin/hostpanel-ssh-key-agent
run rm -rf /usr/local/libexec/hostpanel-ssh-key-agent /run/hostpanel-ssh-key-agent
if [[ $PURGE -eq 1 ]]; then
  run rm -rf /etc/hostpanel-ssh-key-agent
fi
echo "Removed. Audit log kept: /var/log/hostpanel-ssh-key-agent.log"
