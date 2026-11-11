---
title: Plugin registry
description: Switch on the plugin registry, choose who you trust, and browse and install plugins from the admin panel. Settings, trust levels, download limits and the errors you can meet.
keywords: "plugin registry, install plugin, registry trust, PluginRegistry, PLUGIN_REGISTRY_BASE_URL, verified publishers"
---

# Plugin registry

A plugin registry is a catalogue of signed plugin packages. When you point Hostpanel at one, the Plugins page of the admin panel gets a Browse tab where you can search the catalogue, open a plugin's details page, install the version you choose and keep a licence key for a paid plugin.

The registry is off by default and Hostpanel ships with no registry address. You choose the registry, and you choose how much of it you trust.

<Callout type="warning">
A plugin runs inside your server. Switching the registry on does not make every listed plugin safe. Read the trust levels below before you install anything.
</Callout>

## Switch it on

Set two values in `.env` and recreate the stack:

```bash
PLUGIN_REGISTRY_ENABLED=true
PLUGIN_REGISTRY_BASE_URL=https://registry.example.com/v1/
```

| Setting | Compose variable | Default | Meaning |
|---|---|---|---|
| `PluginRegistry__Enabled` | `PLUGIN_REGISTRY_ENABLED` | `false` | Turns the registry on. While it is off, every registry call answers `PLUGIN_REGISTRY_DISABLED` and the Browse tab is not shown. |
| `PluginRegistry__BaseUrl` | `PLUGIN_REGISTRY_BASE_URL` | empty | The registry address: an absolute `https` URL with a host, no user name or password, no query and no fragment. |
| `PluginRegistry__Timeout` | none | `00:00:30` | The time limit of one registry call, a package download included. From 1 second to 5 minutes. |
| `PluginRegistry__MaxDownloadBytes` | none | `52428800` (50 MiB) | The largest package Hostpanel accepts. From 1 to 104857600 (100 MiB). |

`Timeout` and `MaxDownloadBytes` have no `.env` variable. Set them as environment entries on the API service in a compose override, the same way as the order numbering settings in the [configuration reference](./configuration).

An address that is set must be valid even while the registry is off, and an enabled registry with no address stops the API at start. The start-up message names the setting that is wrong.

What the address means, and what the registry must answer at it, is described in [Plugin registry read contract](../developers/plugin-registry-contract).

## What Hostpanel sends

Hostpanel talks only to the address you configured:

- The Browse tab asks for the catalogue, with your search text, the category and the page.
- The details page asks for the plugin's page and its list of versions.
- An install asks for the release you chose, checks it against the list of versions and then downloads its package.
- The update check sends the ids of the plugins you installed or uploaded (not the built-in ones) and asks for their newest versions.
- An install first reads the plugin's page, which says whether the plugin is public or restricted. For a restricted plugin the installation also registers with the registry, as described in [Restricted plugins and the registration of this server](#restricted-plugins-and-the-registration-of-this-server).

For a paid or private plugin it also sends the licence key you saved for it, in an `X-Licence-Key` header, and only to the registry's own address: on the plugin page, the version list, the release lookup, the package download and the update check for that plugin. The listing never carries a key. It sends no cookie, no login token, no host name, no setting and no client or user data. If you do not want those plugin ids to leave your server, leave the registry off.

A request, a redirect and a package address must all have the registry's scheme, host and port, and a package that lives anywhere else is refused. The registry's name must also not resolve to the server itself: loopback, link-local (including the cloud metadata address), unspecified and multicast addresses are refused. Private network ranges are allowed, so a registry inside your own network works, but a registry on `localhost` does not.

## Restricted plugins and the registration of this server

Some plugins in a registry are not offered to the public. The registry gives their version list, their release details and their package only to an installation it has verified, and the site of the registry offers no download of them. Such a plugin is marked **restricted** by the registry (`distribution: HostOnly`). A public plugin works exactly as described above and does not need any of this.

### What the registration does

When the registry is on, Hostpanel gives itself an identity towards the registry. Nobody has to do anything:

