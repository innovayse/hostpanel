---
title: Write a plugin
description: "Write a Hostpanel plugin as a .NET class library on the SDK: payment and provisioning plugins, the plugin.json manifest, packaging and tests."
keywords: "write plugin, hostpanel sdk, payment plugin, provisioning plugin, plugin manifest"
---

# Write a plugin

A plugin is a .NET class library that references the Hostpanel SDK and adds a payment gateway, a provisioning module, a domain registrar or another capability. The plugin never references the Hostpanel host directly — only the SDK.

## Before you start

The SDK project lives at `backend/src/Hostpanel.Sdk` in the repository. Reference it from your class library, or reference the built `Hostpanel.Sdk.dll` directly. This guide is written for Hostpanel.Sdk 3.1.0, so build against that version or a later 3.x one: a package built against an older SDK major is refused with `PLUGIN_SDK_ASSEMBLY_OUTDATED`. The SDK repository also publishes a second package, `Hostpanel.Sdk.Packaging`, versioned in step with the SDK (3.1.0). It holds the checks the host runs on a package, so you can run them on your own build before you upload it: the signature format and verification, the plugin id and version rules, reserved assembly names, the archive limits and path checks, the icon check and the structural manifest rules. Reference it from your build or release tooling only, never from the plugin assembly itself. The bundled plugins in `backend/src/Innovayse.Providers.*` are the reference implementations; read their code before writing your own.

### Name the project

Name a plugin project `<Company>.Hostpanel.Plugins.<Name>`, the .NET plugin convention `<Company>.<Product>.<Type>.<Name>` (as in `Serilog.Sinks.Console`). The project, its folder, its assembly and its root namespace all carry that name, and its test project adds `.Tests`. Innovayse's own plugins are `Innovayse.Hostpanel.Plugins.<Name>`, with tests in `Innovayse.Hostpanel.Plugins.<Name>.Tests`, each in its own repository named `hostpanel-<name>`. The bundled `Innovayse.Providers.*` projects predate the convention and are renamed as they move to their own repositories.

The plugin `id` in `plugin.json` is not part of the name and never changes when a project is renamed: the host keys the plugin's stored settings and data by it.

## Plugin types

The `PluginType` enum defines what a plugin can be:

| Type | Value | Covered in this guide |
|------|-------|-----------------------|
| `Provisioning` | 0 | Yes — see below |
| `Payment` | 1 | Yes — see below |
| `Registrar` | 2 | Listed only |
| `Identity` | 3 | Listed only |
| `Fulfilment` | 4 | Listed only |
| `Hosted` | 5 | Listed only |
| `App` | 6 | Listed only |

## A payment plugin

Inherit `PaymentGatewayBase` and implement `IPaymentPlugin` from `Hostpanel.Sdk.Plugins.Payment`.

### Required members

| Member | Description |
|--------|-------------|
| `string CurrencyCode` | ISO 4217 numeric currency code this gateway settles in (e.g. `"840"` for USD, `"051"` for AMD) |
| `Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)` | Start a payment session and return a redirect URL |
| `Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)` | Poll the gateway for the current state |
| `Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)` | Issue a refund; return the gateway's refund reference |

### Types

**`PaymentInstruction`** properties: `OrderNumber`, `AmountMinor`, `ReturnUrl`, `Description`, `Language`.

**`PaymentSession`** properties: `GatewayOrderId`, `RedirectUrl`.

**`GatewayPaymentStatus`** properties: `State` (`GatewayPaymentState`), `TransactionId`, `RawStatus`, `AmountMinor`, `Currency` (ISO 4217 numeric).

`AcceptsCurrency(string currencyNumeric)` has a default implementation that compares against `CurrencyCode`; override it only when the gateway accepts currencies other than its settlement currency.

**`GatewayPaymentState`** enum: `Pending` (0), `Paid` (1), `Declined` (2).

### Reading settings

Call `GetConfig(key)` (from `PaymentGatewayBase`) to read a setting. Settings are stored as `integration:{pluginId}:{key}` in the application configuration and are written by the admin Integrations page.

```csharp
public string? GetBaseUrl() => GetConfig("gateway_url");
```

### Keeping data between calls

`CreatePaymentAsync`, `GetStatusAsync` and `RefundAsync` are separate calls, and a gateway may need something from one in the next (an order id, a cursor). Take `IPluginStore` in the plugin's constructor, next to the `IConfiguration` the host passes:

```csharp
public MyGateway(IConfiguration configuration, IPluginStore store) : base(configuration)
{
    _store = store;
}
```

The host provides it when it activates the plugin. It is plugin wide: scoped to your plugin id and to no client, with the same key rules and size limits as `call.Store`, and it is removed with the plugin's data when the plugin is removed and its data purged. It never reaches another plugin's values or any client's values; data about one client belongs in `call.Store`, which is bound to the call's client. The store is the same data as the call store of an admin call outside any client.

### Example

::: code-group

