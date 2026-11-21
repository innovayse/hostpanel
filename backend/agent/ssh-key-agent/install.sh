#!/usr/bin/env bash
# Installs, upgrades or checks hostpanel-ssh-key-agent on ONE managed server. Run as root there.
#
#   ./install.sh [--provider generic-linux|cwp] [--binding UUID] [--config FILE] [--dry-run] "<public key>"
#   ./install.sh --check
#
#   --provider P   profile to install (profiles/P.conf) and its provider helper, if any.
#                  Default generic-linux. An existing agent.conf is kept; only `binding` is updated.
#   --binding ID   the HostPanel server record this install belongs to (shown in HostPanel's setup
#                  dialog). Reported by ping; HostPanel refuses an agent bound to another server.
#   --config FILE  use FILE instead of a profile (advanced).
#   --dry-run      print what would change and exit without changing anything.
#   --check        diagnose an existing install and exit; changes nothing.
#
# Idempotent: re-running converges to the same state, and re-running with a newer bundle upgrades
# the agent in place. It creates a locked-down management user whose single authorized key can
# only run the agent, lets that user run exactly the agent (no arguments) as root through sudo,
# and installs the agent. It does not touch sshd, the firewall, any VPN or any customer account.
# Undo with uninstall.sh.
set -euo pipefail

MGMT_USER="${HOSTPANEL_SSH_MGMT_USER:-hpsshkeys}"
HERE="$(cd "$(dirname "$0")" && pwd)"
AGENT_DST="/usr/local/sbin/hostpanel-ssh-key-agent"
LIBEXEC="/usr/local/libexec/hostpanel-ssh-key-agent"
SUDOERS="/etc/sudoers.d/hostpanel-ssh-key-agent"
CONF_DIR="/etc/hostpanel-ssh-key-agent"
CONF="$CONF_DIR/agent.conf"
PROVIDER="generic-linux"
CONFIG_SRC=""
BINDING=""
DRY=0
CHECK=0
KEY=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --provider) PROVIDER="$2"; shift 2 ;;
    --binding) BINDING="${2,,}"; shift 2 ;;
    --config) CONFIG_SRC="$2"; shift 2 ;;
    --dry-run) DRY=1; shift ;;
    --check) CHECK=1; shift ;;
    ssh-ed25519\ *) KEY="$1"; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 1 ;;
  esac
done

[[ $EUID -eq 0 ]] || { echo "Run as root." >&2; exit 1; }

version_of() { [[ -f "$1" ]] && sed -n 's/^AGENT_VERSION = "\(.*\)"$/\1/p' "$1" || echo none; }

# OpenSSH 9.8+ refuses an address for a while after failed or unauthenticated connections. Host
# key reads and calls with a rotated key count, so HostPanel's address can be locked out of every
# server operation. Only reported: these scripts never change sshd.
penalty_note() {
  local cfg; cfg="$(sshd -T 2>/dev/null)" || return 0
  local pen; pen="$(sed -n 's/^persourcepenalties //p' <<<"$cfg")"
  [[ -z "$pen" || "$pen" == no ]] && return 0
  [[ "$(sed -n 's/^persourcepenaltyexemptlist //p' <<<"$cfg")" == none ]] || return 0
  echo "WARN   sshd PerSourcePenalties is on and no address is exempt. Add HostPanel's source address to"
  echo "       PerSourcePenaltyExemptList in sshd_config, or HostPanel can be refused for minutes after a failure."
}