1. **Key pair.** Hostpanel generates an Ed25519 key pair once. The private key is stored encrypted in the settings table with the same protection as the other secrets of the server, never shown and never sent. If the registry asks for a fresh start (see below), a new pair is made, because a registry refuses to register one key twice.
2. **Registration.** Hostpanel sends the registry two things: the public key and the public address of this installation, which is the `ClientBaseUrl` setting of the API (see the [configuration reference](./configuration)). The registry answers with an instance id and a challenge, a random text.
3. **Proof.** The registry fetches `/.well-known/hostpanel-registry/{id}` from that public address, and Hostpanel answers with exactly the challenge, as plain text that no cache may keep. When the text matches, the registry marks the installation verified. This proves that whoever registered controls the address; it does not tell the registry who you are.
4. **Signed requests.** From then on, every request for a restricted plugin is signed with the private key: the version list, the release details, the request for a one time download ticket and the final download of the package. The signature covers the method, the exact request target, a timestamp and a random value used once, so a request cannot be changed or replayed. The package itself is fetched against a ticket that works once, for about a minute, for this installation and this version.

The registration runs by itself: a background job runs it at start, and again whenever a restricted plugin is asked for. It is safe to repeat. After a failure it waits 30 seconds, then 1 minute, 2 minutes and so on up to one hour before trying again. The state of the registration is on the **Browse** tab, in the **Registration with the registry** card: not registered yet, waiting for confirmation, verified, revoked, or failed with the reason in your language. **Retry registration** runs it at once and ignores the wait; after a revocation it asks for a new identity.

### What is sent, and what is not

| Call | Sent |
|---|---|
| Registration | The public key and the public address of this installation. |
| Verification | Nothing but the instance id in the address. The registry reads the challenge from your server. |
| Version list, release details, ticket and package of a restricted plugin | The instance id, the time, a random value and the signature, in the headers `X-Hostpanel-Instance`, `X-Hostpanel-Timestamp`, `X-Hostpanel-Nonce` and `X-Hostpanel-Signature`. No licence key. |

Hostpanel never sends the private key, a password, a cookie, a login token, a setting, a client or user, or the host name of the server beyond the public address you configured. The registry learns the public address you configured and the time of each request for a restricted plugin; that is the whole identity. If you do not want that, leave the registry off, or do not install restricted plugins: the registration is made at start only while the registry is on.

### What you need to set up

- **A public https address.** `ClientBaseUrl` must be the public https address of the installation, not `localhost`, a private network address or a name without a dot. Otherwise the registration fails with `PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID` and the card names the setting.
- **The well-known path must reach the API.** The registry calls `https://your-address/.well-known/hostpanel-registry/{id}`. The edge proxy in front of Hostpanel has to send that path to the API, the way it sends `/api/`. The Caddyfile of the Docker Hub stack (`docker/caddy/Caddyfile`) does it; with another proxy add a route for the prefix `/.well-known/hostpanel-registry/` to the API service. Without it the registration fails with `PLUGIN_REGISTRY_INSTANCE_CHALLENGE_UNREACHABLE`.
- **A correct clock.** A signed request is accepted within a few minutes of the registry's time. If the clock of the server is off, the registry refuses the request and Hostpanel says to fix the server clock; turn on time synchronisation (NTP) on the host.

### Revocation

If the operator of the registry revokes your installation, signed requests are refused, the card shows **Revoked**, and the automatic job leaves it alone. Contact the operator of the registry. **Retry registration** then asks for a new identity.

## Registry trust

Whose plugins may be installed is the `plugins.registry_trust` setting. Change it in the Registry trust card at the top of the Browse tab. A change applies to the next install at once and is written to the activity log.

| Value | What it allows |
|---|---|
| `VerifiedOnly` (default) | Only plugins the registry marks as verified. An unknown stored value is read as this one. |
| `AnyRegistryPublisher` | Any publisher the registry lists, verified or not. The panel shows a warning when you pick it. |
| `OwnKeysOnly` | Only a package whose signature key is one of your own keys, listed in `PluginSigning__OperatorKeys`. The key that ships with Hostpanel does not count. |

