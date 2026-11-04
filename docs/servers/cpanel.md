---
title: cPanel servers
description: Connect a cPanel and WHM server so Hostpanel creates and manages hosting accounts on it.
---

# cPanel servers <Label type="admin" />

Hostpanel manages cPanel accounts through the WHM API 1. One service is one cPanel account.

<Callout type="warning">
This integration has not been tested against a live WHM server yet. The calls follow the cPanel
WHM API 1 reference and are tested against a stub server only. Try it on a test server before you
use it for customers, and report what you find.
</Callout>

## What it does

| Action in Hostpanel | WHM API 1 function |
|---|---|
| Create | `createacct` with `username`, `domain`, `password` and `plan` |
| Suspend | `suspendacct` with `user` and `reason` |
| Unsuspend | `unsuspendacct` with `user` |
| Terminate | `removeacct` with `username` |
| Change package | `changepackage` with `user` and `pkg` |
| Change password | `passwd` with `user` and `password` |
| Log in to cPanel | `create_user_session` with `user` and `service=cpaneld` |
| Test connection | `version`, then `listaccts` to count accounts |
| Account lookup | `listaccts` with `searchtype=user` and `searchmethod=exact` |

The product's **Package** must be the exact name of a WHM package on the server.

## Create an API token in WHM

1. Sign in to WHM as `root`, or as a reseller that owns the accounts.
2. Go to **Development > Manage API Tokens** and click **Generate Token**.
3. Give it a name, for example `hostpanel`. Copy the token. WHM shows it only once.

A reseller token can only manage the reseller's own accounts.

## Add the server in Hostpanel

Go to **Servers**, click **Add New Server** and fill the **General** tab as for any server. On the
**Details** tab:

| Field | Value |
|---|---|
| Module | cPanel |
| Username | The WHM user that owns the token, for example `root` |
| API Token | The token from WHM |

The **Password** and **Access Hash** fields are not used for cPanel. The API token is stored
encrypted with `ENCRYPTION_KEY`, like the other server secrets, and is never written to a log.

Then click **Test Connection**. A working token shows the WHM version and the account count.
`SERVER_CONNECTION_NO_API_KEY` means the username or the API token is missing.

## Ports

Hostpanel calls WHM on port 2087 and shows account owners the cPanel address on port 2083. Both
are HTTPS. Change them for every cPanel server at once in the `Provisioning` settings:

| Setting | Default |
|---|---|
| `Provisioning__CPanelWhmPort` | `2087` |
| `Provisioning__CPanelUserPort` | `2083` |

## How retries behave

- Create: if WHM refuses and an account with that username already exists, Hostpanel reports the
  existing account with its domain. It is adopted only when the domain is the service's own.
- Suspend and unsuspend: a refusal is accepted when the account is already in the asked state.
- Terminate: a refusal is accepted only when WHM positively reports no such account.
- Change package: a refusal is accepted when the account is already on the asked package.
- If Hostpanel cannot read WHM's answer, it never assumes the account is gone or created.

WHM's own error text is never shown or logged, because it can name the account.

## Security notes

- WHM API 1 functions take their arguments in the query string. `createacct` and `passwd` send the
  account password there, over HTTPS. Hostpanel never logs these URLs, and the HTTP client for WHM
  has request logging turned off.
- Single sign-on links from `create_user_session` are single-use and expire. WHM does not bind them
  to the browser's address.
- WHM calls go through the outbound address guard, like the other panels. A server on a private
  address needs a [private network route](./servers#private-network-routes).