# --- diagnostics -------------------------------------------------------------------------------
if [[ $CHECK -eq 1 ]]; then
  fail=0
  say() { printf '%-6s %s\n' "$1" "$2"; [[ "$1" == FAIL ]] && fail=1; true; }
  id "$MGMT_USER" >/dev/null 2>&1 && say OK "management user $MGMT_USER exists" || say FAIL "management user $MGMT_USER missing"
  [[ "$(passwd -S "$MGMT_USER" 2>/dev/null | awk '{print $2}')" =~ ^(L|LK)$ ]] || [[ "$(getent shadow "$MGMT_USER" | cut -d: -f2)" == \!* ]] \
    && say OK "management user password locked" || say FAIL "management user password not locked"
  [[ -x "$AGENT_DST" && "$(stat -c '%U %a' "$AGENT_DST")" == "root 755" ]] && say OK "agent $(version_of "$AGENT_DST") root:755" || say FAIL "agent missing or wrong owner/mode"
  [[ -f "$SUDOERS" && "$(stat -c '%U %a' "$SUDOERS")" == "root 440" ]] && visudo -cf "$SUDOERS" >/dev/null 2>&1 && say OK "sudoers rule valid" || say FAIL "sudoers rule missing or invalid"
  AK="$(getent passwd "$MGMT_USER" | cut -d: -f6)/.ssh/authorized_keys"
  [[ -f "$AK" && "$(grep -c . "$AK")" == 1 ]] && grep -q '^restrict,command="sudo -n /usr/local/sbin/hostpanel-ssh-key-agent" ssh-ed25519 ' "$AK" \
    && say OK "one forced-command key, restricted" || say FAIL "management authorized_keys is not exactly one restricted forced-command key"
  [[ -f "$CONF" ]] && say OK "config $(sed -n 's/^provider *= *//p' "$CONF"), binding $(sed -n 's/^binding *= *//p' "$CONF")" || say FAIL "config missing"
  pong="$(echo '{"v":1,"op":"ping"}' | sudo -u "$MGMT_USER" sudo -n "$AGENT_DST" 2>&1 || true)"
  [[ "$pong" == *'"ok": true'* ]] && say OK "self-test: $pong" || say FAIL "self-test: $pong"
  rc=0
  unsafe="$("$AGENT_DST" --check-config 2>/dev/null)" || rc=$?
  case $rc in
    0) say OK "jail_extras hold no private key material" ;;
    1) say WARN "${unsafe//$'\n'/; }" ;;
    *) say WARN "could not check jail_extras (agent exit $rc); run $AGENT_DST --check-config to see why" ;;
  esac
  for f in /etc/ssh/ssh_host_ed25519_key.pub; do [[ -f $f ]] && say INFO "host key $(ssh-keygen -lf "$f" -E sha256 | awk '{print $2}')"; done
  penalty_note
  exit $fail
fi

# --- install / upgrade -------------------------------------------------------------------------
[[ -n "$KEY" ]] || { echo "Usage: $0 [--provider P] [--binding UUID] [--config FILE] [--dry-run] \"ssh-ed25519 AAAA... comment\"" >&2; exit 1; }
[[ "$PROVIDER" =~ ^[a-z0-9-]+$ ]] || { echo "Invalid provider name." >&2; exit 1; }
[[ -z "$CONFIG_SRC" ]] && CONFIG_SRC="$HERE/profiles/$PROVIDER.conf"
[[ -f "$CONFIG_SRC" ]] || { echo "No profile for provider '$PROVIDER' ($CONFIG_SRC)." >&2; exit 1; }
[[ -z "$BINDING" || "$BINDING" =~ ^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$ ]] || { echo "Binding must be a UUID." >&2; exit 1; }
command -v python3 >/dev/null || { echo "python3 is required." >&2; exit 1; }
command -v sudo >/dev/null || { echo "sudo is required." >&2; exit 1; }
printf '%s\n' "$KEY" | ssh-keygen -l -f /dev/stdin >/dev/null 2>&1 || { echo "Management key is not a valid public key." >&2; exit 1; }

run() { if [[ $DRY -eq 1 ]]; then echo "would: $*"; else "$@"; fi; }

OLD="$(version_of "$AGENT_DST")"; NEW="$(version_of "$HERE/hostpanel-ssh-key-agent")"
[[ "$OLD" == none ]] && echo "Installing agent $NEW ($PROVIDER)." || echo "Agent $OLD -> $NEW."