```csharp [MyGateway.cs]
using Hostpanel.Sdk.Base;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyCompany.Hostpanel.Plugins.MyGateway;

public sealed class MyGateway : PaymentGatewayBase, IPaymentPlugin
{
    public MyGateway(string pluginId, IConfiguration configuration, ILogger<MyGateway> logger)
        : base(pluginId, configuration, logger) { }

    public string CurrencyCode => "840"; // USD (ISO 4217 numeric)

    public Task<PaymentSession> CreatePaymentAsync(
        PaymentInstruction instruction, CancellationToken ct)
    {
        var url = GetConfig("gateway_url")
            ?? throw new InvalidOperationException("gateway_url is required");
        // ... call the gateway API and return a redirect URL
        return Task.FromResult(new PaymentSession("order-123", url + "/pay?order=123"));
    }

    public Task<GatewayPaymentStatus> GetStatusAsync(
        string gatewayOrderId, CancellationToken ct)
    {
        // ... query the gateway
        return Task.FromResult(
            new GatewayPaymentStatus(GatewayPaymentState.Paid, gatewayOrderId, "ok", null, null));
    }

    public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
    {
        // ... call the gateway refund endpoint
        return Task.FromResult("refund-ref-456");
    }
}
```

```json [plugin.json]
{
  "id": "my-gateway",
  "name": "My Gateway",
  "version": "1.0.0",
  "author": "Your Company",
  "description": "Accept payments through My Gateway.",
  "type": "Payment",
  "category": "Payment Gateways",
  "color": "#0369a1",
  "entryPoint": "MyCompany.Hostpanel.Plugins.MyGateway.MyGateway",
  "sdkVersion": "1.0",
  "showInIntegrations": true,
  "integrationsCategory": "Payment Gateways",
  "fields": [
    { "key": "gateway_url", "label": "Gateway URL", "type": "text",   "required": true  },
    { "key": "api_key",     "label": "API Key",     "type": "secret", "required": true  },
    { "key": "mode",        "label": "Mode",        "type": "select", "required": false }
  ]
}
```

```csharp [MyGatewayTests.cs]
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Hostpanel.Sdk.Plugins.Payment;
using Xunit;

public class MyGatewayTests
{
    [Fact]
    public async Task CreatePaymentAsync_ReturnsRedirectUrl()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["integration:my-gateway:gateway_url"] = "https://test.example.com",
                ["integration:my-gateway:api_key"]     = "test-key",
            })
            .Build();

        var gateway = new MyGateway("my-gateway", config, NullLogger<MyGateway>.Instance);
        var instruction = new PaymentInstruction(
            OrderNumber: "1001",
            AmountMinor: 5000,
            ReturnUrl:   "https://panel.example.com/payment/result",
            Description: "Invoice #1001",
            Language:    "en");

        var session = await gateway.CreatePaymentAsync(instruction, CancellationToken.None);

        Assert.False(string.IsNullOrEmpty(session.RedirectUrl));
        Assert.False(string.IsNullOrEmpty(session.GatewayOrderId));
    }
}
```

:::

## A provisioning plugin

Inherit `ProvisioningProviderBase` and implement `IProvisioningPlugin` from `Hostpanel.Sdk.Plugins.Provisioning`. Read `backend/src/Innovayse.Providers.CWP7` as the reference implementation — it shows the full hosting panel client contract and how to handle each provisioning command (create, suspend, unsuspend, terminate, change package, change password, login).

## The manifest (`plugin.json`)

Every plugin ZIP must have `plugin.json` at the root.

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Unique identifier: lower-case letters, digits and dashes, at most 64 characters. Must be stable across versions.' },
  { name: 'name', type: 'string', default: '', description: 'Display name shown in the Plugin Manager and Integrations.' },
  { name: 'version', type: 'string', default: '', description: 'Semantic version string, e.g. 1.0.0.' },
  { name: 'author', type: 'string', default: '', description: 'Author name shown on the plugin card.' },
  { name: 'description', type: 'string', default: '', description: 'One-line description shown on the plugin card.' },
  { name: 'type', type: 'string', default: '', description: 'Plugin type: Payment, Provisioning, Registrar, Identity, Fulfilment, Hosted or App.' },
  { name: 'category', type: 'string', default: '', description: 'Category used to group the plugin inside the Plugin Manager.' },
  { name: 'color', type: 'string', default: '', description: 'Hex colour for the plugin icon background, e.g. #0369a1.' },
  { name: 'icon', type: 'string', default: '', description: 'Optional image of the plugin: a path inside the package to an SVG or PNG file, for example icon.svg or assets/icon.png. Lowercase letters, digits, dot, hyphen and underscore, forward slashes, no leading slash, no .. segment, extension .svg or .png, at most 256 KiB. Omit it to show the colour block with the plugin initial.' },
  { name: 'entryPoint', type: 'string', default: '', description: 'Fully qualified class name of the class that implements the plugin interface.' },
  { name: 'sdkVersion', type: 'string', default: '', description: 'Manifest SDK version. The accepted value is 1.0. The assembly SDK major is checked separately and older majors are refused.' },
  { name: 'showInIntegrations', type: 'boolean', default: 'false', description: 'When true, the plugin appears as a card on the Integrations screen.' },
  { name: 'integrationsCategory', type: 'string', default: '', description: 'Category heading under which the card appears on the Integrations screen.' },
  { name: 'fields', type: 'array', default: '[]', description: 'Settings shown on the integration page, in order.' }
]" />

The `icon` file is checked when the plugin is installed and again when it is loaded. A path that leaves the package, a file that is missing (`PLUGIN_ICON_MISSING`), a file over the size limit, a `.png` that is not a PNG, and an SVG that is not well-formed XML or that holds a script, a `foreignObject`, an event handler attribute (`onload`, `onclick`, ...), a DOCTYPE, or a reference that leaves the image (an `href` that is not a `#fragment`, a `url(...)` that is not a `#fragment`, `@import`) are refused with `PLUGIN_ICON_INVALID`. The admin panel shows the image where it shows the colour block; the host serves it from `GET /api/admin/plugins/{id}/icon` with `X-Content-Type-Options: nosniff` and a policy that blocks script and every fetch.

