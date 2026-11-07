---
title: Plugin registry read contract
description: The read endpoints a plugin registry must serve for Hostpanel to browse and install from it. Paths, request and response shapes, the licence key header, same origin downloads and the SHA-256 rule.
keywords: "plugin registry, registry API, read contract, plugin download, sha256, PLUGIN_REGISTRY_BASE_URL"
---

# Plugin registry read contract

This page is for people who run or build a plugin registry. It describes what Hostpanel asks a registry for and what it needs in the answer. It does not describe how a publisher uploads a package, which is up to the registry; the upload used by the release workflow template is in [Publishing a plugin](./publishing-a-plugin). The operator side, with the settings and trust levels, is in [Plugin registry](../operators/plugin-registry).

The registry client is `PluginRegistryClient` in `backend/src/Innovayse.Hostpanel.Infrastructure/Plugins/Registry/`, and the wire types are the `*Wire` records beside it. Where this page and the code differ, the code is right.

## Rules that apply to every call

- **Address.** All paths below are relative to the operator's `PluginRegistry__BaseUrl`. Hostpanel adds a trailing slash if there is none, so `https://registry.example.com/v1` and `https://registry.example.com/v1/` are the same.
- **Anonymous reads.** Hostpanel sends no `Authorization` header, no cookie and no account credential. Every endpoint on this page must answer without one for a free plugin. A registry that needs a login for reads cannot be used.
- **Licence key.** The one credential Hostpanel may send is `X-Licence-Key`, only when the operator entered a key for a paid or private plugin, and only on the release lookup, the plugin page, the version list and the package download (not on the listing or the update check). It goes to the registry's own origin only, on every redirect hop inside it, and is never logged. Answer `401` when a key is needed and none was sent, and `403` when the key is not valid; Hostpanel shows them as `PLUGIN_REGISTRY_LICENCE_REQUIRED` and `PLUGIN_REGISTRY_LICENCE_INVALID`.
- **HTTPS only.** The address is an absolute `https` URL. The certificate must be valid for the name.
- **Same origin.** Every request, every redirect hop and every package address must have the registry's scheme, host and port. A response that points elsewhere is refused, and nothing is fetched from it.
- **JSON.** Names are camelCase and read case-insensitively. Members Hostpanel does not know are ignored, so a registry may add members. A JSON reply is limited to 1 MiB.
- **Headers.** JSON calls send `Accept: application/json`. The package download sends `Accept: application/zip, application/octet-stream`.
- **Redirects.** A GET may be redirected (301, 302, 303, 307 or 308) at most three times and only inside the same origin. The `POST` of the update check is never redirected: answer it directly.
- **Status codes.** Any success status is accepted. A 404 on a release lookup, a plugin page or a version list means "that is not listed". On those calls and on the package download, 401 and 403 are the licence answers above. A 404 on the listing or the update check, and any other error status, makes the call fail as unavailable.
- **Time.** One call, a download included, must finish within the operator's `PluginRegistry__Timeout` (30 seconds by default).

## How BaseUrl relates to the upload path