# 1. Management user: system account, locked password, /bin/sh only so the forced command can run.
if ! id "$MGMT_USER" >/dev/null 2>&1; then
  run useradd --system --create-home --home-dir "/var/lib/$MGMT_USER" --shell /bin/sh --comment "HostPanel SSH key agent" "$MGMT_USER"
fi
run usermod -L "$MGMT_USER"

# 2. The agent and the provider's helper, if the profile names one: root-owned, not writable by others.
run install -o root -g root -m 0755 "$HERE/hostpanel-ssh-key-agent" "$AGENT_DST"
if [[ -d "$HERE/providers/$PROVIDER" ]]; then
  run install -d -o root -g root -m 0755 "$LIBEXEC"
  for helper in "$HERE/providers/$PROVIDER"/*; do
    run install -o root -g root -m 0755 "$helper" "$LIBEXEC/$(basename "$helper")"
  done
fi
run install -d -o root -g root -m 0755 "$CONF_DIR"
if [[ ! -f "$CONF" ]]; then
  run install -o root -g root -m 0644 "$CONFIG_SRC" "$CONF"
elif [[ "$(sed -n 's/^provider *= *//p' "$CONF")" != "$PROVIDER" ]]; then
  echo "Note: keeping existing $CONF (provider $(sed -n 's/^provider *= *//p' "$CONF")); remove it to switch to $PROVIDER." >&2
fi
if [[ -n "$BINDING" ]]; then
  if [[ $DRY -eq 1 ]]; then echo "would: set binding = $BINDING in $CONF"
  elif grep -q '^binding *=' "$CONF"; then sed -i "s/^binding *=.*/binding = $BINDING/" "$CONF"
  else sed -i "s/^\[agent\]$/[agent]\nbinding = $BINDING/" "$CONF"; fi
fi

# 3. sudo: exactly this command, with NO arguments (the trailing "" forbids any), no tty needed,
#    environment reset. Validated with visudo before it is put in place.
TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT
printf 'Defaults:%s !requiretty, env_reset\n%s ALL=(root) NOPASSWD: %s ""\n' "$MGMT_USER" "$MGMT_USER" "$AGENT_DST" > "$TMP"
visudo -cf "$TMP" >/dev/null
run install -o root -g root -m 0440 "$TMP" "$SUDOERS"
[[ $DRY -eq 1 ]] || visudo -c >/dev/null

# 4. The one authorized key, pinned to the agent; `restrict` removes pty, port/agent/X11
#    forwarding and user rc. The previous key (if any) is replaced, never appended to.
if [[ $DRY -eq 1 ]]; then
  echo "would: write /var/lib/$MGMT_USER/.ssh/authorized_keys (restrict,command=\"sudo -n $AGENT_DST\")"
  exit 0
fi
HOME_DIR="$(getent passwd "$MGMT_USER" | cut -d: -f6)"
install -d -o "$MGMT_USER" -g "$MGMT_USER" -m 0700 "$HOME_DIR/.ssh"
printf 'restrict,command="sudo -n %s" %s\n' "$AGENT_DST" "$KEY" > "$TMP"
install -o "$MGMT_USER" -g "$MGMT_USER" -m 0600 "$TMP" "$HOME_DIR/.ssh/authorized_keys"

echo "Done. Compare this host key with the one HostPanel shows before pinning it:"
for f in /etc/ssh/ssh_host_ed25519_key.pub /etc/ssh/ssh_host_ecdsa_key.pub /etc/ssh/ssh_host_rsa_key.pub; do
  [[ -f $f ]] && ssh-keygen -lf "$f" -E sha256
done
penalty_note
echo "Self-test:"
echo '{"v":1,"op":"ping"}' | sudo -u "$MGMT_USER" sudo -n "$AGENT_DST"
