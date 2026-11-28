---
title: Plugin manager
description: Install, update, remove and sign payment plugins.
---

# Plugin manager

## Signed plugin packages

A plugin runs inside the server, so the server needs to know who made a package before it runs one. A plugin package
(the ZIP you upload with **Upload plugin** on the Plugins page) is signed with a detached Ed25519 signature, the file `plugin.sig` at the
root of the package.

### What the server does

| Plugin | Production | Development |
|---|---|---|
| Shipped inside the server image | loads, never checked | loads |
| Uploaded, signed by a trusted key | installs and loads | installs and loads |
| Uploaded, unsigned, changed after signing, or signed by an unknown key | refused | installs and loads |

The check runs when you upload a package and again every time the server starts, so a package that is changed on
disk afterwards is skipped at the next start. Only that plugin is skipped; the log names the plugin and the reason.
Plugins that ship inside the image are part of the server itself, so they carry no signature of their own.

Everything except the Development and Testing environments counts as production.

### Allowing unsigned plugins

Open **Plugins** in the admin panel, choose the **Settings** tab and switch on **Allow unsigned plugins**. It is off by default, and each change is
written to the activity log. With it on, a production server installs and loads packages that fail the signature
check, so only switch it on when you trust every package you upload. A change of the switch applies to the next
upload at once, and to plugins that are already installed after the next restart.

### Trusted keys

The server trusts the public keys listed in two places. The maintainers' keys ship with the server in the
`PluginSigning:MaintainerKeys` section (key id `official-innovayse-2026`, which signs the Innovayse plugins). You add your own under
`PluginSigning:OperatorKeys`, for example with environment variables:

```text
PluginSigning__OperatorKeys__0__KeyId=my-company-2026
PluginSigning__OperatorKeys__0__PublicKey=<base64 of the 32-byte public key>
```

A key id is 1 to 64 characters of `a-z`, `0-9`, `.`, `-` and `_`. Only public keys go here. The server refuses to
start when a key is not usable.

These lists decide for packages you upload. A package installed from a [plugin registry](../operators/plugin-registry) may also be signed with a key
the registry publishes for the plugin's publisher, so you do not add that key yourself.

### Signing a package

The format is described in the repository's `rules/project.md`. To sign, use the signing tool from a checkout of the
repository. Keep the private key outside every repository; the tool refuses a key file inside a git working tree.

```bash
# once: create a key pair; this prints the public key for PluginSigning__OperatorKeys
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- keygen /secure/place/plugin-signing.key

# every release: sign the folder that will be zipped, then zip it as it is
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- sign ./my-plugin /secure/place/plugin-signing.key my-company-2026
```

Sign after the last change: adding, removing or changing any file afterwards makes the signature invalid. A package
that contains a symbolic link or junction cannot be signed; ship real files.

Known limitation: the signature does not protect against downgrades. It shows that the files come from a trusted key
and are unchanged, not that they are the latest release, so an older signed version can be installed again. Install only
releases you have chosen, and remove a key from `PluginSigning` if everything it signed should stop being trusted.

## Refused plugins in the list

A plugin that is installed but breaks a manifest rule is skipped when the server starts. The log gets one warning that
names the plugin and the rule, and the plugin never runs.

The **Installed** tab of the Plugins page shows such a plugin as a **Refused** card. The entry gives the plugin's folder name
as its id, the rule it broke and a short detail saying what is wrong, and it is never shown as enabled or loaded. Only a
broken manifest rule is listed this way; a package that fails for another reason (for example a signature check) is only
a line in the log.

Every row of the list has one of three statuses:

| Status | Meaning |
|---|---|
| Loaded | The package is installed and not refused; the row says whether the plugin is loaded. If a newer version is waiting for the restart, the row also shows that staged version. |
| Refused | The package is installed but broke a manifest rule at start. The row shows the rule and the detail. |
| Removed | The package is gone but the plugin still keeps stored data. The row shows only the plugin id and is the place to purge that data (see below). |

The rules are the same ones that refuse an upload, and each has the same text as the upload error:

| Rule | What to change in the package |
|---|---|
| `ManifestVersionUnknown` | The manifest declares a manifest version this server does not know. |
| `SdkAssemblyOutdated` | The plugin was built against an older SDK. Rebuild it against the current one. |
| `PermissionUnknown` | A permission does not exist. Use a client permission name or `Any`. |
| `AdminPermissionDeclared` | A permission is set on an admin route, page or slot. The admin area takes none. |
| `RoutePathInvalid` | A route path is not valid. Use lowercase segments and parameters in curly brackets. |
| `RouteDuplicate` | The same route is declared twice for one method in one panel. |
| `LabelKeyMissing` | A text key is used but the English texts do not define it. |
| `SlotUnknown` | Content is put into a slot the server does not offer. |
| `SlotContextKeyNotOffered` | A slot is asked for context it does not offer. |
| `AssemblyUnreadable` | A file in the plugin could not be read. Upload it again. |
| `SlotWidgetIdInvalid` | A widget id is not valid. Use lowercase letters, digits, hyphens and underscores. |
| `SlotWidgetDuplicate` | The same widget id is declared twice for one slot. |
| `PluginIdInvalid` | The plugin id in the manifest is not valid. |
| `VersionInvalid` | The plugin version is not a strict version. Write it as `major.minor.patch`, for example `1.2.3`, with no leading zeros. A pre-release (`-beta.1`) and build metadata (`+7`) may follow. |

To fix a refused plugin, correct the package and upload it again (see the next section). To get rid of it, remove it
from the list; a refused plugin is removed like any other, by its folder name.

## Applying a plugin change without a shell {#applying-a-plugin-change}

Installing, updating or removing a plugin never needs a shell on the server, and you do not have to press anything. Right
after the change is staged, the host applies it itself: first in place, with no restart, and when that is not possible
it restarts itself. The plugin manager shows where each change stands:

| State | Meaning |
|---|---|
| Staged | The change is saved and waits to be applied. |
| Restarting | The host is draining and restarting to apply the change. |
| Loaded and running | The new version is loaded and answers. |
| Failed | The new version could not be loaded. The row shows the reason and the version that was restored. |

### Reloading one plugin in place

Every plugin is loaded in its own unloadable .NET load context. A change is reloaded in place when the plugin is already
loaded, its plugin type and its endpoints capability stay the same, and it is not a hosted plugin. The old version is
drained first (running calls finish within `Plugins__DrainSeconds` and no new call starts), then it is unloaded, the
staged files are swapped in, the new version is loaded and its migration hook runs. The rest of the panel keeps working.
Removing a plugin unloads it the same way. The host keeps no strong reference to plugin types outside the plugin's own
context, which is what lets .NET unload it.

### When the host restarts itself

A change that cannot be reloaded in place uses the restart path. This is the fallback, and it applies when:

- the plugin id was never loaded (a first install of a plugin that does not run yet),
- the plugin is a hosted plugin,
- the new version changes the plugin type or the endpoints capability,
- the drain timed out because a call did not finish within `Plugins__DrainSeconds`.

The host waits `Plugins__RestartDelaySeconds` (default 3, from 0 to 60) before it restarts, so several changes made one
after another are applied by one restart. Then it drains: `GET /health` answers `503` with the error `HOST_DRAINING`, so
an edge proxy stops sending traffic, and running requests and background jobs finish within `Plugins__DrainSeconds`.
After that the process exits with the dedicated exit code 75, and the container restart policy starts it again. On the
start the staged changes are applied.

### Restart now

**Restart now** stays in the plugin manager for staged changes that were not applied, for example after a failed apply.
It is available to administrators only and does the same as `POST /api/admin/plugins/restart`, which is written to the
activity log as "Restarted Plugin Host". While the host restarts, the panel shows a blocking **Restarting** window. It
stays until `GET /health` answers with a new `processStartedAt`, which proves the new process is the one answering, and
then shows **Loaded and running**.

### When the new version fails to load

