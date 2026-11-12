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

**Supported server modules:** only CWP7 maps to an agent profile (`cwp`). All other server types show "SSH key management not available" to clients.

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
| `--provider <profile>` | Agent profile: `cwp` for CWP7, `generic-linux` for plain Linux |
| `--binding <id>` | Ties this installation to the Hostpanel server record |
| `--config <file>` | Use a custom config file instead of the profile's default (advanced) |
| `--dry-run` | Preview what would be installed without making any changes |
| `--check` | Diagnose an existing installation (user, lock, owner/mode, sudoers, forced-command key, config, self-test, host key) |

Running the installer again upgrades the agent binary and updates the binding while keeping the existing `agent.conf`. The installer creates and writes:

- `/usr/local/sbin/hostpanel-ssh-key-agent` — the agent binary
- `/etc/sudoers.d/hostpanel-ssh-key-agent` — the sudoers rule
- `/etc/hostpanel-ssh-key-agent/agent.conf` — the agent configuration (from the profile)
- `/var/lib/hpsshkeys/.ssh/authorized_keys` — the management user's forced-command key

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

- **Agent reachable** — agent responded and the binding matches.
- **Agent unreachable** — connection failed; see "Last check failed" for the reason.
- **Not tested** — settings were changed since the last test.

The agent version is shown as "Agent _version_" or "Agent _version_ (_provider_)". A successful test is required before the server can be enabled. Changing the agent host, port, user, key, or pinned fingerprint clears the test.

If the test fails the reason is shown as "Last check failed: _reason_" (`servers.ssh.failures.*`):

| Reason | Meaning |
|--------|---------|
| the server could not be reached | No network path to the agent host (`Unreachable`) |
| the agent timed out | Connection opened but the agent did not respond in time (`Timeout`) |
| the agent was installed for a different server | Correct host but wrong binding — check the agent host or run this server's install command on it (`BindingMismatch`) |
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
  { name: 'provider', type: 'string', default: 'cwp / generic-linux', description: 'Agent profile name. Set by the installer; reported on ping.' },
  { name: 'binding', type: 'string', default: '', description: 'UUID of the Hostpanel server record. Set by the installer; checked on Test agent.' },
  { name: 'min_uid', type: 'number', default: '1000', description: 'Minimum numeric uid a customer account must have. System accounts below this value are always refused.' },
  { name: 'home_root', type: 'string', default: '/home', description: 'Expected parent of every customer home directory. An account whose passwd home is not directly under this path is refused.' },
  { name: 'deny_accounts', type: 'string', default: 'root,admin,nobody,mysql,postfix,dovecot,named,apache,nginx,hpsshkeys', description: 'Comma-separated account names that are always refused regardless of uid or home. Both profiles add hpsshkeys; the built-in default does not include it.' },
  { name: 'max_keys', type: 'number', default: '20 (profiles); 50 (built-in)', description: 'Maximum number of managed keys per account. Attempts to add beyond this return too_many_keys.' },
  { name: 'audit_log', type: 'string', default: '/var/log/hostpanel-ssh-key-agent.log', description: 'Root-only file where every add and revoke is recorded with fingerprint (never key material).' },
  { name: 'lock_dir', type: 'string', default: '/run/hostpanel-ssh-key-agent', description: 'Directory for per-account lock files that serialise concurrent operations on the same account.' },
  { name: 'account_check_command', type: 'string', default: 'set by cwp profile; empty in generic-linux', description: 'Absolute path of a root-owned helper run as helper <account>. Exit 0 = panel account exists, 1 = not found, anything else = fault.' },
  { name: 'check_shadow_lock', type: 'boolean', default: 'true', description: 'A locked password (usermod -L) makes the account account_suspended. !! or * alone is not treated as a lock.' },
  { name: 'ssh_group', type: 'string', default: '', description: 'If set, only members of this group may receive keys. Leave empty when jail_enrol is on.' },
  { name: 'eligible_list', type: 'string', default: '', description: 'Path to a root-owned file, one account per line, that acts as an SSH opt-in list. The file must not be group- or world-writable.' },
  { name: 'jail_enrol', type: 'boolean', default: 'false', description: 'When true, adding the first key enrols the account in a jailkit chroot; revoking the last key withdraws it. Requires jailkit installed. Agent 1.2.0 and later.' },
  { name: 'jail_root', type: 'string', default: '/home/jail', description: 'Parent directory of all jails; must be root-owned.' },
  { name: 'jail_group', type: 'string', default: 'sftp-ssh', description: 'Group that the customer sshd chroots (used in Match Group / ChrootDirectory). Leave ssh_group empty when jail_enrol is on.' },
  { name: 'jail_sections', type: 'string', default: 'basicshell,editors,extendedshell,netutils,terminfo', description: 'jk_init sections copied into a new jail.' },
  { name: 'jail_shell', type: 'string', default: '/bin/bash', description: 'Login shell set when a customer is enrolled into the jail.' },
  { name: 'jail_extras', type: 'string', default: '/usr/bin/uname,/usr/bin/id,/usr/bin/hostname,/usr/bin/dircolors,/usr/bin/rsync,/usr/bin/tar,/usr/bin/gzip,/usr/bin/unzip,/etc/bashrc,/etc/profile.d/colorls.sh,/etc/DIR_COLORS', description: 'Extra paths copied into each jail with jk_cp when the jail lacks one or its host copy changed (missing in the jail, another size, or newer). Paths missing on the host are skipped, and an entry already in place is not copied again. Includes dircolors and shell profile entries needed by bash login; rsync, tar, gzip and unzip let CI/CD deploy sites over SSH.' },
  { name: 'systemd_unit_dir', type: 'string', default: '/etc/systemd/system', description: 'Directory where the home bind-mount systemd unit is written for each enrolled account.' }
]" />