The fields above cover manifest version 1. A manifest without `manifestVersion` is version 1; the host accepts `1` and `2` and refuses any other value (`PLUGIN_MANIFEST_VERSION_UNKNOWN`). `version` must be a strict `major.minor.patch` version. Manifest version 2 (`"manifestVersion": 2`) adds the members below.

### Manifest v2 members

<SettingTable :settings="[
  { name: 'manifestVersion', type: 'number', default: '1', description: 'Set to 2 to use the members below.' },
  { name: 'settings', type: 'array', default: '[]', description: 'Typed settings an administrator fills in. Supersedes fields; when settings is empty the host converts fields into settings.' },
  { name: 'capabilities', type: 'string[]', default: '[]', description: 'What the plugin can do, shown without activating it. Known names: storedCredentials (a payment plugin that also implements IStoredCredentialPlugin) and endpoints (the plugin class implements IPluginEndpoints; for a plugin that is not an App plugin the host serves its public callbacks only, see Callbacks from outside; an App plugin is served without declaring it). It must agree with the interfaces the class really implements.' },
  { name: 'hostAccess', type: 'string[]', default: '[]', description: 'Kinds of access to a client hosting the plugin asks the host for. The only kind today is ssh. The admin Plugins page shows each one a plugin declares.' },
  { name: 'locales', type: 'object', default: '{}', description: 'Texts by language code, then by key, for example the language en with the key menu.chat and the text Chat. A text key has at least two dot-separated parts, such as menu.chat. The en texts must define every key the manifest uses.' },
  { name: 'api', type: 'object', default: '{}', description: 'Routes the host forwards to the plugin: api.client and api.admin, each a list of routes.' },
  { name: 'ui', type: 'object', default: '{}', description: 'What the plugin adds to the panels: ui.client and ui.admin (menus and pages) and ui.slots (widgets in host slots).' },
  { name: 'externalOrigins', type: 'string[]', default: '[]', description: 'Web origins (scheme, host and port, such as https://example.com) that a link block of a plugin view may point at with an absolute https address, and that a preview block may frame (a pure https origin only).' }
]" />

Wherever a label, title or description may be a text, write either a key of `locales` or plain text. A value shaped like a key (letters, digits, dashes and underscores in two or more parts separated by dots) is looked up in the `en` texts and refused when it is not defined (`PLUGIN_LABEL_KEY_MISSING`).

Each entry in `settings`:

<SettingTable :settings="[
  { name: 'key', type: 'string', default: '', description: 'Setting key read with GetConfig(key).' },
  { name: 'label', type: 'string', default: '', description: 'Text shown for the setting.' },
  { name: 'type', type: 'string', default: '', description: 'Input kind: Text, Url, Select, Secret, Number, Boolean or Textarea. A Secret is stored encrypted and never returned by a read.' },
  { name: 'required', type: 'boolean', default: 'false', description: 'When true, the setting must be filled before the plugin can be enabled.' },
  { name: 'description', type: 'string', default: '', description: 'Help text shown under the setting.' },
  { name: 'default', type: 'string', default: '', description: 'Value used until the administrator sets one. Always written as text, for example 30 or true.' },
  { name: 'placeholder', type: 'string', default: '', description: 'Hint shown in an empty input.' },
  { name: 'min', type: 'number', default: '', description: 'Lowest value of a Number setting.' },
  { name: 'max', type: 'number', default: '', description: 'Highest value of a Number setting.' },
  { name: 'options', type: 'array', default: '[]', description: 'Choices of a Select setting, each an object with value (stored) and label (shown).' }
]" />

Each entry in `api.client` and `api.admin` is a route the host serves under `/api/client/plugins/{id}/...` and `/api/admin/plugins/{id}/ext/...`. The host checks sign-in, the caller's permission, CSRF and the rate limit before the plugin runs:

<SettingTable :settings="[
  { name: 'method', type: 'string', default: '', description: 'Get, Post, Put, Patch or Delete.' },
  { name: 'path', type: 'string', default: '', description: 'Path below the plugin prefix: a leading slash, then lowercase letters, digits, dashes and underscores, or {parameter} segments, for example /invoices/{id}. No empty segment and no .. segment. The same method and path may be declared once per area.' },
  { name: 'permission', type: 'string', default: '', description: 'Client routes only: a client permission name, or Any for every signed-in caller. An admin route declares none, because the admin area is guarded by the admin policy alone (PLUGIN_ADMIN_PERMISSION_DECLARED).' },
  { name: 'rateLimit', type: 'string', default: 'Default', description: 'Rate limit policy: Default, Strict (costly or sensitive routes, such as sending messages) or Heavy (rare, heavy work, such as an export).' },
  { name: 'streaming', type: 'boolean', default: 'false', description: 'When true, the route answers with a stream of server-sent events instead of one reply. A streaming route is also held to a concurrency cap.' }
]" />

`ui.client` and `ui.admin` each hold `menus` and `pages`. Each page:

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Page id, unique per area: 1 to 64 lowercase letters, digits, dashes and underscores, starting with a letter or digit.' },
  { name: 'path', type: 'string', default: '', description: 'Page path below the plugin prefix in the panel, with the same rules as an api path.' },
  { name: 'title', type: 'string', default: '', description: 'Page title.' },
  { name: 'view', type: 'string', default: '', description: 'Path inside the package of the view document that describes the page. The host renders it with its own components, so no plugin code runs in the browser.' },
  { name: 'permission', type: 'string', default: '', description: 'Client pages only: a client permission name, or Any. Not allowed on an admin page.' }
]" />