The trust level is only the first check. Every registry install also has to pass these, whatever you chose:

1. **The checksum.** The package must hash to the SHA-256 the registry published for that release. Anything else is refused with `PLUGIN_REGISTRY_CHECKSUM_MISMATCH`.
2. **The identity.** The `plugin.json` inside the package must name the plugin id and the version you asked for. A package of another plugin or another version is refused with `PLUGIN_REGISTRY_PACKAGE_REFUSED`.
3. **The signature.** `plugin.sig` must be valid for a key Hostpanel trusts: the key that ships with it or one of your `PluginSigning__OperatorKeys`.

A registry is a catalogue, not a root of trust. A compromised registry can offer a package, but it cannot make Hostpanel install one that a trusted key did not sign.

### Unsigned packages are never installed from a registry

The `plugins.allow_unsigned` setting lets a production server accept a package you upload by hand without a valid signature. It never applies to a registry install. A package from the registry must carry a valid signature in every environment, development and testing included, whether `allow_unsigned` is on or off.

To trust a publisher whose key does not ship with Hostpanel, add that key to `PluginSigning__OperatorKeys` first (the compose variables are `PLUGIN_OPERATOR_KEY_ID` and `PLUGIN_OPERATOR_PUBLIC_KEY`, see the [configuration reference](./configuration)).

## Download limits

- A package is streamed to a temporary file in the plugin staging folder and never held in memory. The file is deleted when the install ends, whatever the result.
- A package over `MaxDownloadBytes` is refused as soon as its declared length or the bytes read pass the limit, with `PLUGIN_REGISTRY_PACKAGE_REFUSED`.
- A JSON reply from the registry (a listing or a release) is limited to 1 MiB.
- A redirect is followed for a GET request only, at most three hops, and only inside the registry's own origin.
- The whole call, download included, must finish within `PluginRegistry__Timeout`.

After these limits, the package goes through the same checks as an uploaded one: manifest rules, archive limits, SDK version and, unless you confirmed a downgrade (see below), no downgrade.

## Browse and install

When the registry is on, the Plugins page has two tabs, Installed and Browse.

1. Open **Plugins** and choose **Browse**.
2. Search by name, or pick a category (Payment, Provisioning, Registrar, App or Other). The list is paged.
3. Each card shows the name, the publisher, the newest version, the SDK version it was built for, its price (Free, Paid or Private) and a Verified badge when the registry marks it as verified.
4. Press **Install** to install the newest version, or open the card for the details page (below) to choose another version.
5. Restart when the banner offers it. A new plugin is loaded at the next start.

A plugin you already have shows as installed with its version. If the registry has a newer version, the button reads **Update to** that version. An update is staged like an uploaded upgrade: it is applied at the next restart. In the Installed tab, a plugin with a newer registry version shows an update badge.

An entry in the catalogue that has no usable version is left out of the list, and the rest of the page is shown. An entry that is not a plugin at all (a bad id, no name, an unknown price) still makes the whole page fail with `PLUGIN_REGISTRY_RESPONSE_INVALID`.

### The details page

A card opens the plugin's own page, which shows:

- The name, the publisher, the price, the Verified badge, the installed version, a staged version that waits for a restart, and the update that is available.
- Links the publisher gave: documentation, support, source repository, where to buy a licence, and the licence of the plugin.
- A **Versions** list. Each row shows the version, whether it is published, deprecated or yanked, the date, the host versions it runs on (and whether that includes this server), the release notes and the permissions it asks for. A yanked version cannot be installed, and neither can a version that does not run on this server.
- **Uninstall** for a plugin you installed or uploaded. It asks first, removes the plugin's files and finishes after a restart. The data the plugin stored is kept until you delete it. A built-in plugin cannot be uninstalled.

Each version row has one action: **Install** for a plugin you do not have, **Update** for a version newer than the installed one, **Downgrade** for an older one, and nothing for the one that is installed. The panel asks you to confirm every install.

