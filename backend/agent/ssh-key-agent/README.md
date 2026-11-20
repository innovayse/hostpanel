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
| `{"v":1,"op":"ping"}` | `agent_version`, `provider`, `binding`, `capabilities` (1.5.0+: the ops of "Websites and databases" this server has a helper for), `php_versions` (the php versions the server offers, sorted, for example `["8.1","8.2","8.3"]`; `[]` without a `php-versions` helper) |
| `{"v":1,"op":"status","account":"u"}` | `shell_enabled`, `managed_keys`, `unmanaged_keys`, `reason` when disabled |
| `{"v":1,"op":"list","account":"u"}` | `keys: [{key_id, fingerprint}]`, `unmanaged_keys` |
| `{"v":1,"op":"add","account":"u","key_id":"<uuid>","public_key":"ssh-ed25519 …"}` | `key_id`, `fingerprint`, `changed` |
| `{"v":1,"op":"revoke","account":"u","key_id":"<uuid>"}` | `key_id`, `changed` |
| `{"v":1,"op":"ensure-website","account":"u","hostname":"h","docroot":"d","php":"8.3","link_root":true}` (1.5.0+; `php`, `link_root` optional) | `changed` |
| `{"v":1,"op":"remove-website","account":"u","hostname":"h"}` (1.5.0+) | `changed` |
| `{"v":1,"op":"ensure-database","account":"u","label":"l"}` (1.5.0+) | `created`, `database`, `username`, `password` (only when `created` is true) |
| `{"v":1,"op":"remove-database","account":"u","label":"l"}` (1.5.0+) | `changed` |

Every response carries `"v":1` and `"ok"`. Refusals are `{"ok":false,"error":"<code>"}`:
`invalid_request`, `unsupported_version`, `unknown_operation`, `request_too_large`,
`invalid_account`, `account_not_found`, `account_not_eligible`, `account_suspended`,
`invalid_key`, `duplicate_key`, `key_id_conflict`, `too_many_keys`, `ssh_disabled`, `unsafe_path`,
`file_too_large`, `timeout`, `internal_error`; from the optional ops: `not_supported`, `site_not_owned`,
`account_exists` (1.4.0+), and since 1.5.0 `website_conflict`, `php_version_unsupported`, `website_failed`,
`database_failed`, `database_limit`. Unknown request fields are refused. `add` and
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
| `helper_dir` | Absolute directory of the provider helpers behind the 1.5.0 website and database ops (default `/usr/local/libexec/hostpanel-ssh-key-agent`). A missing helper makes its op `not_supported`. |

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

## Removing the jail of a deleted account (`remove-jail`, agent 1.4.0+)

`{"v":1,"op":"remove-jail","account":"<user>"}` runs before the usual account checks, because the account is gone by
design: it answers `account_exists` (and changes nothing) while the passwd entry exists, then disables and deletes the
jail's home mount unit, refuses if anything is still mounted inside the jail, and removes `<jail_root>/<user>` with
`rm -rf --one-file-system`. Answer: `{"ok":true,"removed":true|false}`. HostPanel sends it after a hosting account was
terminated on the panel; an older agent answers `unknown_operation`, which HostPanel only logs.

## Websites and databases (`ensure-website`, `remove-website`, `ensure-database`, `remove-database`, agent 1.5.0+)

Four ops that give a hosting account a website (vhost, document root, php-fpm pool) and databases, without HostPanel
knowing anything about the panel underneath. Each runs a provider helper from `helper_dir` (default
`/usr/local/libexec/hostpanel-ssh-key-agent`) whose name is the op's name; no helper on the server means the op answers
`not_supported`, and `ping` lists the ops that have one in `capabilities`. The CWP profile ships all four in
`providers/cwp/` (plus the shared `hp-common.sh`). Like every op they run after the account checks and under the
account's lock, and they write one audit line each with the outcome and the hostname or label, never a helper's answer.
A fifth helper, `php-versions` (no arguments, one `N.N` per line on stdout, sorted by the agent), is not an op: ping runs it to report `php_versions`, and a server without it, or whose helper fails, reports `[]`. `ensure-website` accepts only a `php` the provider also lists there.

### Requests

| Field | Rule |
|---|---|
| `account` | as everywhere |
| `hostname` | a lower case domain name. The CWP helper accepts only the account's own CWP domain or a subdomain of it |
| `docroot` | relative to the home: 1 to 6 segments of `A-Za-z0-9._-`, no segment starting with a dot (so no `..`, `.ssh`, `.conf`), at most 200 characters. The helper also resolves it and refuses anything that leaves the home through a symlink |
| `php` (optional) | `8.3` style; the helper maps it to the installed php-fpm of that version. Left out: no php-fpm pool and no handler |
| `link_root` (optional, boolean) | the document root is a symlink that deploys switch (`current/public`). Needs `php`. Adds the pool lines such a switch needs behind Apache (`opcache.validate_timestamps=1`, `opcache.revalidate_freq=0`, `opcache.revalidate_path=1`, `realpath_cache_size=0`) and leaves the last path component to the deploys instead of creating a directory |
| `label` | `^[a-z][a-z0-9]{0,14}$`; the database and its user are named `<account>_<label>` |

Any other field, a wrong type, or `link_root` without `php` is `invalid_request`, before any helper runs.

### Answers

| Op | Success |
|---|---|
| `ensure-website`, `remove-website`, `remove-database` | `{"v":1,"ok":true,"changed":true\|false}` |
| `ensure-database` | `{"v":1,"ok":true,"created":true,"database":"alice_shop","username":"alice_shop","password":"<40 hex>"}`; when it already existed (and the agent made it) `created` is false and there is no `password` |

