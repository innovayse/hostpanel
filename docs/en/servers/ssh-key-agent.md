---
title: SSH key agent
description: "Install the Hostpanel SSH key agent so clients can add ED25519 public keys to their hosting accounts: setup, configuration, errors, uninstall."
keywords: "ssh key agent, ed25519, ssh keys, agent configuration, host fingerprint"
---

# SSH key agent <Label type="admin" />

<DemoLink path="/servers" />

The SSH key agent lets clients add their own ED25519 public keys to their hosting accounts. Each server runs a small agent process; Hostpanel connects to it over SSH using a per-server identity key and a pinned host fingerprint.

## How it works

```
Hostpanel ──ssh (pinned host key, per-server ED25519)──▶ hpsshkeys@server
  authorized_keys: restrict,command="sudo -n /usr/local/sbin/hostpanel-ssh-key-agent"
  sudoers:         hpsshkeys ALL=(root) NOPASSWD: /usr/local/sbin/hostpanel-ssh-key-agent
```

The `restrict` directive removes PTY, port forwarding and user rc; the forced command ignores whatever the client asks to run; sudo allows only that exact path with no arguments. Requests travel on stdin as a JSON document; the agent never executes a shell or takes a path from the request.

**Supported server modules:** a server is supported when the hosting panel plugin that serves its module ships an SSH agent profile (see [SSH agent profile of a hosting panel plugin](../developers/write-a-plugin.md#ssh-agent-profile)). All other server types show "SSH key management not available" to clients.

## Set up SSH on a server

Go to **Servers** and click the **SSH** row action on a server to open the SSH key management dialog.

<Steps>
<li>

**Connection settings.** Fill in the fields and save. These settings are used by all subsequent steps.

| Field | Notes |
|-------|-------|
| SSH hostname | Hostname clients use to connect (shown to the client) |
| SSH port | Default 22 |
| Agent host | Address Hostpanel dials to reach the agent (may differ from the SSH hostname) |
| Agent SSH port | Default 22 |
| Management user | System account the agent runs as; default `hpsshkeys` |
| Connect timeout (s) | Seconds before a connection attempt is abandoned |
| Operation timeout (s) | Seconds before an agent request is abandoned |

**Network access.** Hostpanel connects from a fixed address. Allow only that address on the agent port with the line shown for your firewall (CSF, firewalld, or ufw). Set `SSH_AGENT_SOURCE_ADDRESSES` in the deployment environment to the address managed servers see Hostpanel's connections come from; the dialog then shows ready-made firewall and sshd lines.

On OpenSSH 9.8 and later, failed or unauthenticated connections are penalised per source address (`PerSourcePenalties`, on by default). Add Hostpanel's address to `PerSourcePenaltyExemptList` in `sshd_config` to prevent a temporary lockout. The dialog shows the exact line to add.

With a VPN or private network: enter the server's private address as the Agent host; the host Hostpanel runs on must be in that network.

</li>
<li>

**Generate identity.** Click **Generate identity**. Hostpanel creates an ED25519 key pair for this server, stores the private key encrypted, and displays a one-command installer. Copy the command and run it as root on the server.

<Callout type="warning">
Run the installer in a native SSH terminal, not in a hosting panel's browser terminal. Browser terminals drop <code>$</code> variables and echo input, which breaks the command.
</Callout>

The installer verifies the SHA256 checksum shown in the admin panel before extracting. It prints the server's host key fingerprints. Note them for the next step.

The installer accepts these flags:

| Flag | Effect |
|------|--------|
| `--provider <profile>` | Agent profile: the id the plugin that serves the server's module ships (the SSH dialog fills it in), or `generic-linux` for plain Linux |
| `--binding <id>` | Ties this installation to the Hostpanel server record |
| `--config <file>` | Use a custom config file instead of the profile's default (advanced) |
| `--dry-run` | Preview what would be installed without making any changes |
| `--check` | Diagnose an existing installation (user, lock, owner/mode, sudoers, forced-command key, config, self-test, host key) |

Running the installer again upgrades the agent binary and updates the binding while keeping the existing `agent.conf`. The installer creates and writes:

- `/usr/local/sbin/hostpanel-ssh-key-agent`: the agent binary
- `/etc/sudoers.d/hostpanel-ssh-key-agent`: the sudoers rule
- `/etc/hostpanel-ssh-key-agent/agent.conf`: the agent configuration (from the profile)
- `/var/lib/hpsshkeys/.ssh/authorized_keys`: the management user's forced-command key

The installer never touches sshd, the firewall, any VPN, Jailkit, or customer accounts.

</li>
<li>

**Pin the host key.** Click **Read host key**. Hostpanel connects to the agent without authenticating and reads the fingerprint the server presents. Compare it with what the installer printed; if identical, click **Use this fingerprint** and save. All future connections that present any other fingerprint are refused.

If **Read host key** fails:

| Error code | Meaning |
|------------|---------|
| `SSH_SETTINGS_INCOMPLETE` | Agent host field is empty; fill in the connection settings first |
| `SSH_AGENT_HOST_UNREACHABLE` | No network path to the agent host and port; check the address, firewall and VPN |
| `SSH_AGENT_PORT_REFUSED` | The port actively refused the connection; nothing is listening there or a firewall rejects Hostpanel's address |
| `SSH_AGENT_CONNECTION_CLOSED` | The server accepted then closed the connection before the SSH greeting; check `PerSourcePenalties`, `MaxStartups` and TCP wrappers |
| `SSH_HOST_KEY_UNREADABLE` | Connected but received no host key; check that the port runs sshd |

</li>
<li>

**Test the connection.** Click **Test agent**. The status line shows one of:

- **Agent reachable**: agent responded and the binding matches.
- **Agent unreachable**: connection failed; see "Last check failed" for the reason.
- **Not tested**: settings were changed since the last test.

The agent version is shown as "Agent _version_" or "Agent _version_ (_provider_)". A successful test is required before the server can be enabled. Changing the agent host, port, user, key, or pinned fingerprint clears the test.

If the test fails the reason is shown as "Last check failed: _reason_" (`servers.ssh.failures.*`):

| Reason | Meaning |
|--------|---------|
| the server could not be reached | No network path to the agent host (`Unreachable`) |
| the agent timed out | Connection opened but the agent did not respond in time (`Timeout`) |
| the agent was installed for a different server | Correct host but wrong binding, check the agent host or run this server's install command on it (`BindingMismatch`) |
| the agent version is not supported | Agent is too old for this Hostpanel version (`UnsupportedVersion`) |
| the agent failed internally | Agent-side error; check `/var/log/hostpanel-ssh-key-agent.log` (`InternalError`) |
| the agent rejected the request | Unexpected refusal from the agent (`InvalidRequest`) |
| the agent does not support this operation | Agent version does not implement this operation (`UnknownOperation`) |
| the request was too large | Request body exceeded the agent's size limit (`RequestTooLarge`) |
| the server has no complete agent settings | One or more required fields are empty (`AgentNotConfigured`) |
| unknown reason | Unexpected response (`Unknown`) |

The table lists common reasons. The admin panel shows about twenty; consult `servers.ssh.failures` in the locale file for the full list.

`SSH_AGENT_NOT_VERIFIED` is shown when enabling without a passing test. `SSH_MODULE_NOT_SUPPORTED` is returned for server types whose module has no agent profile.

</li>
<li>

**Enable.** Toggle **Customers on this server may manage SSH keys** on. Enabling is refused if another already-enabled server uses the same agent host and port (`SSH_AGENT_ENDPOINT_IN_USE`). Changing the agent host, port, user, key, or pinned fingerprint clears the test and requires re-testing before re-enabling.

</li>
</Steps>

### Rotate identity

Click **Rotate identity** to issue a new key for the same server binding. Client SSH key access stops immediately until the installer is run again with the new command shown.

For disaster recovery, **Advanced** lets you paste an existing private key instead of generating a new one.

## Agent configuration (`/etc/hostpanel-ssh-key-agent/agent.conf`)

The installer writes the base configuration from the chosen profile. An administrator can edit it directly on the server. The defaults below are the profile values written by the installer; the agent's own built-in defaults differ (for example `max_keys` is 50 built-in, 20 in both profiles; `deny_accounts` and `account_check_command` are profile-specific).

<SettingTable :settings="[
  { name: 'provider', type: 'string', default: 'generic-linux or the id a panel plugin ships', description: 'Agent profile name. Set by the installer; reported on ping.' },
  { name: 'binding', type: 'string', default: '', description: 'UUID of the Hostpanel server record. Set by the installer; checked on Test agent.' },
  { name: 'min_uid', type: 'number', default: '1000', description: 'Minimum numeric uid a customer account must have. System accounts below this value are always refused.' },
  { name: 'home_root', type: 'string', default: '/home', description: 'Expected parent of every customer home directory. An account whose passwd home is not directly under this path is refused.' },
  { name: 'deny_accounts', type: 'string', default: 'root,admin,nobody,mysql,postfix,dovecot,named,apache,nginx,hpsshkeys', description: 'Comma-separated account names that are always refused regardless of uid or home. Both profiles add hpsshkeys; the built-in default does not include it.' },
  { name: 'max_keys', type: 'number', default: '20 (profiles); 50 (built-in)', description: 'Maximum number of managed keys per account. Attempts to add beyond this return too_many_keys.' },
  { name: 'audit_log', type: 'string', default: '/var/log/hostpanel-ssh-key-agent.log', description: 'Root-only file where every add and revoke is recorded with fingerprint (never key material).' },
  { name: 'lock_dir', type: 'string', default: '/run/hostpanel-ssh-key-agent', description: 'Directory for per-account lock files that serialise concurrent operations on the same account.' },
  { name: 'account_check_command', type: 'string', default: 'set by a panel plugin profile; empty in generic-linux', description: 'Absolute path of a root-owned helper run as helper <account>. Exit 0 = panel account exists, 1 = not found, anything else = fault.' },
  { name: 'check_shadow_lock', type: 'boolean', default: 'true', description: 'A locked password (usermod -L) makes the account account_suspended. !! or * alone is not treated as a lock.' },
  { name: 'ssh_group', type: 'string', default: '', description: 'If set, only members of this group may receive keys. Leave empty when jail_enrol is on.' },
  { name: 'eligible_list', type: 'string', default: '', description: 'Path to a root-owned file, one account per line, that acts as an SSH opt-in list. The file must not be group- or world-writable.' },
  { name: 'jail_enrol', type: 'boolean', default: 'false', description: 'When true, adding the first key enrols the account in a jailkit chroot; revoking the last key withdraws it. Requires jailkit installed. Agent 1.2.0 and later.' },
  { name: 'jail_root', type: 'string', default: '/home/jail', description: 'Parent directory of all jails; must be root-owned.' },
  { name: 'jail_group', type: 'string', default: 'sftp-ssh', description: 'Group that the customer sshd chroots (used in Match Group / ChrootDirectory). Leave ssh_group empty when jail_enrol is on.' },
  { name: 'jail_sections', type: 'string', default: 'set by the profile', description: 'jk_init sections copied into a new jail. Set by the profile (generic-linux: basicshell,extendedshell,editors,netutils,terminfo), no built in default; enrolment with none is refused. With jail_enrol on, install.sh --check warns for each section missing from /etc/jailkit/jk_init.ini.' },
  { name: 'jail_shell', type: 'string', default: '/bin/bash', description: 'Login shell set when a customer is enrolled into the jail.' },
  { name: 'jail_extras', type: 'string', default: 'empty (the jail-extras helper lists the deploy tools)', description: 'Extra paths copied into each jail with jk_cp when the jail lacks one or its host copy changed (missing in the jail, another size, or newer). Paths missing on the host are skipped, and an entry already in place is not copied again. An entry that is or contains (symlinks followed) private key material is refused and install.sh --check warns: a private directory, or a file named *.key, ssh_host_*_key, privkey*.pem, *.p12, *.pfx or *.jks; for outgoing TLS list /etc/pki/tls/certs, /etc/pki/ca-trust/extracted and /etc/ssl/certs, never /etc/pki/tls. Includes dircolors and shell profile entries needed by bash login; rsync, tar, gzip and unzip let CI/CD deploy sites over SSH. Added to what the jail-extras helper of the provider lists; the private key check covers both.' },
  { name: 'systemd_unit_dir', type: 'string', default: '/etc/systemd/system', description: 'Directory where the home bind-mount systemd unit is written for each enrolled account.' },
  { name: 'helper_dir', type: 'string', default: '/usr/local/libexec/hostpanel-ssh-key-agent', description: 'Directory of the provider helpers behind the website and database operations (ensure-website, remove-website, ensure-database, remove-database). An operation without its helper answers not_supported. Agent 1.5.0 and later.' },
  { name: 'web_server', type: 'string', default: '(empty)', description: 'apache or nginx. Read by the generic-linux website helpers. Empty means the one that is running; set it when both or neither is running, or the website operations are refused. Agent 1.6.0 and later.' },
  { name: 'host_key_file', type: 'string', default: '/etc/ssh/ssh_host_ed25519_key.pub', description: 'The public host key of the server, reported on ping and on add so Hostpanel can show it to the customer and hand it to a plugin. Point it at the key of the sshd the customers connect to. Empty, a missing file, or a file that is not exactly one ED25519 key line reports no host key. Agent 1.6.0 and later.' }
]" />

## Jail extras hold only public trust material

A jail is a customer's own environment, so `jail_extras` may list only public files. The agent refuses an entry that is, or contains, private key material. It walks every entry and everything below it, follows symlinks, and treats a path as private when:

- one of its components is named `private` (such as `/etc/pki/tls/private`), or
- its file name matches `*.key`, `ssh_host_*_key`, `privkey*.pem`, `*.p12`, `*.pfx` or `*.jks` (the match ignores case).

Public halves such as `*.pub`, `*.crt` and `fullchain.pem` are fine. An entry that does not exist on the server is skipped, as it is when copying.

When an entry is refused, the agent does not enrol the account: the call fails with `internal_error` before anything is built, and the message names the entry. `install.sh --check` (the agent's `--check-config`) warns about such an entry and prints the safe trust paths. It exits with 1 for an unsafe entry, and with 2 when the configuration cannot be read, which `install.sh` reports as a failed check.

For outgoing TLS in a jail (git, curl) list the certificate directories, never their parent `/etc/pki/tls`, which holds the `private` directory:

    jail_extras = /usr/bin/uname,/usr/bin/id,/usr/bin/hostname,/usr/bin/dircolors,/usr/bin/rsync,/usr/bin/tar,/usr/bin/gzip,/usr/bin/unzip,/etc/bashrc,/etc/profile.d/colorls.sh,/etc/DIR_COLORS,/etc/pki/tls/certs,/etc/pki/ca-trust/extracted,/etc/ssl/certs

## A jail for site deployers {#jail-for-deployers}

A bare jailkit tree holds a shell and little else, so a jailed account could not deploy a site. From agent 1.7.0 the `generic-linux` profile ships a `jail-extras` helper (`/usr/local/libexec/hostpanel-ssh-key-agent/jail-extras`). The agent runs it on every enrolment and copies what it lists into the jail, on top of `jail_extras`:

- the deploy tools that are installed, found with `command -v`: flock, setsid, nohup, awk, basename, cut, find, grep, head, ln, mkdir, mktemp, mv, readlink, rm, sed, sha256sum, sort, tail, tar, touch, tr, curl, gzip, gunzip, mysql, mysqldump, base64, and `rsync` and `unzip` when they are present;
- every installed PHP command line binary with its ini directory, its directory of additional ini files (and the directories the links in it lead to) and its extension directory;
- the CA bundle found in the safe trust paths (`/etc/ssl/certs/ca-certificates.crt`, or `/etc/pki/tls/certs/ca-bundle.crt` with its target on RHEL and AlmaLinux).

The agent joins the helper's paths and `jail_extras` into one list and runs the private key check over that list, so one code path guards both: a path that is or contains a private key stops the enrolment with `internal_error`, and `install.sh --check` reports it. The helper only prints paths and never copies anything; run it by hand with `jail-extras /etc/ssl/certs` to see what a server would give a jail.

The jail sections are the profile's choice too. The `generic-linux` profile sets `jail_sections = basicshell,extendedshell,editors,netutils,terminfo` and a panel plugin's profile sets its own sections and extras; the agent has no built in default for either. When `jail_enrol` is on, `install.sh --check` warns for each configured section that `/etc/jailkit/jk_init.ini` does not define, or for all of them when jailkit is not installed.

## Websites and databases

Agent 1.5.0 can give a hosting account a website and databases on request. It runs one root-owned helper per operation from `helper_dir`; the `generic-linux` profile ships all four, so nothing needs configuring on a plain Linux server, and a panel plugin's profile may ship its own. A server without a helper answers `not_supported`, and the ping answer lists the operations that are available (`capabilities`) and the PHP versions the server offers (`php_versions`, for example `["8.1", "8.2", "8.3"]`, empty when the server has no `php-versions` helper).

| Operation | Request fields | Success answer |
|-----------|----------------|----------------|
| `ensure-website` | `account`, `hostname`, `docroot`, optional `php`, optional `link_root` | `changed` |
| `remove-website` | `account`, `hostname` | `changed` |
| `ensure-database` | `account`, `label` | `created`, `database`, `username`, and `password` only when `created` is true |
| `remove-database` | `account`, `label` | `changed` |

- `hostname` must be the account's own domain or a subdomain of it.
- `docroot` is relative to the account's home, never leaves it, and never points into `.ssh` or `.conf`.
- `php` (for example `8.3`) selects the installed php-fpm of that version, which must be one of the ping answer's `php_versions` (otherwise `php_version_unsupported`); `link_root` marks the document root as a symlink that deploys switch, which adds the php-fpm pool lines such a switch needs behind Apache and needs `php`.
- `label` is 1 to 15 lower case letters and digits starting with a letter; the database and its user are called `<account>_<label>`. An account may have 10.

The helpers are idempotent, keep a root-owned record of what they created, and `remove-*` only touches that: a website or database that someone else made is refused with `site_not_owned`. Every file they edit is backed up first, Apache and php-fpm configuration is tested before a graceful reload, and a failed test restores the files. The database password is generated on the server, sent back only in the answer that created the database, and never written to any log; if it is lost, remove the database and create it again.

Errors added in 1.5.0: `not_supported`, `site_not_owned`, `website_conflict`, `php_version_unsupported`, `website_failed`, `database_failed`, `database_limit`. The full request and answer shapes are in the agent's `README.md`.

### Plain Linux servers (`generic-linux`)

A server added with the **Linux server (no control panel)** module (`Linux`) gets this profile with no plugin involved; the SSH dialog offers the built-in bundle for it, and orders never choose such a server.

Agent 1.6.0 ships website and database helpers for the `generic-linux` profile, so a server with no hosting panel can host websites too (`install.sh --provider generic-linux` puts them in `helper_dir`). They need Debian, Ubuntu, RHEL or AlmaLinux with Apache or nginx and, for PHP, php-fpm.

- **Web server.** The one that is running. When both or neither run, the website operations are refused and the ping answer lists none until `web_server` in `agent.conf` is set to `apache` or `nginx`. Virtual hosts are written to `/etc/apache2/sites-available/hp-<hostname>.conf` (Debian Apache, enabled with `a2ensite`, `proxy_fcgi` enabled with `a2enmod`), `/etc/httpd/conf.d/hp-<hostname>.conf` (RHEL Apache) or `/etc/nginx/conf.d/hp-<hostname>.conf` (nginx), and tested with `apache2ctl -t`, `httpd -t` or `nginx -t` before a reload.
- **PHP.** Debian and Ubuntu with the sury packages, RHEL with Remi, and the RHEL AppStream are recognised. A version is offered when its php-fpm, pool directory and command line binary are all there. The ping answer lists them in `php_versions` and gives the command line binary of each in `php_cli` (for example `{"8.3": "/usr/bin/php8.3"}`); the `ensure-website` answer carries `php_cli` too, so a deploy can run the right PHP.
- **Pools.** One php-fpm pool per account and PHP version (`hp-<account>.conf`), shared by the account's websites on that version and removed with the last of them.
- **Document root.** The home gets `o+x` and missing directories are created owned by the account with mode 755. With `link_root` the last path component is never created, the deploy links it. nginx passes the resolved path (`$realpath_root`) to PHP; behind Apache the pool revalidates OPcache and the realpath cache on every request.
- **What it may touch.** A website whose hostname is registered to another account answers `site_not_owned`; a virtual host or pool that the agent did not write answers `website_conflict` and is never changed. `remove-website` only removes what the agent registered in `/var/lib/hostpanel-ssh-key-agent/<account>/`. Database operations are offered only when MySQL or MariaDB is there (next item).
- **Databases.** Offered when a `mariadb` (else `mysql`) client is installed and one of the units `mysql`, `mysqld` or `mariadb` is running; otherwise the ping answer lists no database operation. The helpers act as the database administrator through `/root/.my.cnf` when it exists, else through root's socket authentication. `ensure-database` creates the database and a user `<account>_<label>` for `localhost` and `127.0.0.1` only, at most 10 per account. The password is returned once, by the call that creates the database, and the agent never stores or logs it. The registry entry is written before anything is created, and `remove-database` drops only a database with an entry; any other answers `site_not_owned`.
- **SELinux.** While SELinux is enforcing (`getenforce` prints `Enforcing`, the default on RHEL and AlmaLinux), `ensure-website` is refused with `website_failed` and changes nothing: the virtual hosts, pools and document roots the helper writes are not labelled for SELinux, so the web server could not read them. Set SELinux to permissive or disabled, or serve the site by hand, until labelling is supported.

`/usr/local/libexec/hostpanel-ssh-key-agent/detect` prints what the helpers found on a server, including the database client and unit.

## Limiting a key to some addresses, and the host key {#source-restriction-and-host-key}

Agent 1.8.0 and later can limit where a key may be used from. A plugin that issues an SSH key (the `IPluginHostSshSourceApi` of Hostpanel.Sdk 3.16.0) names the addresses or CIDR networks, and the agent writes them into the key's line in `authorized_keys`:

```
from="203.0.113.7,198.51.100.0/24" ssh-ed25519 AAAA... hostpanel:<key id>
```

A connection from any other address is refused by the server. A key without a list works from anywhere, as before, and a client's own keys never have one. A list holds 1 to 16 distinct entries, each a plain IPv4 or IPv6 address or a CIDR network whose host bits are zero (`198.51.100.0/24`, not `198.51.100.5/24`). Host names, wildcards and negations are refused, so nothing but an address reaches the key line. The same key id asked again with another list answers `key_id_conflict`: revoke the key and add it again to change its sources. The sources stay with the key: when a suspended service is unsuspended, the key comes back with the same list. The client sees the list next to the key on the service's **SSH Keys** tab.

`ping` and `add` also return the server's public host key (`host_key`, one `ssh-ed25519 ...` line without a comment), read from `host_key_file` (default `/etc/ssh/ssh_host_ed25519_key.pub`). The host hands it to the plugin with the access it issued, so the plugin can pin the key from the first connection instead of trusting whatever a first scan shows. Set `host_key_file` to the key of the sshd customers connect to when that is not the default one. An agent older than 1.6.0 reports no host key and refuses a key with a list (`invalid_request`), so update the agent before a plugin asks for sources.

## Keys over a service's life

| Event | Keys on the server | In Hostpanel |
|-------|--------------------|--------------|
| Service suspended in Hostpanel | Taken off the server (a key not confirmed removed stays and is logged for staff) | Suspended; put back under the same ID on unsuspension; InstallFailed if not confirmed |
| Account suspended in the panel only | Stay and keep logging in, the password lock does not stop keys. Suspend through Hostpanel instead | Unchanged |
| Service terminated | Revoked automatically | Revoked; if the server did not confirm, RevokeFailed with reason, visible to staff |
| Service moved to another server | Stay on the old server until manually revoked | Listed as from the previous server; nothing is sent to the new server |
| SSH key management switched off | Stay | Client can still revoke but cannot add |
| Agent uninstalled | Stay unless revoke-all ran first | Revokes fail (RevokeFailed) until the agent is reinstalled |
| Identity rotated | Unaffected | Client SSH access off until the installer runs with the new key |
| Plugin removed | Every key issued to the plugin is revoked automatically | Revoked; if the server did not confirm, RevokeFailed with reason and retried until it is |

## Agent error codes

These codes come from the agent itself and appear in Hostpanel's error responses or staff logs.

| Code | Meaning |
|------|---------|
| `invalid_account` | Account name fails the server's naming rules |
| `account_not_found` | No passwd entry for this account |
| `account_not_eligible` | Account exists but is excluded (not in `eligible_list`, not in `ssh_group`, or the provider's panel check failed) |
| `account_suspended` | Account's password is locked (`usermod -L`) and `check_shadow_lock` is on |
| `invalid_key` | Public key is not a valid ED25519 key |
| `duplicate_key` | This key fingerprint already exists under a different `key_id` |
| `key_id_conflict` | Same `key_id`, different key material or a different list of sources |
| `too_many_keys` | Account has reached the maximum number of managed keys |
| `ssh_disabled` | Account's shell is a disabled shell (e.g. `/sbin/nologin`) and `jail_enrol` is off |
| `unsafe_path` | The `.ssh` directory or `authorized_keys` file is a symlink or owned unexpectedly |
| `file_too_large` | `authorized_keys` exceeds the size limit |
| `not_supported` | No helper for this operation on the server |
| `site_not_owned` | The hostname is not the account's domain, or a vhost or database of that name exists but the agent did not create it |
| `website_conflict` | A vhost the panel made serves a different document root |
| `php_version_unsupported` | The requested php-fpm version is not installed |
| `website_failed` | A website operation failed; a failed configuration test restores the files |
| `database_failed` | A database operation failed; a failed creation drops what it made |
| `database_limit` | The account already has 10 databases created by the agent |
| `timeout` | Request took too long (stdin: 10 s, total: 60 s) |
| `internal_error` | Agent-side failure; details in the server's audit log |

## Uninstall

Before uninstalling the agent, revoke all managed keys so clients cannot continue logging in with orphaned keys. There is no button in the admin panel yet, use the admin API directly:

1. Call `POST /api/admin/servers/{id}/ssh-keys/revoke-all`. This removes all managed keys from the server.
2. On the server, in the unpacked installer bundle, run:

```bash
./uninstall.sh           # removes management key, sudoers entry, hpsshkeys user and agent binary; keeps agent.conf
./uninstall.sh --purge   # also removes agent.conf (audit log is always kept)
./uninstall.sh --dry-run # preview only
```

The uninstaller never touches sshd, the firewall, jailkit, or customers' files.

To verify no managed keys were left behind after uninstalling:

```bash
grep -rl 'hostpanel:' /home/*/.ssh/authorized_keys
```

## What customers see

Clients manage their SSH keys from the **SSH Keys** tab on their service in the client portal. They can add a key by pasting a public key and giving it a label, see the fingerprint and label of each installed key, and revoke any key individually. Adding and revoking operations go directly to the agent on the server; only the key fingerprint is stored in Hostpanel, never the private key.

A plugin that declares SSH host access can be issued a key for a client's service whose product the operator bound one of the plugin's service widgets to on the **Plugins** page. A product listed only in the plugin's own manifest does not count: until an operator binds it, the plugin reaches no hosting. Hostpanel generates the key pair, installs the public half through this agent exactly like a key the client adds, and hands the private key to the plugin once; Hostpanel never stores or logs it. Such a key is listed on the service's **SSH Keys** tab like any other, and the client can revoke it at any time. Switching the plugin off keeps its keys (the plugin cannot use the host while it is off); removing the plugin revokes them.

Add and revoke requests from the client portal are rate-limited to 20 operations per 60 seconds per client (`SshKeys` policy). This can be adjusted in the deployment's rate limit settings.

See the [client portal guide](/customers/portal) for the full walkthrough.