A **downgrade is allowed, but only after an explicit confirmation** that warns an older version may lack fixes and may not read data a newer one wrote. Without the confirmation the request is refused with `PLUGIN_DOWNGRADE_REFUSED`. A script that calls `POST /api/admin/plugins/registry/{id}/install` has to send `"allowDowngrade": true` for the same effect. A downgrade is staged for the next restart like an update and is written to the activity log as a forced downgrade.

### Release notes and updates {#release-notes-and-updates}

Release notes tell you what a version changes before you install it. They are Markdown written by the publisher and shown as formatted text.

- **Browse tab.** A card with a version has a **What is new in** link that opens the release notes of the newest version inline. For a plugin you already have, when a newer version exists, a **What changed** link opens the details page, where the update is explained.
- **Installed tab.** Each plugin has a **Release notes of** link for the installed version. A plugin with an update also has **What is new in** for the new version and **What changes in**, which opens the details page.
- **Details page.** The rows of the **Versions** list open their release notes, and a plugin with an update shows **What changes from 1.0.0 to 1.2.0** above them.
- **Update.** **Update** does not start at once. A confirmation dialog, "Update this plugin?", shows **What changes from 1.0.0 to 1.2.0** with the release notes of every version after the installed one up to the one you chose, newest first, so an update that skips versions still shows all of them. A version in between without notes shows "This version has no release notes." instead of being left out. Press **Update** in the dialog to stage the version, or cancel to leave things as they are. The update is applied at the next restart, as before.

### Licence keys

A paid or private plugin needs a licence key from its publisher. The Licence key card on the details page says whether a key is saved, and lets you save a new one (it replaces the old) or remove it. A key is at most 256 printable characters, with no spaces. The key is write only: it is stored encrypted on your server and never shown again, no endpoint returns it, and it is left out of the logs and the activity log. Save the key before you install. Without one the registry answers that a licence is needed, and with a wrong one that it is not valid (`PLUGIN_REGISTRY_LICENCE_REQUIRED` and `PLUGIN_REGISTRY_LICENCE_INVALID` below).

### Endpoints

For scripts. All of these need an administrator and live under `/api/admin/plugins`:

| Method and path | What it does |
|---|---|
| `GET /registry` | The paged catalogue (the Browse tab). |
| `GET /registry/updates` | The newest registry version of each plugin you installed. |
| `GET /registry-instance` | Where this server stands with the registry: `state` (`NotRegistered`, `Pending`, `Verified`, `Revoked`, `Error`), the instance id, the public address, the code and sentence of the last failure, and when the next automatic attempt is due. Nothing secret. |
| `POST /registry-instance/retry` | Runs the registration at once, ignoring the wait, and answers with the state it left. Not available in a demo installation. |
| `GET /registry/{id}` | The details page of one plugin, with its versions. The licence key is never returned, only whether one is saved. |
| `PUT /registry/{id}/licence` | Saves the licence key for the plugin. Body: `{ "key": "YOUR-KEY" }`. |
| `DELETE /registry/{id}/licence` | Removes the saved key. |
| `POST /registry/{id}/install` | Installs or stages one version. Body: `{ "version": "1.2.0", "allowDowngrade": false }`. |
| `DELETE /{id}` | Uninstalls an installed or uploaded plugin. |

Installing needs an administrator and is not available in a demo installation. Every install, and every refusal that happens before the install, is written to the activity log with the plugin id and version, and the SHA-256 when the package was downloaded.

## Errors

