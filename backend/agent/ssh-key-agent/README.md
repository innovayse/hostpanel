# hostpanel-ssh-key-agent

A small, dependency-free Python 3 helper that lets HostPanel add, list and revoke customers'
ED25519 public keys in their hosting accounts' `~/.ssh/authorized_keys`. It is installed once on
each managed server, by that server's administrator, with a one-command installer HostPanel
generates. Nothing in it is specific to one hosting company, network or server.

## Supported providers

| Profile | Status | Account checks on top of the generic ones |
|---|---|---|
| `cwp` (CentOS Web Panel) | Supported; adapter, unit tests and isolated lab tests | Account must exist in CWP (`root_cwp.user`, via `providers/cwp/account-check`); suspended (`usermod -L`) accounts cannot receive keys. |
| `generic-linux` | Agent profile; lab-tested. **Not selectable in HostPanel yet**: HostPanel has no server module for a plain Linux host. | passwd identity checks and the suspension lock only. |
| Other hosting panels | **Not supported.** Extension point only. | A provider is a profile in `profiles/<name>.conf` plus, if it needs one, a root-owned helper in `providers/<name>/` that answers `account_check_command`; HostPanel maps the server module to the profile in `SshKeyProviderFactory.AgentProfileFor`. None exists for them yet. |

HostPanel only offers SSH key management on servers whose module maps to a profile (today: the
v7 hosting panel module → `cwp`). Every other server answers "not available for this type of
hosting" to customers.

## Setting up a server (HostPanel admin → Servers → *SSH*)

1. **Connection.** Enter where customers connect (SSH hostname and port) and where HostPanel
   reaches the agent (agent host, port, management user: `hpsshkeys` by default). Save.
2. **Install.** *Generate identity*: HostPanel creates an ED25519 key pair for this server only,
   stores the private key encrypted, and shows a one-command installer. Run it as root on the
   server. It downloads the agent bundle from your HostPanel, **stops unless the SHA256 matches the
   value shown in the authenticated admin panel**, unpacks to a fresh temporary directory and runs
   `install.sh --provider <profile> --binding <id> "<public key>"`. The installer prints the
   server's host key fingerprints. Paste it into an SSH session: a hosting panel's browser terminal drops `$`
   variables and repeats input, which breaks the command.
3. **Host key.** *Read host key*: HostPanel shows the key the agent endpoint presents (without
   authenticating). Compare it with what the installer printed; if identical, *Use this
   fingerprint* and save. Connections presenting any other key are refused.
4. **Test.** The agent must answer **and** report the binding of this server record. An agent
   installed for another record is reported as `binding_mismatch`.
5. **Enable.** Refused by the API until a test with the saved settings succeeded, and refused if
   another enabled server already uses the same agent host and port. Changing the agent host,
   port, user, key or pinned fingerprint clears the test.

*Rotate identity* issues a new key (same binding) and switches customer access off until the
installer is run again. *Advanced* lets an administrator paste an existing private key instead.

## Network

HostPanel needs TCP access from its API host to the agent host and port. Any of these works:

* a private network or VPN between HostPanel and the server (recommended);
* the server's ordinary SSH port over the internet, ideally restricted by firewall to HostPanel's
  source address.

Either way the connection is authenticated by the per-server key, the host key is pinned, and the
management account can do nothing but run the agent. No VPN is required by the software.

Set `SSH_AGENT_SOURCE_ADDRESSES` on the HostPanel deployment to the address managed servers see its
connections come from; Servers → SSH then shows the exact CSF, firewalld and ufw lines that allow only
that address on the agent port, and the `PerSourcePenaltyExemptList` line for sshd. With a VPN, enter the
server's VPN address as the agent host; the host HostPanel runs on must itself be in the VPN.

**OpenSSH 9.8 and later** refuse an address for a while after failed or unauthenticated
connections (`PerSourcePenalties`, on by default). *Read host key* is such a connection, and so is
every call made with a rotated key before the installer has run again; repeated, they can lock
HostPanel out of the server for up to ten minutes, which customers see as "could not be reached".
Add HostPanel's source address to `PerSourcePenaltyExemptList` in `sshd_config`. `install.sh` and
`install.sh --check` print a warning when penalties are on and no address is exempt; they never
change sshd themselves.

