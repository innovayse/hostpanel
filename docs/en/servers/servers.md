---
title: Servers and server groups
description: "Add and test provisioning servers in Hostpanel, edit or delete them, group them for load balancing and set private network routes."
keywords: "servers, server groups, load balancing, connection test, private network routes"
---

# Servers and server groups <Label type="admin" />

<DemoLink path="/servers" />

Go to **Servers** to manage provisioning servers and load-balancing groups. Subtitle: "Manage provisioning servers and load-balancing groups."

## The server list

Servers are listed by name as cards, 20 to a page. Each card shows **Server Name**, **IP Address**, **Accounts**, **Status**, and **Actions**. Click **Edit** on a row to open its settings. When the connection is healthy the status reads "Online · _n_ accounts · _version_".

**Status badges:** Online, Offline, Not tested.

Type part of a name, host name or IP address in the search box to find a server. The **Filters** button narrows the list by module and by status (Online, Offline or Not tested); applied filters show as chips you can remove. The search, the filters and the page stay in the address, so a link or a reload opens the same list. Search treats `%` and `_` as ordinary characters. Screens that need to choose a server (the group form, adding an existing service and a service's server) and the dashboard's capacity card read every server regardless of the page.

**Row actions:** Test Connection, SSH (opens the SSH key management dialog), Edit, Delete.

**DEFAULT** badge marks the default server for its module. When other things are equal, the default server is preferred within its group or module when a server is picked for a new account (see [Fill types](#fill-types)).

<Callout type="note">
<strong>cPanel</strong> provisions accounts automatically and is built in; it is new and not yet tested against a live WHM server, see <a href="./cpanel">cPanel servers</a>. Every other panel is provisioned by its plugin: install and switch on the plugin, and its panel types (for example DirectAdmin) appear in the Module list, ready to pick, to test and to provision accounts on. An entry in the list with no plugin behind it (DirectAdmin, Lukittu, Virtualmin, HyperVm) is stored but does not provision accounts automatically and cannot be tested. A server that uses a panel type whose plugin is switched off answers that the plugin is not available. Control Web Panel (CWP) is supported through the CWP plugin: install it from the plugin registry, switch it on in Integrations, and note that a newly installed plugin is loaded at the next start.
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
| Default server | Mark as the default for this module. It is preferred within its group or module when other things are equal |
| Disable this server | Prevents new accounts being assigned here |

### Nameservers tab

Up to five nameservers (hostname and IP each) shown to clients when an account is provisioned on this server: Primary, Secondary, Third, Fourth, Fifth.

### Details tab

| Field | Notes |
|-------|-------|
| Module | Control panel type: cPanel and the types of the installed panel plugins; a server saved with an older type keeps it while you edit it |
| Username | API username |
| Password | API password (stored encrypted) |
| API Token | API token where the module uses token-based auth (stored encrypted) |
| Access Hash | Used by some panels for API authentication, as the panel's plugin describes (stored encrypted). cPanel uses Username and API Token instead |
| Use SSL for connections | Enables HTTPS for API requests. Ignored for cPanel, which is always reached over HTTPS |

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
| `PANEL_PLUGIN_UNAVAILABLE` | No installed plugin serves this module, or the plugin is switched off, not configured or not working; check the Plugins page |

## Edit and delete a server

Click **Edit** on a row to open the form. Operations on a server that no longer exists return `SERVER_NOT_FOUND`. Deleting a server that still hosts client services or SSH keys is refused (`SERVER_IN_USE`).

## Server groups

Server groups let you assign a set of servers to a product. New orders spread across the group according to the fill type.

### Create a group

In the **Groups** section below the server list, click **Create Group**.

| Field | Notes |
|-------|-------|
| Group Name | e.g. "Production servers" |
| Fill Type | How accounts are distributed (see below) |
| Servers | Move servers from Available into In Group |

### Fill types

| Fill Type | Label |
|-----------|-------|
| LeastFull | Add to the least full server |
| FillUntilFull | Fill active server until full then switch |

**Servers that are never picked:** with either fill type, disabled servers and full servers are skipped. A server is full when its account count has reached its Max Accounts. A Max Accounts of 0 counts as full. A server with no maximum (the field left blank) is never full.

**LeastFull:** the server with the lowest fill ratio (accounts ÷ max accounts) wins. Servers with no maximum have a fill ratio of 0. Ties go to the default server, then to the server with the larger capacity, then to the first by name.

**FillUntilFull:** the default server is used first. After it, servers that have a maximum are used in name order. Servers with no maximum come last, as overflow. A server is filled until it reaches its maximum, then the next one is used.

**Without a group:** when a product has no server group, LeastFull always applies, whatever fill types your groups use.

**Default server:** in every case the default server is preferred within its group or module when other things are equal.

<Callout type="note">
Account counts come from connection tests and from the Sync Server Account Counts job, so the fill shown and used for selection is approximate between syncs.
</Callout>

### Delete a group

Deleting a group that products still provision into is refused (`SERVER_GROUP_IN_USE`). Deleting an unused group unassigns all its servers but does not delete them.

## Private network routes

When a server's control panel API is reachable only over a VPN or private network, fill in **API address** on the server itself (Servers, edit the server, General), for example `10.77.0.1`. Hostpanel then dials that address for API calls, and hands it to a hosting panel plugin as the host name of its connection, while the hostname keeps building the control panel and webmail links shown to clients. When the server accepts a self signed certificate, that is the address whose certificate is accepted. Leave it empty and the API is dialled at the hostname, as before.

An administrator sets this per server in the admin panel, so adding a server behind a VPN needs no change to the deployment. A deployment that would rather pin the routes in its environment can still use `PANEL_HOST_ADDRESSES` as `hostname=address` pairs (comma-separated, e.g. `srv4.example.com=10.77.0.1`); it dials the private address but keeps the hostname for the request and TLS certificate check.

See [Configuration reference](/operators/configuration) for the full list of environment settings.
