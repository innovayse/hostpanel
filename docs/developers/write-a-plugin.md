---
title: Write a plugin
---

# Write a plugin

A plugin is a .NET class library that references the Hostpanel SDK and adds a payment gateway, a provisioning module, a domain registrar or another capability. The plugin never references the Hostpanel host directly — only the SDK.

## Before you start

The SDK project lives at `backend/src/Innovayse.SDK` in the repository. Reference it from your class library, or reference the built `Innovayse.SDK.dll` directly. The bundled plugins in `backend/src/Innovayse.Providers.*` are the reference implementations; read their code before writing your own.

### Name the project

Name a plugin project `<Company>.HostpanelPlugins.<Name>`, the .NET plugin convention `<Company>.<Product>.<Type>.<Name>` (as in `Serilog.Sinks.Console`) with product and type joined into one segment. They are joined because the host refuses to load any plugin assembly whose name starts with `Innovayse.Hostpanel.` (`PLUGIN_HOST_ASSEMBLY_REFUSED`): that prefix belongs to the host's own assemblies, and a plugin must never be able to stand in for one. The project, its folder, its assembly and its root namespace all carry that name, and its test project adds `.Tests`. Innovayse's own plugins are `Innovayse.HostpanelPlugins.<Name>`, with tests in `Innovayse.HostpanelPlugins.<Name>.Tests`, each in its own repository `innovayse/hostpanel-<name>`: Innovayse Sites, for example, is `Innovayse.HostpanelPlugins.Sites` in `innovayse/hostpanel-sites`. The bundled `Innovayse.Providers.*` projects predate the convention and are renamed as they move to their own repositories.

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

Inherit `PaymentGatewayBase` and implement `IPaymentPlugin` from `Innovayse.SDK.Plugins.Payment`.

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

### Example

::: code-group

```csharp [MyGateway.cs]
using Innovayse.SDK.Base;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Payment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyCompany.HostpanelPlugins.MyGateway;

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
  "entryPoint": "MyCompany.HostpanelPlugins.MyGateway.MyGateway",
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
using Innovayse.SDK.Plugins.Payment;
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

Inherit `ProvisioningProviderBase` and implement `IProvisioningPlugin` from `Innovayse.SDK.Plugins.Provisioning`. Read `backend/src/Innovayse.Providers.CWP7` as the reference implementation — it shows the full hosting panel client contract and how to handle each provisioning command (create, suspend, unsuspend, terminate, change package, change password, login).

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
  { name: 'entryPoint', type: 'string', default: '', description: 'Fully qualified class name of the class that implements the plugin interface.' },
  { name: 'sdkVersion', type: 'string', default: '', description: 'Manifest SDK version. The accepted value is 1.0. The assembly SDK major is checked separately and older majors are refused.' },
  { name: 'showInIntegrations', type: 'boolean', default: 'false', description: 'When true, the plugin appears as a card on the Integrations screen.' },
  { name: 'integrationsCategory', type: 'string', default: '', description: 'Category heading under which the card appears on the Integrations screen.' },
  { name: 'fields', type: 'array', default: '[]', description: 'Settings shown on the integration page, in order.' }
]" />

The fields above cover manifest version 1. Manifest version 2 (`"manifestVersion": 2`) adds `settings`, `capabilities`, `locales`, `api`, `ui` and `externalOrigins`; documentation for those fields will be added in a follow-up page.

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

**`resetOnSuccess`** is `true` or `false`, only inside a form's `submit`. After a successful submit every field goes
back to empty.

**`followUp`** goes on an action block or inside a form's `submit`, with exactly `refresh` and `until`. After a
successful call (and its normal `refresh`), the panel reads `until.route` again and again, refreshing the listed blocks
each time, until the condition holds (then once more), it runs out of attempts, or the reader leaves the page. A failed
read counts as an attempt. Show a publish result without a reload:

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

`refresh` lists 1 to 20 block `id`s, each present in the same view. How often and how long the panel polls is the
host's choice, not yours: every 3 seconds, at most 20 times (about a minute). A long job should report its own state on
the `until` route rather than expect more time.

A view that breaks these rules is refused at install and skipped at startup: `PLUGIN_VIEW_CONDITION_INVALID` (a bad
condition, a condition route that is not `GET`, `visibleWhen` outside a block), `PLUGIN_VIEW_FOLLOW_UP_INVALID` (a bad
`followUp` or one in the wrong place, a `refresh` id no block carries, `resetOnSuccess` that is not a boolean or not in a
`submit`), and `PLUGIN_VIEW_ROUTE_UNDECLARED` for a condition route the manifest does not declare.

## Package it

Folder layout inside the ZIP:

```
plugin.json                                ← at the root
MyCompany.HostpanelPlugins.MyGateway.dll  ← your assembly
plugin.sig                                 ← signature file (required on production servers)
```

Do **not** include the SDK assembly (`Innovayse.SDK.dll`) or any assembly the host already provides. The server refuses packages that contain a host assembly (`PLUGIN_HOST_ASSEMBLY_REFUSED`). A duplicate plugin `id` is skipped at startup without error — the first loaded version wins (built-in plugins load before uploaded ones).

### Sign the package

Packages loaded from outside the Development and Testing environments must be signed. To skip signature checks, open **Plugins** in the admin panel and switch on **Allow unsigned plugins**. The `PluginSigner` tool is part of the Hostpanel repository (`backend/tools/Innovayse.Hostpanel.PluginSigner`), not the SDK. Run it on the **unpacked folder** before zipping.

Generate a key pair. The key file is a base64-encoded private key; store it outside any git working tree (the tool refuses to sign if the key file is inside one):

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- keygen my-key.key
```

Sign the plugin folder:

```bash
dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- sign ./MyCompany.HostpanelPlugins.MyGateway my-key.key my-key-id
```

This writes `plugin.sig` into the folder. Zip the folder after signing.

The operator must add the matching public key to `PluginSigning:OperatorKeys` in their deployment environment, or the package will be refused with `PLUGIN_SIGNATURE_KEY_UNKNOWN`. When you update a plugin, re-sign the new folder before re-zipping — updating re-checks the signature and the server will refuse a stale or missing `plugin.sig`.

## Test it

Write unit tests for your gateway against a stubbed HTTP client — see `backend/tests/Innovayse.Inecobank.Tests` as the pattern.

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