## How HostPanel reaches the agent

```
HostPanel ──ssh (pinned host key, per-server ED25519 key)──▶ hpsshkeys@server
   authorized_keys: restrict,command="sudo -n /usr/local/sbin/hostpanel-ssh-key-agent" <key>
   sudoers:         hpsshkeys ALL=(root) NOPASSWD: /usr/local/sbin/hostpanel-ssh-key-agent ""
```

`restrict` removes pty, port/agent/X11 forwarding and user rc; the forced command ignores whatever
the client asks to run; sudo allows that exact path with no arguments. The request travels on
stdin as one JSON document; the agent never executes a shell or takes a path from the request.

## Contract (version 1)

| Request | Success response fields |
|---|---|
| `{"v":1,"op":"ping"}` | `agent_version`, `provider`, `binding` |
| `{"v":1,"op":"status","account":"u"}` | `shell_enabled`, `managed_keys`, `unmanaged_keys`, `reason` when disabled |
| `{"v":1,"op":"list","account":"u"}` | `keys: [{key_id, fingerprint}]`, `unmanaged_keys` |
| `{"v":1,"op":"add","account":"u","key_id":"<uuid>","public_key":"ssh-ed25519 …"}` | `key_id`, `fingerprint`, `changed` |
| `{"v":1,"op":"revoke","account":"u","key_id":"<uuid>"}` | `key_id`, `changed` |

Every response carries `"v":1` and `"ok"`. Refusals are `{"ok":false,"error":"<code>"}`:
`invalid_request`, `unsupported_version`, `unknown_operation`, `request_too_large`,
`invalid_account`, `account_not_found`, `account_not_eligible`, `account_suspended`,
`invalid_key`, `duplicate_key`, `key_id_conflict`, `too_many_keys`, `ssh_disabled`, `unsafe_path`,
`file_too_large`, `timeout`, `internal_error`. Unknown request fields are refused. `add` and
`revoke` are idempotent by `key_id`. Fields added since 1.0 (`provider`, `binding`, `reason`) are
additive; older HostPanels ignore them and older agents simply omit them.

## Safety properties

* **Account boundary**: strict name pattern; must exist in passwd; `uid >= min_uid`; not on
  `deny_accounts`; home exactly `<home_root>/<dir>`. The path comes from passwd, never from the
  request. Provider checks (panel account, suspension, group, opt-in list) run before anything is
  written; suspension and eligibility only gate *receiving* keys: `list` and `revoke` keep
  working, so access can always be removed.
* **Least privilege for file work**: the root parent validates, takes a per-account `flock`, then
  forks a child that drops to the account's uid/gid before touching any file.
* **Symlink / traversal**: `.ssh` and `authorized_keys` are opened with `O_NOFOLLOW` relative to
  directory descriptors; a foreign-owned or non-directory `.ssh` is `unsafe_path`.
* **Atomic writes**: `O_EXCL` temp file, `fsync`, `rename`, `fsync` of the directory.
* **Only its own keys**: managed lines are `ssh-ed25519 <blob> hostpanel:<key_id>`; keys added by
  other means are counted but never listed, changed or removed.
* **Timeouts**: 10 s for stdin, 60 s per request, so a silent client cannot pin a root process.
* **Audit**: every `add`/`revoke` is appended to the root-only audit log, with fingerprint, never
  key material.

## Per-server policy (`/etc/hostpanel-ssh-key-agent/agent.conf`)

| Setting | Effect |
|---|---|
| `provider`, `binding` | Reported by ping. Set by the installer. |
| `account_check_command` | Absolute path of a root-owned helper run as `helper <account>` (no shell, empty environment, 10 s timeout). Exit 0 = panel account, 1 = not one, anything else = fault. |
| `check_shadow_lock` | A locked password (`usermod -L`) makes the account `account_suspended`. `!!`/`*` alone is not a lock. |
| `ssh_group` | Only members may receive keys, set it when only one group may log in over SSH (e.g. a jailed-shell group). |
| `eligible_list` | Root-owned, not group/world-writable file, one account per line: an SSH opt-in list. |