| Code | Status | When |
|---|---|---|
| `PLUGIN_REGISTRY_DISABLED` | 409 | The registry is off or has no address. |
| `PLUGIN_REGISTRY_UNAVAILABLE` | 502 | The registry could not be reached, answered with an error status, or answered 404 for anything but a release lookup. |
| `PLUGIN_REGISTRY_TIMED_OUT` | 504 | The call took longer than `PluginRegistry__Timeout`. |
| `PLUGIN_REGISTRY_RESPONSE_INVALID` | 502 | The reply is not what a registry should send: bad JSON, a bad plugin id, version, price or checksum, or a release for something other than what was asked. One entry that is not a plugin (bad id, no name, unknown price) fails the whole page; an entry with no usable version is only skipped. |
| `PLUGIN_REGISTRY_PLUGIN_NOT_FOUND` | 404 | The registry does not list that version of that plugin. |
| `PLUGIN_REGISTRY_PACKAGE_REFUSED` | 502 | The package address or a redirect is outside the registry's origin, the package is over the size limit, or it is another plugin or version than asked for. |
| `PLUGIN_REGISTRY_CHECKSUM_MISMATCH` | 502 | The downloaded package does not match the published SHA-256. |
| `PLUGIN_REGISTRY_NOT_TRUSTED` | 403 | The registry trust setting does not allow this plugin: it is not verified under `VerifiedOnly`, or its signing key is not one of yours under `OwnKeysOnly`. |
| `PLUGIN_REGISTRY_LICENCE_REQUIRED` | 422 | The plugin is paid or private and the registry answered that a licence key is needed, because none is saved for it. Save one on the details page. |
| `PLUGIN_REGISTRY_LICENCE_INVALID` | 422 | The registry refused the saved licence key. Replace it with a valid one. |
| `PLUGIN_REGISTRY_HOST_INCOMPATIBLE` | 409 | The version does not run on this Hostpanel version, because it is outside the host range the publisher declared. Choose another version or upgrade Hostpanel. |
| `PLUGIN_REGISTRY_VERSION_YANKED` | 409 | The publisher withdrew this version, so it cannot be installed. |
| `PLUGIN_REGISTRY_VERSION_COMPROMISED` | 409 | The registry rejected this version, for example because it was found to be unsafe. It is never installed. |
| `PLUGIN_DOWNGRADE_REFUSED` | 409 | The version is older than the installed one and the downgrade was not confirmed (`allowDowngrade`). |
| `PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID` | 422 | `ClientBaseUrl` is not a public https address the registry can use. Set it, then retry the registration. |
| `PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED` | 409 | The registry has not verified this server yet, so a restricted plugin is not given out. The registration runs by itself; check the card on the Browse tab. |
| `PLUGIN_REGISTRY_INSTANCE_REVOKED` | 409 | The registry revoked this installation. Contact the operator of the registry, then retry the registration. |
| `PLUGIN_REGISTRY_INSTANCE_CHALLENGE_UNREACHABLE` | 502 | The registry could not read the confirmation text at `/.well-known/hostpanel-registry/{id}` on your public address. Route that path to the API. |
| `PLUGIN_REGISTRY_INSTANCE_SIGNATURE_REJECTED` | 502 | The registry did not accept the signature. Retry the registration; if it keeps happening the stored identity may be damaged. |
| `PLUGIN_REGISTRY_INSTANCE_CLOCK_SKEW` | 502 | The clock of this server is too far from the correct time. Fix the server clock and try again. |
| `PLUGIN_REGISTRY_INSTANCE_REPLAY_REJECTED` | 502 | The registry took the request for a repeat of an earlier one. Try again. |
| `PLUGIN_REGISTRY_DOWNLOAD_TICKET_REFUSED` | 502 | The registry did not accept the one time download ticket. Try the install again. |
| `PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED` | 429 | The registry limits how often this server may register or ask for restricted plugins. Wait, then try again. |

Hostpanel never shows you the registry's own error text, and the log keeps only the kind of failure, so an address or a reply cannot leak into either.

An install can also fail with any refusal of an ordinary plugin upload, for example `PLUGIN_SIGNATURE_KEY_UNKNOWN`, `PLUGIN_SIGNATURE_INVALID`, a manifest error or `PLUGIN_ARCHIVE_TOO_LARGE`. Those mean the package itself is not acceptable, and nothing was installed.

## Related pages

- [Plugin registry read contract](../developers/plugin-registry-contract), what a registry must serve.
- [Publishing a plugin](../developers/publishing-a-plugin), how a publisher gets a package into a registry.
- [Configuration reference](./configuration), every other setting.