If the new version of a plugin cannot be loaded, whether at the start after a restart or during an in place reload,
the host keeps running. It takes the new version out and restores the previous version of that plugin. A new install
that fails to load is shown as refused, with its load error. The change is recorded as **Failed** with the reason and the
restored version, and the host never retries it, so there is no restart loop. To try again, install a corrected package.

### Activity log

Every change is written to the activity log with who made it, the old and the new version and the result:

- the request itself writes "Plugin Change Applied In Place", "Plugin Change Applying By Restart" or "Plugin Change Failed And Restored",
- after a restart the host writes "Plugin Change Applied By Restart" or "Plugin Change Failed By Restart" with the reason.

### Drain time, delay and stop grace period

`Plugins__DrainSeconds` (default 30, from 1 to 300) is the drain time of a reload and of a restart, and it is also the
shutdown timeout of the host. Docker kills a container that does not stop within its stop grace period, so the API
service sets `stop_grace_period` longer than the drain time: 45 seconds by default. In the compose files the options are
`PLUGIN_DRAIN_SECONDS`, `PLUGIN_RESTART_DELAY_SECONDS` and `API_STOP_GRACE_PERIOD` (see the
[configuration reference](../operators/configuration.md)). If you raise the drain time, raise the grace period above it.

### The restart policy is required

The automatic restart exits the host on purpose, with a clean exit, so the API container must have
`restart: unless-stopped` or `restart: always`. `on-failure` does not start a container again after a clean exit, and
without a policy the panel stays down after a change that needs a restart. The compose files that ship with Hostpanel
have it. On a self hosted installation check it with

```bash
curl -fsSL https://raw.githubusercontent.com/innovayse/hostpanel/main/install.sh | bash -s -- --check
```