## Keys over a service's life

| Event | Keys on the server | In Hostpanel |
|-------|--------------------|--------------|
| Service suspended in Hostpanel | Taken off the server (a key not confirmed removed stays and is logged for staff) | Suspended; put back under the same ID on unsuspension; InstallFailed if not confirmed |
| Account suspended in the panel only | Stay and keep logging in — the password lock does not stop keys. Suspend through Hostpanel instead | Unchanged |
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
| `key_id_conflict` | Same `key_id`, different key material |
| `too_many_keys` | Account has reached the maximum number of managed keys |
| `ssh_disabled` | Account's shell is a disabled shell (e.g. `/sbin/nologin`) and `jail_enrol` is off |
| `unsafe_path` | The `.ssh` directory or `authorized_keys` file is a symlink or owned unexpectedly |
| `file_too_large` | `authorized_keys` exceeds the size limit |
| `timeout` | Request took too long (stdin: 10 s, total: 60 s) |
| `internal_error` | Agent-side failure; details in the server's audit log |

## Uninstall

Before uninstalling the agent, revoke all managed keys so clients cannot continue logging in with orphaned keys. There is no button in the admin panel yet — use the admin API directly:

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

Clients manage their SSH keys from the **SSH Keys** tab on their service in the client portal. They can add a key by pasting a public key and giving it a label, see the fingerprint and label of each installed key, and revoke any key individually. Adding and revoking operations go directly to the agent on the server; only the key fingerprint is stored in Hostpanel — never the private key.

A plugin that declares SSH host access can be issued a key for a client's service whose product the operator bound one of the plugin's service widgets to on the **Plugins** page. A product listed only in the plugin's own manifest does not count: until an operator binds it, the plugin reaches no hosting. Hostpanel generates the key pair, installs the public half through this agent exactly like a key the client adds, and hands the private key to the plugin once; Hostpanel never stores or logs it. Such a key is listed on the service's **SSH Keys** tab like any other, and the client can revoke it at any time. Switching the plugin off keeps its keys (the plugin cannot use the host while it is off); removing the plugin revokes them.

Add and revoke requests from the client portal are rate-limited to 20 operations per 60 seconds per client (`SshKeys` policy). This can be adjusted in the deployment's rate limit settings.

See the [client portal guide](/customers/portal) for the full walkthrough.