## Jailed SSH enrolment (`jail_enrol`, agent 1.2.0+)

Off by default. With `jail_enrol = true` a customer gets SSH by adding their first key in
HostPanel; nobody runs anything on the server by hand. It assumes the customer sshd chroots
`jail_group` into `jail_root/%u` (for example `Match Group sftp-ssh` with
`ChrootDirectory /home/jail/%u`) and that jailkit is installed.

| Key | Default | Meaning |
|---|---|---|
| `jail_enrol` | `false` | Enrol on the first key, withdraw on the last. |
| `jail_root` | `/home/jail` | Parent of every jail; root-owned. |
| `jail_group` | `sftp-ssh` | Group the customer sshd chroots. Leave `ssh_group` empty when this is on. |
| `jail_sections` | `basicshell,editors,extendedshell,netutils,terminfo` | `jk_init` sections copied into a new jail. |
| `jail_shell` | `/bin/bash` | Login shell set on enrolment. |
| `jail_extras` | `/usr/bin/uname,/usr/bin/id,/usr/bin/hostname,/usr/bin/dircolors,/usr/bin/rsync,/usr/bin/tar,/usr/bin/gzip,/usr/bin/unzip,/etc/bashrc,/etc/profile.d/colorls.sh,/etc/DIR_COLORS` | Paths copied into the jail with `jk_cp -f` (binaries bring their libraries) when the jail lacks one or its host copy changed (missing in the jail, another size, or newer in whole seconds); paths missing on the host are skipped, and an entry already in place costs no copy. A bare jailkit tree lacks `uname`, which a login profile script calls, and `/etc/profile.d/colorls.sh` (with `dircolors`), where `ll` is defined; `rsync`, `tar`, `gzip` and `unzip` let a CI job deploy a site over SSH. |
| `systemd_unit_dir` | `/etc/systemd/system` | Where the home bind-mount unit is written. |

While it is on, an account that passes every other check reports `shell_enabled: true` even
before it is jailed, because adding a key is what gives it SSH. Suspended and denied accounts
are never enrolled.

**On `add`, before the key is written** (each step skipped when already done; any failure
refuses the add with `internal_error` and writes no key):

1. `jk_init -f -j <jail_root>/<user> <jail_sections>` when the jail has no `etc/`.
2. The user is appended to the jail's own `etc/passwd` and `etc/group`.
3. `<jail>/home/<user>` is created and the real home is bind-mounted onto it by a persistent
   unit (`systemd-escape -p --suffix=mount`, `What=/home/<user>`, `Options=bind`), then
   `systemctl enable --now`. Customer files are never moved or copied.
4. `usermod -aG <jail_group> <user>`, and `usermod -s <jail_shell> <user>` if the shell was a
   disabled one.

**On `revoke` of the last managed key**, when the account is in `jail_group` and has no
hand-installed keys either: `gpasswd -d <user> <jail_group>`, and `usermod -s /sbin/nologin` if
the shell is still `jail_shell`. The jail and its mount stay, so the next key re-enables SSH at
once. Both events are written to the audit log (`jail_enrol`, `jail_withdraw`).

To remove a jail entirely: `systemctl disable --now <unit>`, delete the unit file and
`systemctl daemon-reload`. **Before deleting anything**, check that `findmnt -R <jail_root>/<user>`
prints nothing: while the bind mount is active, `<jail_root>/<user>/home/<user>` *is* the
customer's website, and `rm -rf` on the jail would delete it. Only when nothing is mounted, remove
the jail with `rm -rf --one-file-system <jail_root>/<user>`.

## Innovayse Sites site preparation (`prepare-site`, agent 1.3.0+)

`{"v":1,"op":"prepare-site","account":"<user>","domain":"<the account's domain>"}` runs `site_prepare_command`
(empty = the op answers `not_supported`) as root with `<user> <domain>`, after the usual account checks and under the
account lock; with `jail_enrol` on, the jail is made first. The CWP profile ships the helper as
`/usr/local/libexec/hostpanel-ssh-key-agent/prepare-site`; set `site_prepare_command` to it to enable the op.