Failures are the stable codes of the table below. The helper's exit code is the whole signal: 3 not the account's or not
created by the agent (`site_not_owned`), 4 the vhost serves another document root (`website_conflict`), 5 the php version
is not installed (`php_version_unsupported`), 6 the account is at its database limit (`database_limit`), anything else, a
timeout, a missing or doubled `RESULT` line, or a `RESULT` that fails its schema is `website_failed` or `database_failed`.

| Code | Op | Meaning |
|---|---|---|
| `not_supported` | all | no helper for the op on this server |
| `site_not_owned` | all | the hostname is not the account's domain, a vhost or database of that name exists but the agent did not create it, or a vhost belongs to another account |
| `website_conflict` | ensure-website | a vhost CWP made serves a different document root; it is never rewritten |
| `php_version_unsupported` | ensure-website | that php-fpm version is not installed |
| `website_failed` | website ops | anything else; a configuration test that fails restores every edited file first |
| `database_limit` | ensure-database | the account already has 10 databases the agent made |
| `database_failed` | database ops | anything else; a failed creation drops what it made |

### The helper contract

A helper is called as root with a fixed environment (`PATH` only, no stdin, 50 second limit) and these arguments, all
validated by the agent first and again by the helper:

```
ensure-website  <account> <hostname> <docroot> <php or empty> <link_root 0|1>
remove-website  <account> <hostname>
ensure-database <account> <label>
remove-database <account> <label>
```

It prints progress lines for whoever runs it by hand and exactly one `RESULT {json}` line on stdout. The agent reads only
that line, parses it and checks it against the op's schema (`changed` boolean; for `ensure-database`: `created`,
`database`, `username` equal to `<account>_<label>`, and `password` of 40 lower case hex characters only when `created`).
stderr is discarded. The database password is generated in the helper (`openssl rand -hex 20`), travels to MySQL on
stdin so it is never on a command line, and exists only in that `RESULT` line and the response to the call that created
the database: it is not written to the registry, the audit log or any other log. A lost password means dropping the
database (`remove-database`) and creating it again.

`HP_ROOT` is a test seam: it prefixes every path and tool the CWP helpers use. The agent never takes it from a request; it
reaches a helper only through the agent's own fixed environment (`JAIL_ENV`), which holds `PATH` in production.

### What the CWP helpers do

* **Idempotent and minimal.** Each step is skipped when already done; the answer says `changed: false` when nothing was.
* **Only what the agent created.** A root-owned registry in `/var/lib/hostpanel-ssh-key-agent/<account>/` (one
  `website.<hostname>` or `database.<label>` file per thing, mode 0700 directory) records what was made. A `remove-*` for
  something with no entry, whose vhost or database exists, is `site_not_owned`; with nothing there it succeeds with
  `changed: false`. A vhost file written by the helper starts with `# hostpanel-ssh-key-agent managed`; the php block it
  adds to a vhost CWP made is wrapped in `# hostpanel-ssh-key-agent php begin|end`, and removal strips only that. The
  pool is removed only when it carries the marker and no other registered website of the account uses that php version;
  a document root directory only when the helper created it and it is empty (customer files are never removed).
* **Backup, test, restore, graceful.** Every file it edits is copied to `/var/lib/hostpanel-ssh-key-agent/backups/<account>-<time>-<pid>/`
  first. Apache (`apachectl -t`) and php-fpm (`php-fpm -t`) configuration is tested before any reload, any failure restores
  the files and answers `website_failed`, and the reload is graceful (`apachectl graceful`, `systemctl reload php-fpm<NN>`).
* **PHP.** `php: "8.3"` means `/opt/alt/php-fpm83`; a version that is not one of ping's `php_versions` (the installed `/opt/alt/php-fpm<NN>` that hold a php-fpm binary, listed by the `php-versions` helper) is `php_version_unsupported`. The pool
  (`<account>.conf`, socket `<account>.sock`) is made as CWP shapes its own, and CWP's per-domain record
  `~/.conf/webservers/<hostname>.conf` is written (when absent) so a later CWP vhost rebuild keeps the version.
* **Docroot.** Missing directories are created owned by the account with group `nobody` and mode 2750 (Apache serves static
  files as `nobody`); with `link_root` the last component is left to the deploys. A vhost CWP made for the account's own
  domain keeps its content and only gets the php block, provided its `DocumentRoot` already is `/home/<account>/<docroot>`;
  otherwise `website_conflict`. Any other hostname of the account gets a vhost file the helper writes (HTTP; TLS is left to
  the panel's own certificate handling).
* **Uninstalling the agent** removes the helpers but keeps `/var/lib/hostpanel-ssh-key-agent/`, so a reinstall still knows
  what the agent created; the websites and databases themselves stay until removed through the ops or by hand.
* **Databases.** `CREATE DATABASE` (utf8mb4) and a user for `localhost` and `127.0.0.1` with all privileges on that database
  only, at most 10 per account.

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
`tests/test_ops.py` also runs the CWP helpers for real against a stand-in server tree (`HP_ROOT`) with fake `apachectl`,
`php-fpm`, `mysql`, `openssl`, `chown`, `chgrp` and `systemctl`, and checks the golden vectors in `tests/contract/*.json`
(one file per op: request, what the helper printed, and the response the agent must give), which the HostPanel adapter
tests reuse as fixtures.