Each menu entry:

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Entry id, unique within the plugin.' },
  { name: 'label', type: 'string', default: '', description: 'Text shown in the menu.' },
  { name: 'page', type: 'string', default: '', description: 'Id of the page of the same area that the entry opens.' },
  { name: 'icon', type: 'string', default: '', description: 'Icon name: 1 to 64 lowercase letters, digits and dashes. Omit it for the default icon of the panel.' },
  { name: 'order', type: 'number', default: '0', description: 'Position in the menu, lowest first.' }
]" />

Each entry in `ui.slots` puts a widget into a slot the host offers. The slot list is versioned; the host offers `client.dashboard.cards` (no context), `client.service.tabs` (context key `serviceId`), `admin.client.panels` (`clientId`) and `admin.service.panels` (`serviceId` and `clientId`). A slot that is not listed, or a context key the slot does not offer, is refused.

<SettingTable :settings="[
  { name: 'id', type: 'string', default: '', description: 'Widget id, unique within the slot: lowercase letters, digits, dashes and underscores.' },
  { name: 'slot', type: 'string', default: '', description: 'Slot id, one of the four above.' },
  { name: 'view', type: 'string', default: '', description: 'Path inside the package of the view document that describes the widget.' },
  { name: 'title', type: 'string', default: '', description: 'Title shown with the widget.' },
  { name: 'order', type: 'number', default: '0', description: 'Position among the widgets of the slot, lowest first.' },
  { name: 'permission', type: 'string', default: '', description: 'Client slots only: a client permission name, or Any.' },
  { name: 'productIds', type: 'number[]', default: '[]', description: 'Products the widget is bound to; it shows only where the slot service belongs to one of them. Empty shows it for every product. An administrator can change the binding on the Plugins page.' },
  { name: 'contextKeys', type: 'string[]', default: '[]', description: 'Context the widget needs from the slot. The host verifies the keys for the caller and passes only these to the plugin.' }
]" />

### Callbacks from outside

A payment gateway calls the panel server to server, with no signed-in user: a card webhook, a pre check and confirmation, a bank result URL. A plugin that implements `IPluginEndpoints` can receive these through a public door, with no gateway code in the host. This is open to a plugin of any type: a payment plugin lists `"capabilities": ["endpoints"]` in its manifest and its class implements `IPluginEndpoints` beside `IPaymentPlugin`; an App plugin needs no capability. The capability opens this door only. The `api.client` and `api.admin` routes, pages, menus and slots of a plugin that is not an App plugin are not served, whatever it declares. A manifest that lists `endpoints` while its class does not implement `IPluginEndpoints` is refused at load with `PLUGIN_ENDPOINTS_NOT_IMPLEMENTED`, and a plugin that does not list it answers its callbacks with the same `404 PLUGIN_ROUTE_NOT_FOUND` as an unknown plugin.

Declare each callback in `api.callbacks`, a list of routes with the same `method` and `path` rules as `api.client` (a `permission` is not allowed, and neither is `streaming`; `rateLimit` is ignored, because the door has its own limit):

```json
{
  "api": {
    "callbacks": [
      { "method": "Post", "path": "/webhook" },
      { "method": "Post", "path": "/confirm/{step}" }
    ]
  }
}
```

The host serves them under `/api/plugins/{id}/callback/...`, so the second route above is `POST /api/plugins/{id}/callback/confirm/result`. Give that address to the gateway.

What the host does:

- It answers only a declared callback of an installed and enabled plugin. An unknown plugin, a switched-off or unconfigured one, an undeclared path and a wrong method all answer the same `404 PLUGIN_ROUTE_NOT_FOUND`, so the door never tells what is installed. The routes of `api.client` and `api.admin` are not reachable through it.
- There is no sign-in and no CSRF header. The plugin gets `call.Area == PluginArea.Callback`, no client reference, no permissions, a fixed caller reference `callback` and the plugin-wide store.
- The body is passed on byte for byte, never parsed or re-serialised, so a signature over it still verifies. `call.Headers` holds an allow-list of the request headers, exactly as received (see below), and `call.Query` the query string.
- A callback may answer with `PluginReply.Raw(status, body, contentType)`, for gateways that expect a plain answer such as `OK` as `text/plain`: the bytes, status and media type are written as given, with `X-Content-Type-Options: nosniff`. A raw reply is accepted on a callback only (on a client or admin route it is a plugin fault, `PLUGIN_FAILED`), its media type must be `text/plain`, `application/json`, `application/xml` or `application/x-www-form-urlencoded` (anything else, markup and script included, is `PLUGIN_FAILED`), and its body is held to `PluginApi__MaxBodyBytes`. Every raw reply also carries `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`.
- Only these request headers reach `call.Headers`: `Content-Type`, `Content-Length`, `User-Agent`, `Signature`, `Digest`, `Content-Digest`, `Date`, `Idempotency-Key`, `Webhook-Id`, `Webhook-Timestamp`, `Webhook-Signature`, and any `X-*` header except `X-Forwarded-*`, `X-Real-IP` and the host's own (`X-Language`, `X-Requested-With`, `X-Logout-Key`, `X-Service-Key`, `X-Renewed-Access-Token`). `Cookie`, `Authorization`, `Proxy-Authorization` and `Host` are never passed on. If your gateway signs a header that is not on this list, send it as an `X-*` header or put it in the body.
- The body is capped by `PluginApi__MaxBodyBytes` (413 `PLUGIN_REQUEST_TOO_LARGE` above it) and every request to the door is counted per caller address against `RateLimit__PluginCallback` (429 above it).
- A failure is logged with the plugin id only, never with a header, a query or a body.