Hostpanel builds every read address by appending the paths below to `BaseUrl`. The upload shown in [Publishing a plugin](./publishing-a-plugin#upload-with-an-api-key) is `POST /v1/plugins/{id}/versions`, and the release workflow builds it from its own `PLUGIN_REGISTRY_URL` variable, which has no version segment (`https://registry.example.com`). The two side by side:

| Who | Setting | Value | Address it uses |
|---|---|---|---|
| Publisher's workflow | `PLUGIN_REGISTRY_URL` | `https://registry.example.com` | `POST https://registry.example.com/v1/plugins/{id}/versions` |
| Operator's Hostpanel | `PLUGIN_REGISTRY_BASE_URL` | `https://registry.example.com/v1/` | `GET https://registry.example.com/v1/plugins/{id}/versions/{version}` |

So the operator's `BaseUrl` is the publisher's registry URL plus the `/v1/` segment. Reads and the upload then share the same collection, `/v1/plugins/{id}/versions`: `POST` there uploads, and a GET of `/v1/plugins/{id}/versions/{version}` reads one release. A registry is free to serve the reads at another prefix, as long as the operator's `BaseUrl` ends where the `plugins` path starts.

## The endpoints

### List and search

```
GET {BaseUrl}plugins?q={text}&category={category}&page={page}&pageSize={pageSize}
```

| Parameter | Sent | Meaning |
|---|---|---|
| `q` | only when the operator typed a search | URL escaped text, at most 100 characters. Match it against the name, description and author as you see fit. |
| `category` | only when a category is chosen | One of `Payment`, `Provisioning`, `Registrar`, `App`, `Other`, written like that. Read it case-insensitively. |
| `page` | always | Starts at 1. |
| `pageSize` | always | From 1 to 100 (the admin panel asks for 20). Return at most this many items. |

Reply:

```json
{
  "items": [
    {
      "pluginId": "sample-gateway",
      "name": "Sample gateway",
      "description": "Takes card payments through a sample processor.",
      "publisherName": "Example Publisher",
      "iconUrl": "icons/sample-gateway.svg",
      "category": "Payment",
      "latestVersion": "1.2.0",
      "verified": true,
      "pricing": "Free"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

`total` is the number of plugins that match the filter across all pages. A negative value is read as 0. `page` and `pageSize` are not needed: Hostpanel uses the ones it asked for. Members Hostpanel does not use (rating, downloads and so on) may be present.

### Latest versions of installed plugins

```
POST {BaseUrl}plugins/latest
Content-Type: application/json

{ "ids": ["sample-gateway", "another-plugin"] }
```

The ids are those of the plugins the operator installed or uploaded. Answer with a bare JSON array, one item for each id you know and nothing for an id you do not know. Each item describes the newest published version, in the same shape as a release (see the next section):

```json
[
  {
    "pluginId": "sample-gateway",
    "version": "1.2.0",
    "downloadUrl": "/v1/plugins/sample-gateway/versions/1.2.0/download",
    "sha256": "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
    "verified": true,
    "sdkVersion": "3.1.0",
    "requiresLicence": false
  }
]
```

The reply is not paged: it must fit in 1 MiB. An item for an id that was not asked about is ignored, and an item whose `version` is not a strict `major.minor.patch` is left out. `pluginId` must be a well formed plugin id or the whole reply is invalid. This is how the Installed tab learns that an update exists, from `version` alone. The item has no name, so Hostpanel shows the id; `requiresLicence` set to `true` reads as a paid plugin.

### One release

```
GET {BaseUrl}plugins/{id}/versions/{version}
```

`id` and `version` are URL escaped path segments. Reply:

```json
{
  "pluginId": "sample-gateway",
  "version": "1.2.0",
  "downloadUrl": "https://registry.example.com/v1/files/sample-gateway-1.2.0.zip",
  "sha256": "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
  "verified": true
}
```

| Member | Rule |
|---|---|
| `pluginId`, `version` | Optional. When present they must equal what was asked, character for character, or the install is refused. When absent, Hostpanel takes the requested values. |
| `downloadUrl` | Required. Absolute, or relative to `BaseUrl` (a path starting with `/` is relative to the host root). It must resolve to the registry's own scheme, host and port. |
| `sha256` | Required. The SHA-256 of the ZIP file, 64 hexadecimal characters, either case. |
| `verified` | Optional, `false` when absent. This is the value the `VerifiedOnly` trust level reads. |

Answer 404 when the registry does not have that version. Other members (key id, size, status, changelog and so on) are ignored here.

### One plugin page

```
GET {BaseUrl}plugins/{id}
```

Reply: the listing entry as `summary`, plus links and the newest version.

```json
{
  "summary": { "pluginId": "sample-gateway", "name": "Sample gateway", "latestVersion": "1.2.0", "pricing": "Free" },
  "licence": "MIT",
  "repository": "https://example.org/sample/gateway",
  "buyUrl": null,
  "contactUrl": "https://example.org/contact",
  "documentationUrl": "https://example.org/docs",
  "supportUrl": "https://example.org/support",
  "latest": { "version": "1.2.0", "status": "Published", "publishedAt": "2026-01-02T03:04:05Z" }
}
```

`summary` follows the rules in "Listing entries" below, except that a missing `latestVersion` is allowed. The links are kept only when they are absolute `http` or `https` addresses (they may point to any host, the admin panel opens them in a new tab); anything else is dropped. `latest` is a version as described next, or `null`. Answer 404 when the plugin is not listed: Hostpanel shows it as not found.

### Versions of a plugin

```
GET {BaseUrl}plugins/{id}/versions
```

Reply: a JSON array of versions, newest first.

```json
[
  {
    "version": "1.2.0",
    "sdkVersion": "3.1.0",
    "sizeBytes": 20480,
    "publishedAt": "2026-01-02T03:04:05Z",
    "status": "Published",
    "statusReason": null,
    "replacementVersion": null,
    "minHostVersion": "1.0.0",
    "maxHostVersion": null,
    "changelog": "Fixes a rounding error.",
    "permissions": ["FileSystem"],
    "icon": { "url": "/media/icon.png", "contentType": "image/png", "sizeBytes": 900, "width": 64, "height": 64 },
    "screenshots": [{ "url": "/media/shot1.png", "contentType": "image/png", "sizeBytes": 52000, "width": 800, "height": 600 }]
  }
]
```

| Member | Rule |
|---|---|
| `version` | Required, a strict `major.minor.patch`. A row without a usable one is left out. |
| `status` | Required: `InReview`, `Published`, `Rejected`, `Deprecated` or `Yanked`, read case-insensitively. A row with another value is left out. |
| `permissions` | Optional. `Process`, `RawSockets`, `ReflectionEmit`, `NativeInterop` or `FileSystem`. Unknown names are ignored. |
| `icon`, `screenshots` | Optional. `url` is absolute or relative to `BaseUrl`; an image that is not on the registry's own origin is dropped. At most 20 screenshots are kept. |
| `changelog` | Optional, plain text, line breaks kept, at most 20000 characters. |
| other text members | Cleaned of control characters and capped. |

Answer 404 when the plugin is not listed.

## Listing entries

Hostpanel checks every entry, and one entry that is not a plugin (bad id, no name, unknown pricing) fails the whole reply as `PLUGIN_REGISTRY_RESPONSE_INVALID`. An entry with no usable `latestVersion` is only skipped.

| Member | Rule |
|---|---|
| `pluginId` | Required (`id` is read too). A plugin id: lower-case letters, digits and single hyphens, at most 64 characters. |
| `name` | Required, not empty once control characters and surrounding space are removed. Kept to 200 characters. |
| `latestVersion` | A strict `major.minor.patch` version, or `null` for a plugin with nothing published. An entry with a missing, `null` or malformed value is left out of the page; the other entries are kept. |
| `pricing` | Required. `Free`, `Paid` or `Private`, read case-insensitively. |
| `category` | Optional. An unknown or missing value is shown as `Other`. |
| `description` | Optional, kept to 2000 characters. |
| `publisherName` | Optional (`author` is read too), kept to 200 characters. |
| `sdkVersion` | Optional, kept to 32 characters, shown as information only. The listing need not carry it. |
| `iconUrl` | Optional. Absolute or relative to `BaseUrl`. It is kept only when it is on the registry's own origin; any other icon address is dropped. |
| `verified` | Optional, `false` when absent. Shown as a badge. The trust decision at install time reads `verified` from the release, so serve it in both places. |

The registry's text is shown in the admin panel as plain text. Markup in it is not interpreted.

## The package

`downloadUrl` is fetched with a GET. The answer must be the signed ZIP described in [Publishing a plugin](./publishing-a-plugin#zip-content-rules), within the operator's `MaxDownloadBytes` (50 MiB by default). A `Content-Length` over the limit is refused before a byte is read, and a body without one is cut off when it passes the limit. The file name does not matter.

Hostpanel then checks, in this order, before the package is stored:

1. The trust level against `verified` of the release, and for `OwnKeysOnly` against the key id in `plugin.sig`.
2. The SHA-256 of the downloaded file against `sha256` of the release, case-insensitively.
3. The `id` and `version` in the package's `plugin.json` against the ones asked for.
4. The ordinary package rules and the signature against the keys the host trusts. A package from a registry must be signed; the unsigned allowance for hand uploads never applies.

Publish the `sha256` of exactly the file you serve. Replacing the file without updating the release makes every install fail with `PLUGIN_REGISTRY_CHECKSUM_MISMATCH`, and a version is best treated as immutable once listed.

## A quick check

With a registry up, these requests must all work without credentials for a free plugin:

```bash
curl --fail https://registry.example.com/v1/plugins?page=1\&pageSize=20
curl --fail --header "Content-Type: application/json" --data '{"ids":["sample-gateway"]}' https://registry.example.com/v1/plugins/latest
curl --fail https://registry.example.com/v1/plugins/sample-gateway/versions/1.2.0
curl --fail https://registry.example.com/v1/plugins/sample-gateway
curl --fail https://registry.example.com/v1/plugins/sample-gateway/versions
```

For a paid or private plugin the release, the page, the version list and the download must answer `401` without the header and `403` with a wrong key:

```bash
curl --header "X-Licence-Key: YOUR-KEY" --fail https://registry.example.com/v1/plugins/paid-plugin/versions/1.0.0
```

Then fetch the `downloadUrl` of the last answer and compare `sha256sum` of the file with `sha256`.