The CWP helper is idempotent and touches only that account: php-fpm 8.3 pool (with `opcache.validate_timestamps=1`,
`revalidate_freq=0`, `revalidate_path=1`, `realpath_cache_size=0`, which a `current` symlink switch behind Apache needs),
the vhost handler plus CWP's `~/.conf/webservers/<domain>.conf` record, a `<user>_site` database, `~/shared/.env`
(secrets generated on the server, never printed), `releases/` and `shared/` setgid `nobody`, `public_html ->
current/public` (the old placeholder is backed up), and PHP 8.3 in the jail. Everything it edits is copied to
`/root/isites-provision-backup/<user>-<time>/` first; Apache and php-fpm are reloaded gracefully, only after their config
tests pass, and the vhost is restored if a test fails.

Answers: `{"ok":true,"prepared":true,"steps":[...]}`, or `not_supported`, `site_not_owned` (the vhost does not belong to
the account, or no jail/vhost), `site_prepare_failed`. HostPanel calls it with a 10 minute timeout after provisioning a
service whose product metadata holds `{"innovayseSites": {"prepare": true}}`.

## Removing the jail of a deleted account (`remove-jail`, agent 1.4.0+)

`{"v":1,"op":"remove-jail","account":"<user>"}` runs before the usual account checks, because the account is gone by
design: it answers `account_exists` (and changes nothing) while the passwd entry exists, then disables and deletes the
jail's home mount unit, refuses if anything is still mounted inside the jail, and removes `<jail_root>/<user>` with
`rm -rf --one-file-system`. Answer: `{"ok":true,"removed":true|false}`. HostPanel sends it after a hosting account was
terminated on the panel; an older agent answers `unknown_operation`, which HostPanel only logs.

## Lifecycle

| Task | Command (as root on the server, in the unpacked bundle) |
|---|---|
| Install / reinstall | the command HostPanel shows (or `./install.sh --provider P --binding ID "<key>"`) |
| Preview | add `--dry-run` |
| Upgrade | run the current installer command again; prints `Agent <old> -> <new>`, keeps `agent.conf`, updates the binding |
| Diagnose | `./install.sh --check`: user, lock, agent owner/mode, sudoers, forced-command key, config, self-test, host key |
| Remove / roll back | `./uninstall.sh [--dry-run] [--purge]`: management key first, then sudoers, user and agent; config kept unless `--purge`; audit log always kept |

Neither script touches sshd, the firewall, any VPN, Jailkit or customers' keys. **Before
uninstalling, revoke the server's managed keys from HostPanel**
(`POST /api/admin/servers/{id}/ssh-keys/revoke-all`); keys left behind keep working until removed
by hand: list them with `grep -l 'hostpanel:' /home/*/.ssh/authorized_keys`.

## What happens to keys over a service's life

Every key is pinned to the server and account it was issued on. Removals always go there, by the
key's own tag, so they can never touch a key HostPanel did not write, not even on a server whose
account has since been reused.

| Event | Keys on the server | In HostPanel |
|---|---|---|
| Service suspended in HostPanel | Taken off the server (control panels suspend by locking the password, which does not stop a key from logging in); a key the server does not confirm removed stays installed and is logged for staff | `Suspended`; put back under the same id on unsuspension, `InstallFailed` if that is not confirmed |
| Account suspended in the panel only | **Stay and keep logging in**: the password lock does not stop keys. Suspend through HostPanel | Unchanged |
| Service terminated | Revoked automatically on their server | `Revoked`; if the server did not confirm, `RevokeFailed` with the reason, visible to staff |
| Service moved to another server | Stay on the old server until revoked | Listed as from the previous server; the customer (or revoke-all on the old server) removes them there; nothing is ever sent to the new server for them |
| SSH key management switched off on the server | Stay | Customer can no longer add, but can still revoke |
| Agent uninstalled | Stay unless revoke-all ran first | Revokes fail (`RevokeFailed`) until reinstalled |
| Identity rotated | Unaffected | Customer access off until the installer runs with the new key |

## Tests

```bash
python3 -m unittest discover -s backend/agent/ssh-key-agent/tests
```

They run unprivileged against a temporary home directory (no root, no real accounts).