What the plugin must do: verify the call itself (the gateway's signature, checksum or shared secret) before it trusts anything in it, using a secret from its own settings. Answer `PluginReply.Problem(400, ...)` or `403` for a call that does not verify, and a success reply for one that does; keep the work idempotent, because gateways deliver the same event more than once. Nothing in the host says the call is authentic.

A manifest that breaks one of these rules is refused at install and at startup with a `PLUGIN_*` manifest error code that names the offending member (`PLUGIN_CALLBACK_INVALID` for a callback that declares a permission or streams, `PLUGIN_ROUTE_PATH_INVALID` and `PLUGIN_ROUTE_DUPLICATE` for its path and repeats). Read `backend/src/Hostpanel.Sdk/Plugins/Manifest` for the exact types.

### Capabilities

A version 2 manifest may list `capabilities`, each a name from the SDK's `PluginCapabilities`:

| Capability | Allowed on | The plugin class must also implement |
|------------|------------|--------------------------------------|
| `storedCredentials` | `Payment` only | `IStoredCredentialPlugin` |
| `endpoints` | any type | `IPluginEndpoints` |

An `App` plugin answers routes and shows pages, menus and slots without declaring anything. Any other type that declares
`api` routes or `ui` menus, pages or slots must declare `endpoints`, or the package is refused at install and skipped at
startup (`PLUGIN_ENDPOINTS_NOT_DECLARED`). This is how one plugin, for example a `Fulfilment` plugin that sells credits,
also gives the client a screen. An unknown name, a name declared twice, or `storedCredentials` on a type other than
`Payment` is refused the same way (`PLUGIN_CAPABILITY_NOT_ALLOWED`).

A declared capability is checked against the class when the plugin is activated. If the class does not implement the
interface a capability promises, the plugin is not activated for anything, its own type's calls included: a payment
plugin that declares `storedCredentials` without implementing `IStoredCredentialPlugin` takes no payments, and a
fulfilment plugin that declares `endpoints` without implementing `IPluginEndpoints` receives no fulfilment calls. The
admin log names the plugin and the capability.

Each entry in `fields`:

<SettingTable :settings="[
  { name: 'key', type: 'string', default: '', description: 'Setting key read with GetConfig(key).' },
  { name: 'label', type: 'string', default: '', description: 'Label shown next to the field on the integration page.' },
  { name: 'type', type: 'string', default: 'text', description: 'Field type: text, secret, select or textarea.' },
  { name: 'required', type: 'boolean', default: 'false', description: 'When true, the integration page shows Not configured until this field is filled.' }
]" />

## View state

A page or slot view (`{ "schema": 1, "blocks": [...] }`) can react to its own data without a reload. Three optional
members do it; all need SDK 2.2.0 or later, and the view schema stays 1.

**A condition** is one shape, used by `visibleWhen` and by `followUp.until`, with exactly these three members:

```json
{ "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "site.status", "equals": ["ready", "live"] }
```

- `route` is a `GET` route your manifest declares in the same area as the page or slot.
- `field` is a dotted path into the JSON answer (letters, digits, `_` and `-` per segment, at most 200 characters).
- `equals` is one JSON value (string, number, `true`, `false`, `null`) or a list of 1 to 20 of them. The condition holds
  when the field strictly equals one of them: `"1"` does not match `1`.

**`visibleWhen`** goes on any block, including header actions and blocks inside tabs. The block is shown only when the
condition holds; while it is read, when the read fails, or when the field is missing, it stays hidden. It is read again
when the block's own `id` is refreshed. Hide a create form once a site exists:

```json
{
  "kind": "form",
  "id": "create-site",
  "visibleWhen": { "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "exists", "equals": false },
  "fields": [
    { "key": "name", "label": "site.name", "type": "text", "required": true },
    { "key": "description", "label": "site.description", "type": "textarea" }
  ],
  "submit": {
    "route": { "method": "post", "path": "/services/{serviceId}/site" },
    "label": "site.create",
    "refresh": ["create-site", "site"],
    "resetOnSuccess": true
  }
}
```

**How often a route is read.** A page is one picture of your data. Every block, condition and card that names the same
`GET` route on a page is answered by one request, and the panel sends a new one only when the page is refreshed (an
action, a form, a conversation turn or a follow-up asked for it) or after any call that is not a read ended. A block
that appears later takes the answer the page already holds, so a page naming one state route in ten places costs one
request per refresh, not ten. The panel keeps no read past that moment and none from another page. A route read by
several blocks is still read once for each refresh, so keep it cheap to answer.

**`resetOnSuccess`** is `true` or `false`, only inside a form's `submit`. After a successful submit every field goes
back to empty.

**`followUp`** goes on an action block or inside a form's `submit`, with exactly `refresh` and `until`. After a
successful call (and its normal `refresh`), the panel reads `until.route` again and again, refreshing the listed blocks
each time, until the condition holds (then once more), it runs out of attempts, or the reader leaves the page. A failed
read counts as an attempt. The watch outlives the button that started it, so a button that hides itself once its work is
done (a `visibleWhen` on the same data) does not stop it. The sentence the action answered with is taken down when the
condition holds, so a line like "is being published" does not outlive the publish. Show a publish result without a
reload:

```json
{
  "kind": "action",
  "label": "site.publish",
  "route": { "method": "post", "path": "/services/{serviceId}/site/publish" },
  "followUp": {
    "refresh": ["publish-result"],
    "until": {
      "route": { "method": "get", "path": "/services/{serviceId}/site" },
      "field": "publish.state",
      "equals": ["done", "failed"]
    }
  }
}
```

Both examples assume the view also has blocks with the ids `site` and `publish-result`, and that the manifest declares
the three routes in its `api` block and the label keys in `locales.en`:

```json
{
  "locales": {
    "en": {
      "site.name": "Site name",
      "site.description": "Description",
      "site.create": "Create site",
      "site.publish": "Publish"
    }
  }
}
```

`refresh` lists 1 to 20 block `id`s, each present in the same view. Give every block that has an `id` its own, in any
container (a tab, a pane, a header): the panel treats blocks that share one as one block, so a refresh would reach all of
them and a new follow-up of one would end the running follow-up of the other. A view that gives one `id` to two blocks is
refused at install (`PLUGIN_VIEW_BLOCK_ID_REPEATED`); a plugin that is already installed with such a view still loads, the
host writes a warning in its log, and the panel draws every block but acts on blocks with one `id` as if they were one. The `id` of a tab or of a preview source is a separate name and may match a
block's. How often and how long the panel polls is the
host's choice, not yours: every 3 seconds, at most 20 times (about a minute). A long job should report its own state on
the `until` route rather than expect more time.

**`bodySource`** goes on an action block in the client area and makes the button send what the reader saw. It holds
exactly a `GET` `route` and 1 to 10 `fields`, each a body member (a camelCase name, not a label member such as `title`)
and the dotted path it is read from. Without it an action posts `{}`, as before.

```json
{
  "kind": "action",
  "id": "publish",
  "label": "site.publish",
  "route": { "method": "post", "path": "/services/{serviceId}/site/publish" },
  "labelSource": { "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "publishLabel" },
  "bodySource": {
    "route": { "method": "get", "path": "/services/{serviceId}/site" },
    "fields": { "expectedVersion": "publishVersion" }
  }
}
```

The action needs an `id` and a `POST`, `PUT` or `PATCH` route, and a `labelSource` must read the same route: the panel
makes one read for both, so the button sends the value on its label. The button is disabled until every member has been
read as a string, number or boolean. The body is fixed at the click. A refused call reads the data and the action's
`refresh` blocks again, so answer a conflict with your own sentence; your route, not the panel, decides whether the value
is still current. A result card's action in a chat event carries a literal `body` of the same shape; an event with a
malformed one loses that action.

A view that breaks these rules is refused at install and skipped at startup: `PLUGIN_VIEW_ACTION_BODY_INVALID` (a bad
`bodySource`, or one in the wrong place), `PLUGIN_VIEW_CONDITION_INVALID` (a bad
condition, a condition route that is not `GET`, `visibleWhen` outside a block), `PLUGIN_VIEW_FOLLOW_UP_INVALID` (a bad
`followUp` or one in the wrong place, a `refresh` id no block carries, `resetOnSuccess` that is not a boolean or not in a
`submit`), and `PLUGIN_VIEW_ROUTE_UNDECLARED` for a condition route the manifest does not declare.

