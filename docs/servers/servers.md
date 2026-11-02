---
title: Servers and server groups
---

# Servers and server groups <Label type="admin" />

Go to **Servers** to manage provisioning servers and load-balancing groups. Subtitle: "Manage provisioning servers and load-balancing groups."

## The server list

Servers are listed with the columns **Server Name**, **IP Address**, **Accounts**, **Status**, and **Actions**. When the connection is healthy the status reads "Online · _n_ accounts · _version_".

**Status badges:** Online, Offline, Not tested.

**Row actions:** Test Connection, SSH, Edit, Delete.

**DEFAULT** badge marks the default server for its module, used when a product has no server group and only one module is in use across all servers.

<Callout type="note">
Only <strong>CWP7</strong> and <strong>Plesk</strong> are fully supported provisioning modules at this time. The other entries in the Module list (cPanel, DirectAdmin, CentOS, Lukittu, Virtualmin, HyperVm) are stored but do not provision accounts automatically and cannot be tested.
</Callout>

## Add a server

Click **Add New Server**. The form has three tabs.

### General tab

| Field | Notes |
|-------|-------|
| Name | A friendly label shown in the panel |
| Hostname | Used for API connections to this server |
| IP Address | The server's main public IP address |
| Assigned IP Addresses | Additional IPs (one per line) |
| Monthly Cost (USD) | For your own records |
| Datacenter / NOC | e.g. Hetzner, OVH |
| Max Accounts | Leave blank for unlimited |
| Server Status Address | Full path to a status page shown on the client portal status monitor |
| Default server | Mark as the default for this module |
| Disable this server | Prevents new accounts being assigned here |

### Nameservers tab

Up to five nameservers (hostname and IP each) shown to clients when an account is provisioned on this server: Primary, Secondary, Third, Fourth, Fifth.

### Details tab

| Field | Notes |
|-------|-------|
| Module | Control panel: cPanel, Plesk, DirectAdmin, CentOS Web Panel, CWP7, Lukittu, Virtualmin, HyperVm |
| Username | API username |
| Password | API password (stored encrypted) |
| API Token | API token where the module uses token-based auth (stored encrypted) |
| Access Hash | Used by cPanel/WHM and CWP7 for token-based authentication (stored encrypted) |
| Use SSL for connections | Enables HTTPS for API requests |

When editing, credential fields show **(blank = keep)**. Secrets are stored encrypted with `ENCRYPTION_KEY`. There is no port field; the panel API port is fixed per module.

## Test a connection

Click **Test Connection** on a server row. Hostpanel calls the server's panel API and updates the status badge. The result is shown inline, not as a page error.

**Success:** "Online · _n_ accounts · _version_"

**Failure codes shown in the test result:**

| Code | What to check |
|------|---------------|
| `SERVER_CONNECTION_UNREACHABLE` | Hostname or IP is wrong, the panel API port is blocked, or the panel is down |
| `SERVER_CONNECTION_NO_ACCESS_HASH` | No access hash is stored for this server; add one on the Details tab |
| `SERVER_CONNECTION_NO_API_KEY` | No API key or access hash is stored; add credentials on the Details tab |
| `SERVER_CONNECTION_MODULE_UNSUPPORTED` | This module has no connection probe; testing is not available for it |

## Edit and delete a server

Click a server row or the **Edit** row button to open the form. Operations on a server that no longer exists return `SERVER_NOT_FOUND`. Deleting a server that still hosts client services or SSH keys is refused (`SERVER_IN_USE`).

## Server groups

Server groups let you assign a set of servers to a product. New orders spread across the group according to the fill type.

### Create a group

In the **Groups** section below the server list, click **Create Group**.

| Field | Notes |
|-------|-------|
| Group Name | e.g. "CWP Servers" |
| Fill Type | How accounts are distributed (see below) |
| Servers | Move servers from Available into In Group |

### Fill types

| Fill Type | Label |
|-----------|-------|
| LeastFull | Add to the least full server |
| FillUntilFull | Fill active server until full then switch |

<Callout type="warning">
At the time of writing the server selector does not read the group's fill type. Both options behave as "least full".
</Callout>

**How a server is picked:** disabled servers and servers that have reached their account limit are skipped. Among the eligible servers, the one with the lowest fill ratio (accounts ÷ max accounts) wins. Servers with unlimited capacity or an unknown count have a fill ratio of 0. Ties go to the server with the higher account limit; further ties are broken by name.

### Delete a group

Deleting a group that products still provision into is refused (`SERVER_GROUP_IN_USE`). Deleting an unused group unassigns all its servers but does not delete them.

## Private network routes

When a server's control panel API is reachable only over a VPN or private network, set `PANEL_HOST_ADDRESSES` in the deployment environment as `hostname=address` pairs (comma-separated, e.g. `srv4.example.com=10.77.0.1`). Hostpanel dials the private address but keeps the hostname for the request and TLS certificate check, so customer-facing links and certificate checks are unchanged.

See [Configuration reference](/operators/configuration) for the full list of environment settings.