The check **fails** (exit code 1) when the API service of the installation's compose file has no restart policy or has
`on-failure`, and says how to fix it. See [Upgrade](../operators/upgrade.md#check-the-installation).

## Uploading a newer version

Uploading a package for a plugin that is already installed is an upgrade. It goes through the same checks as any
upload (manifest, SDK reference and signature). If the plugin can be reloaded in place (see above), the new version is
applied at once. Otherwise it is **staged** instead of replacing the installed plugin, because the installed one is
loaded and running. The folder of a loaded plugin is never overwritten in place, so this
holds for the same version too. The upload answers with the staged version and says whether the host applied it in place or is restarting to apply it, and the list
shows it next to the plugin. Uploading again before the restart replaces the staged package. Staging is audited:
"Staged Plugin Upgrade", "Staged Plugin Replacement" (the same version again) or "Forced Plugin Downgrade".

**Versions are strict.** A version is `major.minor.patch` with no leading zeros, plus an optional pre-release and
build metadata. `1.0` or `01.0.0` is refused at install with `PLUGIN_MANIFEST_VERSION_INVALID` (400), and a plugin whose
manifest already has such a version is refused at startup and shown as refused. Build metadata does not count: `1.0.0`
and `1.0.0+7` are the same version.

- **The swap happens on restart.** When the server starts it first records the upgrade, then moves the staged package in
  place of the installed one. If the swap fails (for example a file is locked), the installed version stays, the record
  is put back as it was, the log gets an error and the staged package is kept. Work folders an earlier run left behind
  are swept at startup, and a version that a crash left set aside is moved back first.
- **The migration hook runs once it succeeds.** After a swap, a plugin that has a migration hook is migrated when the
  server starts, and the activity log gets a "Migrated Plugin Data" line. The record of the upgrade is deleted only after
  that, and a second upgrade on top of a still pending one keeps the first "from" version. A plugin that has no migration
  hook just gets the activity line. If the package is removed in the meantime, the migration is dropped.
- **A plugin whose migration is pending is switched off.** From the moment the upgrade is recorded until the hook
  succeeds, the plugin is not called and not started: its routes answer as if it were disabled, payments and fulfilment
  do not use it. It would otherwise run new code on data in the old shape. The same holds when the hook throws or runs too
  long.
- **Retry.** The hook is tried again at every start, and also as soon as the operator enables the plugin or saves its
  settings, which is when a plugin that was off or missing a required setting becomes able to run it. The hook must be
  idempotent, because a failed attempt can be repeated. Look in the log for the plugin id when it stays switched off.
- **The hook has a time limit.** `Plugins__MigrationTimeoutSeconds` (default 120, between 1 and 3600) is the longest one
  hook may run. After it the host cancels the hook, stops waiting, logs the plugin and keeps it switched off until a later
  attempt succeeds, so one hanging hook cannot block the start of the server or the others. A hook that ignores its
  cancellation token may keep running in the background.
- **A downgrade is refused.** A version older than the one the restart will install (the staged upload when there is one,
  otherwise the installed version) answers `PLUGIN_DOWNGRADE_REFUSED` (409) and nothing is staged. So does an upload
  when the installed version cannot be read as a version, because it cannot be shown to be newer. Both attempts are
  audited ("Refused Plugin Downgrade"). Upload it again with the force option (`force=true` on
  `POST /api/admin/plugins/install`) to stage it anyway.
- **Settings and stored data are kept** through an upgrade.
- **Saved card customers are assumed to belong to the plugin id `stripe`.** The upgrade that introduced per-gateway customers copies earlier saved card customers to the new per-gateway table as customers of the plugin with the id `stripe`, because the only gateway that ever wrote them was that one. If your card gateway plugin uses another id, install the plugin as `stripe`, or rewrite `GatewayModuleId` in `GatewayCustomers` to your plugin id before clients pay with a saved card; otherwise those clients are asked for their card again.
- **The same version** of a plugin that is not loaded (for example a refused one) replaces the installed files at once, as
  before, and still needs a restart to apply.

Removing a plugin also discards an upgrade staged for it. A plugin that ships with the build is never replaced or removed,
even when the server refused it: its id stays taken, so an upload with the same name never loads in its place.

## Removing a plugin and purging its data

Removing a plugin deletes its package files (and any upgrade staged for it) and switches the plugin off. It does **not**
delete what the plugin kept: its settings and its stored data stay, so installing it again later finds them. The plugin
is unloaded at once when it can be (see [Applying a plugin change without a shell](#applying-a-plugin-change)), and
otherwise stays loaded until the next restart, but is switched off at once and answers nothing. A plugin shipped inside the server
image cannot be removed. If the files are already gone but the plugin was once switched on here, removing it again just
switches it off. The removal is written to the activity log.

A plugin that was removed but still keeps data is listed as **Removed** under **Removed plugins** on the **Settings** tab of the Plugins page. To delete that data, use the purge on
that row, which calls `DELETE /api/admin/plugins/{id}/data`:

- It is refused with `PLUGIN_STILL_INSTALLED` (409) while a folder for the plugin exists on disk, in the server image or
  in the upload folder, even when its `plugin.json` is damaged. Remove the plugin first, then purge.
- It answers `PLUGIN_NOT_FOUND` (404) when the plugin keeps no data, which is also what a second purge gets. Slot widget
  bindings count as kept data: a removed plugin with only bindings is listed as **Removed** and can be purged.
- It switches the plugin off, queues the deletion and writes it to the activity log. The deletion runs in the background and
  is retried until it succeeds, so it may finish a moment after the answer. No restart is needed.
- It deletes the plugin's stored data and also the plugin's slot widget product bindings (see the next section) that were
  written before the purge was requested. Data and bindings written after that, for example by a reinstalled plugin, are
  kept.

<div class="warning custom-block">

Purging cannot be undone. The plugin's stored data is deleted for good, so take a backup first (see
[Backup](../operators/backup.md)).

</div>

## Binding a slot widget to products after install

A plugin can put a widget into a page slot and, in its manifest, limit it to some products with `productIds`. Product ids
differ between installations, so a plugin that targets your own products cannot know them when it is built. After
install you can bind each widget to your products on the plugin's own page: open the plugin from the Installed tab, then its **Settings** tab, which lists its slot widgets.

- The setting made there overrides the manifest. A widget with products chosen there shows only for services of those
  products, and the `productIds` in the manifest are ignored for that widget.
- A widget that was never bound keeps the manifest binding. If the manifest
  lists none either, it shows for every service.
- A slot that has no service (for example a client page) never shows a widget that has a binding.
- A product that was deleted is ignored when the widget is shown. If every product of a binding is gone, the widget stays bound and
  shows nowhere. It does not fall back to the manifest, because the binding decides what the widget may reach. Clear the binding
  with an empty list to bring the manifest binding back.
- Saving drops product ids that do not exist. If product ids are sent and none of them exists, the save is refused with 400 `PLUGIN_SLOT_PRODUCTS_UNKNOWN` and the binding is left as it was. Only an explicit empty list clears it.
- Two saves of one widget at the same time run one after another, and the last one replaces the whole set.

The bindings are kept in their own table, `PluginSlotProductBindings`, and are written only by the operator save. They
are not a setting and cannot be changed from the settings pages or an integration form. They are deleted when the
data of the plugin is purged, except a binding saved after the purge was requested, which is kept.

The admin API is `GET /api/admin/plugins/{pluginId}/slot-widgets`, which lists the widgets of a loaded plugin (404 `PLUGIN_NOT_FOUND` for any other) with
`slotId`, `widgetId`, `title`, `manifestProductIds`, `productIds` and `isBound`, and
`PUT /api/admin/plugins/{pluginId}/slot-widgets/{slotId}/{widgetId}` with the body `{ "productIds": [3, 7] }`, which
answers the same shape with the products that were stored. An empty list clears the binding. When two saves of the same widget keep colliding the answer is 409. A widget the plugin does not declare in that slot answers 404 `PLUGIN_SLOT_WIDGET_NOT_FOUND`.

## Panel plugins and their servers

A plugin that serves hosting panels (it declares `provisioningPanel` and lists `serverModules`) shows its servers on its own page. Open the plugin from the Installed tab and choose the **Settings** tab: the **Servers** section lists every server stored on a module the plugin declares. Each row has the server name (a link to the Servers page narrowed to that name), its host name, its last known connection state (Online, Offline or Not tested) and when it was last tested, or that it never was. With no matching server the section says so and links to the Servers page, where one can be added. A plugin that declares no server module shows no such section. The list comes from `GET /api/admin/plugins/{pluginId}/servers`, which answers `PLUGIN_NOT_FOUND` (404) for an id that is not loaded.

A panel plugin keeps its connection per server, not as one connection. The status card beside its settings on the Integrations page therefore reflects its servers and shows how many are online: **Online** when at least one tested server is online and none is offline, **Partially online** when some are online and some are offline, **Offline** when every tested server is down, and **Not tested** when none has been tested yet. The state of each server is the one its last connection test left, see [Test a connection](../servers/servers.md#test-a-connection).

## Plugin errors

The API returns these error codes as the `type` field of the error response body.

| Code | Status | When |
|------|--------|------|
| `PLUGIN_MANIFEST_INVALID` | 400 | The `plugin.json` is missing or cannot be parsed. |
| `PLUGIN_MANIFEST_VERSION_UNKNOWN` | 400 | The manifest declares a manifest version this server does not know. |
| `PLUGIN_MANIFEST_VERSION_INVALID` | 400 | The plugin version string is not a valid strict version (`major.minor.patch`). |
| `PLUGIN_MANIFEST_ID_INVALID` | 400 | The plugin id in the manifest is not valid. |
| `PLUGIN_ID_INVALID` | 400 | The plugin id passed to a remove or purge call is not valid. |
| `PLUGIN_SDK_VERSION_UNSUPPORTED` | 400 | The manifest `sdkVersion` field is not accepted. The accepted value is `1.0`. |
| `PLUGIN_SDK_ASSEMBLY_OUTDATED` | 400 | The plugin was built against an older SDK assembly major version. Rebuild against the current SDK. |
| `PLUGIN_ASSEMBLY_UNREADABLE` | 400 | The plugin assembly or another required file cannot be read. Upload the package again. |
| `PLUGIN_HOST_ASSEMBLY_REFUSED` | 400 | The package includes an assembly the host already provides. Remove it from the ZIP. |
| `PLUGIN_ASSEMBLY_CONFLICT` | 400 | The package includes an assembly whose file name another installed or staged plugin already ships. Rename it or remove the other plugin. |
| `PLUGIN_ARCHIVE_TOO_LARGE` | 413 | The uploaded archive exceeds the size limit. |
| `PLUGIN_ARCHIVE_UNSAFE` | 400 | The archive contains unsafe entries (for example path traversal). |
| `PLUGIN_PERMISSION_UNKNOWN` | 400 | A permission declared in the manifest does not exist. |
| `PLUGIN_ADMIN_PERMISSION_DECLARED` | 400 | A permission is set on an admin route, page or slot. The admin area takes none. |
| `PLUGIN_ROUTE_PATH_INVALID` | 400 | A route path in the manifest is not valid. |
| `PLUGIN_ROUTE_DUPLICATE` | 400 | The same route is declared twice for one method in one panel. |
| `PLUGIN_LABEL_KEY_MISSING` | 400 | A text key is used in the manifest but the English locale file does not define it. |
| `PLUGIN_SLOT_UNKNOWN` | 400 | Content is put into a slot the server does not offer. |
| `PLUGIN_SLOT_CONTEXT_KEY_NOT_OFFERED` | 400 | A slot is asked for context it does not offer. |
| `PLUGIN_SLOT_WIDGET_ID_INVALID` | 400 | A widget id is not valid. Use lowercase letters, digits, hyphens and underscores. |
| `PLUGIN_SLOT_WIDGET_DUPLICATE` | 400 | The same widget id is declared twice for one slot. |
| `PLUGIN_VIEW_MISSING` | 400 | A view file declared in the manifest is not present in the package. |
| `PLUGIN_VIEW_INVALID` | 400 | A view file is present but cannot be parsed or is structurally invalid. |
| `PLUGIN_VIEW_SCHEMA_UNSUPPORTED` | 400 | The view schema version is not supported. |
| `PLUGIN_VIEW_BLOCK_KIND_UNKNOWN` | 400 | A view block uses a kind the server does not know. |
| `PLUGIN_VIEW_ROUTE_UNDECLARED` | 400 | A view references a route not declared in the manifest. |
| `PLUGIN_MENU_ICON_INVALID` | 400 | The menu icon name is not valid. |
| `PLUGIN_PAGE_ID_INVALID` | 400 | A page id in the manifest is not valid. |
| `PLUGIN_UNSIGNED` | 400 | The package has no signature and the server requires signed packages. |
| `PLUGIN_SIGNATURE_MALFORMED` | 400 | The signature file exists but cannot be read or parsed. |
| `PLUGIN_SIGNATURE_KEY_UNKNOWN` | 400 | The package is signed with a key id the server does not recognise. Add the public key to `PluginSigning:OperatorKeys`. |
| `PLUGIN_SIGNATURE_INVALID` | 400 | The package signature does not match the files. |
| `PLUGIN_NOT_TRUSTED` | 400 | The archive's SHA-256 is not listed in `Plugins:TrustedPackageSha256`. |
| `PLUGIN_BUILT_IN` | 409 | The plugin id matches a plugin that ships with the server. Built-in plugins cannot be replaced. |
| `PLUGIN_DOWNGRADE_REFUSED` | 409 | The uploaded version is older than the installed (or staged) version. Pass `force=true` to override. |
| `PLUGIN_STILL_INSTALLED` | 409 | Purge was called while the plugin folder still exists on disk. Remove the plugin first. |
| `PLUGIN_NOT_FOUND` | 404 | The plugin id is not known or keeps no stored data. |
| `PLUGIN_SLOT_PRODUCTS_UNKNOWN` | 400 | All product ids in a slot widget binding request are unknown. |
| `PLUGIN_SLOT_WIDGET_NOT_FOUND` | 404 | The slot widget is not declared by the plugin. |