Two checks were added after plugins were already installed, so they apply to an **install** only: `PLUGIN_VIEW_BLOCK_ID_REPEATED`
(one `id` on more than one block) and `PLUGIN_VIEW_INVALID` for a mistyped display member (a header's `compact`,
`titleSource` or `subtitleSource`, an action's `labelSource`, a preview's `note`, a table's `display`). A plugin that is
already installed with such a view is not removed by a host upgrade: it loads, and the host writes one warning per fault
to its log. Fix them in your next version, because uploading it will be refused until you do.

## Working screens: layout, split and preview

Needs SDK 3.2.0 or later; the view schema stays 1.

A client page may set `"layout": "workspace"` (`PluginPageLayouts`) in `plugin.json`: full width, no service side menu,
a slim link back to the service (its `{serviceId}` path parameter names which). Absent means `standard`. An admin page
takes no layout. A bad value is `PLUGIN_PAGE_LAYOUT_UNKNOWN`.

`split` puts two panes side by side, stacked on a phone, and never nests inside a split (`PLUGIN_VIEW_SPLIT_INVALID`):

```json
{ "kind": "split", "panes": [ { "blocks": [ { "kind": "chat", "send": { "method": "post", "path": "/chat" } } ] },
                              { "blocks": [ { "kind": "preview", "sources": [ ] } ] } ] }
```

`preview` shows one address in a sandboxed frame, on a client page only:

```json
{
  "kind": "preview",
  "id": "site-preview",
  "size": "desktop",
  "sources": [
    { "id": "preview", "label": "site.preview", "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "previewUrl" },
    { "id": "live", "label": "site.live", "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "liveUrl" }
  ]
}
```

Up to 5 `sources`, each exactly `id`, `label`, a `GET` `route` and a dotted `field`; `size` is `desktop` or `phone`, and
the reader can switch both. A bad shape, or a preview in a slot or on an admin page, is `PLUGIN_VIEW_PREVIEW_INVALID`.

**Letting the reader select elements.** A preview may declare `selection`, and a chat in the same document `"selection": true`; both are off when absent:

```json
"selection": {
  "sources": ["preview"],
  "route": { "method": "post", "path": "/services/{serviceId}/site/selection" },
  "release": { "route": { "method": "get", "path": "/services/{serviceId}/site/state" }, "field": "previewRelease" }
}
```

`sources` are the source ids that may select: leave a live site out. The framed page runs your own script and talks to the panel with `postMessage`, `{ "type": "hostpanel.preview.selection", "version": 1, "release": "...", "event": "ready" | "toggle" | "exit", "id": "..." }`, and receives `{ type, version, "event": "state", "active": bool, "selected": [ids] }` from the panel. The select button appears only after `ready`. `id` is a dotted path of letters and digits, at most 128 characters and 20 per selection. **A message is a claim, never trusted:** the panel accepts it only from its own frame's window, in exactly this shape, naming the release your route reports, on a listed source, and a `toggle` only while the mode is on; any script in the frame could still send one. So your `route` gets `{ "releaseId", "ids" }` and answers `{ "items": [{ "id", "label" }] }` with the ids it really knows, and the panel draws only those labels. The next chat message carries `"selection": { "releaseId", "ids" }`, which your server must check again; take no text from the frame. Treat `ids` and `releaseId` as untrusted input in both routes: they come from page script, whatever the panel checked first, so validate them against your own data and never use one as a path, a query fragment or text for a model without that check. A selection is emptied when the release changes. The panel then ignores the frame until it says `ready` again, so **the framed page must send `ready` again after a release change** (a new document does so by loading; a page that updates itself in place must send it itself), or the select button stays hidden. A "try again" (`resend`) sends the same selection while its release is on the preview; once the release has changed the panel sends nothing, tells the reader the page changed and to select again, and leaves the text in the message field, because the message without its selection would reach further than what the reader chose. The panel only reads the release when it reads the block, so **your chat route must refuse a `releaseId` that is no longer on the preview, with a problem reply and before it changes anything**: the panel's check is a courtesy, and yours is the one that holds. One preview per document may declare `selection`, since the claim does not say which preview made it. A bad shape, a second selecting preview, or a chat selection with no preview selection, is `PLUGIN_VIEW_SELECTION_INVALID`.

**What may be framed.** The address the route returns is shown only when its origin is one of your manifest's
`externalOrigins` (a pure `https` origin) or a domain of the reader's own active hosting services on a product the
operator bound to your plugin's slot widgets, as the service's domain was typed at order (with its `www` name, and only for
a reader who may see services; your manifest's `productIds` do not count, nothing bound means no domains, and the panel's
own origin is never on the list). Anything else shows nothing, with no message, and your plugin cannot
change that: the host writes the list per reader, and the same list narrows the page's `frame-src`, so a browser refuses
any other frame too.

**The frame is `sandbox="allow-scripts allow-forms"`.** Scripts run, but the frame has an opaque origin: no cookies or
storage of the site's own, no pop-ups, no top navigation, no access to the panel. A site that needs its own session
will look signed out there, and a site that refuses framing (`X-Frame-Options`, `frame-ancestors`) stays blank, so send
your preview without those headers.

On a developer machine, `PLUGIN_PREVIEW_DEV_HTTP_ORIGIN=http://localhost:8088` in `docker/.env` (the dev compose file maps
it to `PluginPreview__DevelopmentHttpOrigins__0`, and `PLUGIN_PREVIEW_DEV_HTTP_ORIGIN_2` to `__1`, for a second address;
empty means none) allows an exact `http` origin on a loopback host. Outside the Development environment the host refuses to start with it set.

## Chat events

Needs SDK 3.2.0 or later. A `chat` block's streaming route may send these events (`PluginChatEvents`) beside its reply
text, so a reader can see what happened without reading it out of a paragraph. Text is plain and already in the reader's
language. `error` and `done` stay the host's. The reply text itself (`delta`, `message`, an assistant row of `history`) may
use a small Markdown subset: paragraphs, `#` headings, `-` and `1.` lists, fenced code, `**bold**`, `*italic*` and
`` `code` ``. HTML and Markdown links in it stay plain text; send a link with the `link` event.

```json
{ "event": "step", "data": { "id": "build", "label": "Building the site", "state": "running" } }
{ "event": "step", "data": { "id": "build", "label": "Building the site", "state": "done" } }
{ "event": "result", "data": { "tone": "success", "title": "Published", "text": "Your site is live.", "actions": [
  { "label": "Open site", "href": "https://example.com/" },
  { "label": "Roll back", "route": { "method": "post", "path": "/services/{serviceId}/site/rollback" }, "confirm": "Roll back?" } ] } }
{ "event": "issue", "data": { "text": "The build failed: the theme is missing." } }
```

- `step` is one row of a list in the reply; sending its `id` again (or its `label`, without an `id`) updates the row.
  `state` is `running`, `done` or `failed`.
- `result` is a card. `tone` is `info` (default), `success`, `warning` or `danger`; at least one of `title` and `text`.
  At most 5 `actions`, each with a `label` and exactly one of `route` (one of your own, called through the host, so an
  undeclared route or a missing permission is refused as usual) or `href` (under the rule of a link block).
  A card stays in the conversation after what it offers has changed, so an action may carry `visibleWhen`: the same
  condition a view block has, except that the event spells the value list `equalsAny` (a C# record cannot have a member
  named `equals`): `{ "route": { "method": "get", "path": "/services/{serviceId}/site" }, "field": "publishVersion",
  "equalsAny": [5] }`. The route is a `GET` route of your own that the page can fill, `field` a dotted path, and
  `equalsAny` one to 20 strings, numbers, booleans or `null`; the action shows only while the field strictly equals one
  of them. The panel reads it when the card is drawn and again whenever a block named in the chat's `refresh` is read
  again (after a turn, and after a card action), and hides the action while it is being read, when the read fails and
  when the condition does not hold. It applies to a route, an `href` and a `resend` action alike. Without it an action
  always shows. A condition the panel cannot read drops the action, since a button meant to hide must never show.
  Your route still decides whether a call is current: the condition is a courtesy to the reader, not the check.
  A row left with no visible action is hidden too.
- `issue` is your own failure, in the danger tone. The stream goes on; end it when you are done.

A panel ignores an event it cannot read. **Stop** cancels the stream: your enumerator's token fires (`EnumeratorCancellation`)
and nothing more is sent. Stop does not undo anything, so observe the token and leave your own work consistent.

## Package it

Folder layout inside the ZIP:

```
plugin.json                                ← at the root
MyCompany.Hostpanel.Plugins.MyGateway.dll  ← your assembly
plugin.sig                                 ← signature file (required on production servers)
```

**Assembly naming.** The prefix `Innovayse.Hostpanel.` belongs to the host, so a package carrying a DLL under it is refused. The one exception is the plugin namespace `Innovayse.Hostpanel.Plugins.<Name>` (for example `Innovayse.Hostpanel.Plugins.Stripe.dll`), which is how official plugins are named and is always allowed in a package. The dot after `Plugins` is the boundary: `Innovayse.Hostpanel.PluginsX.dll` and `Innovayse.Hostpanel.Plugins.dll` are still host names and are refused. No host assembly is ever named under the plugin namespace (an architecture test guards it), so a plugin can never shadow one. Third-party plugins keep their own prefix, such as `MyCompany.MyGateway`.

Do **not** include the SDK assembly (`Hostpanel.Sdk.dll`) or any assembly the host already provides. The server refuses packages that contain a host assembly (`PLUGIN_HOST_ASSEMBLY_REFUSED`). Give your assemblies names no other plugin uses: a package is also refused (`PLUGIN_ASSEMBLY_CONFLICT`) when one of its DLL or EXE file names, compared without regard to case, is already shipped by another installed or staged plugin, because the loader would bind both plugins to one copy. The rule covers every DLL in the package, shared third-party libraries included (a JSON or HTTP library two plugins both ship counts): if another installed plugin already ships a library of that name, pick a different assembly name or merge the library into your own assembly. Upgrading the same plugin `id` never conflicts with itself. A duplicate plugin `id` is skipped at startup without error — the first loaded version wins (built-in plugins load before uploaded ones).

### Sign the package

Packages loaded from outside the Development and Testing environments must be signed. To skip signature checks, open **Plugins** in the admin panel and switch on **Allow unsigned plugins**. The `PluginSigner` tool is part of the Hostpanel repository (`backend/tools/Innovayse.Hostpanel.PluginSigner`), not the SDK. Run it on the **unpacked folder** before zipping. The tool uses `Hostpanel.Sdk.Packaging` (3.1.0) for the signature format, so the signature it writes is checked by the same code the host installs with, and your pipeline can call `PluginSigner` and `PluginSignatureValidator` from that package to verify a signed folder before release.

Generate a key pair. The key file is a base64-encoded private key; store it outside any git working tree (the tool refuses to sign if the key file is inside one):

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- keygen my-key.key
```

Sign the plugin folder:

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- sign ./MyCompany.Hostpanel.Plugins.MyGateway my-key.key my-key-id
```

This writes `plugin.sig` into the folder. Zip the folder after signing.

The operator must add the matching public key to `PluginSigning:OperatorKeys` in their deployment environment, or the package will be refused with `PLUGIN_SIGNATURE_KEY_UNKNOWN`. When you update a plugin, re-sign the new folder before re-zipping — updating re-checks the signature and the server will refuse a stale or missing `plugin.sig`.

## Test it

Write unit tests for your gateway against a stubbed HTTP client, so no test needs the real payment provider.

For a manual end-to-end test:

<Steps>
<li>

Start a local Hostpanel stack (see [Local development](/developers/local-dev)).

</li>
<li>

Build your class library in Release, sign the output folder with `PluginSigner`, zip it, and upload the ZIP via **Settings → Plugin Manager → Install Plugin**. In Development and Testing environments unsigned packages load without any switch. On other servers, open **Plugins** in the admin panel and switch on **Allow unsigned plugins** to skip signature verification.

</li>
<li>

Restart the server from the "Restart required" banner.

</li>
<li>

Go to **Integrations**, find your plugin card, click **Configure →**, enter the test credentials, save and enable.

</li>
<li>

Create a test client, raise an invoice, and pay it. Verify the payment appears in **Gateway Transaction Log**. If signature verification fails, the upload is rejected with `PLUGIN_UNSIGNED`.

</li>
</Steps>

## Share it

Use semantic versioning (`major.minor.patch`). Include a changelog so operators know what changed. Operators install via the Plugin Manager by uploading the ZIP.

<Callout type="danger">
Never paste live credentials (API keys, secrets) into a screenshot, an issue or a pull request. Use test credentials or mask them.
</Callout>
