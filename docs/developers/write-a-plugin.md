---
title: Write a plugin
---

# Write a plugin

A plugin is a .NET class library that references the Hostpanel SDK and adds a payment gateway, a provisioning module, a domain registrar or another capability. The plugin never references the Hostpanel host directly — only the SDK.

## Before you start

The SDK project lives at `backend/src/Innovayse.SDK` in the repository. Reference it from your class library, or reference the built `Innovayse.SDK.dll` directly. The bundled plugins in `backend/src/Innovayse.Providers.*` are the reference implementations; read their code before writing your own.

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

## A payment plugin

Inherit `PaymentGatewayBase` and implement `IPaymentPlugin` from `Innovayse.SDK.Plugins.Payment`.

### Required members

| Member | Description |
|--------|-------------|
| `string CurrencyCode` | ISO 4217 currency code this gateway settles in (e.g. `"AMD"`) |
| `bool AcceptsCurrency(string iso4217)` | Return `true` when the gateway can charge in the given currency |
| `Task<PaymentSession> CreatePaymentAsync(PaymentInstruction instruction, CancellationToken ct)` | Start a payment session and return a redirect URL |
| `Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)` | Poll the gateway for the current state |
| `Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)` | Issue a refund; return the gateway's refund reference |

### Types

**`PaymentInstruction`** properties: `OrderNumber`, `AmountMinor`, `ReturnUrl`, `Description`, `Language`.

**`PaymentSession`** properties: `GatewayOrderId`, `RedirectUrl`.

**`GatewayPaymentStatus`** properties: `State` (`GatewayPaymentState`), `TransactionId`, `RawStatus`, `AmountMinor`, `Currency`.

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

public sealed class MyGateway : PaymentGatewayBase, IPaymentPlugin
{
    public MyGateway(IConfiguration configuration)
        : base("my-gateway", configuration) { }

    public string CurrencyCode => "USD";

    public bool AcceptsCurrency(string iso4217) => iso4217 == "USD";

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
  "entryPoint": "MyCompany.MyGateway.MyGateway",
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

        var gateway = new MyGateway(config);
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
  { name: 'type', type: 'string', default: '', description: 'Plugin type: Payment, Provisioning, Registrar, Identity, Fulfilment or Hosted.' },
  { name: 'category', type: 'string', default: '', description: 'Category used to group the plugin inside the Plugin Manager.' },
  { name: 'color', type: 'string', default: '', description: 'Hex colour for the plugin icon background, e.g. #0369a1.' },
  { name: 'entryPoint', type: 'string', default: '', description: 'Fully qualified class name of the class that implements the plugin interface.' },
  { name: 'sdkVersion', type: 'string', default: '', description: 'SDK version the plugin was built against. Supported value: 1.0.' },
  { name: 'showInIntegrations', type: 'boolean', default: 'false', description: 'When true, the plugin appears as a card on the Integrations screen.' },
  { name: 'integrationsCategory', type: 'string', default: '', description: 'Category heading under which the card appears on the Integrations screen.' },
  { name: 'fields', type: 'array', default: '[]', description: 'Settings shown on the integration page, in order.' }
]" />

Each entry in `fields`:

<SettingTable :settings="[
  { name: 'key', type: 'string', default: '', description: 'Setting key read with GetConfig(key).' },
  { name: 'label', type: 'string', default: '', description: 'Label shown next to the field on the integration page.' },
  { name: 'type', type: 'string', default: 'text', description: 'Field type: text, secret, select or textarea.' },
  { name: 'required', type: 'boolean', default: 'false', description: 'When true, the integration page shows Not configured until this field is filled.' }
]" />

## Package it

Folder layout inside the ZIP:

```
plugin.json              ← at the root
MyCompany.MyGateway.dll  ← your assembly
```

Do **not** include the SDK assembly (`Innovayse.SDK.dll`) or any assembly the host already provides. The server refuses packages that contain a host assembly (`PLUGIN_HOST_ASSEMBLY_REFUSED`). A duplicate plugin `id` is skipped at startup without error — the first loaded version wins (built-in plugins load before uploaded ones).

## Test it

Write unit tests for your gateway against a stubbed HTTP client — see `backend/tests/Innovayse.Inecobank.Tests` as the pattern.

For a manual end-to-end test:

<Steps>
<li>

Start a local Hostpanel stack (see [Local development](/developers/local-dev)).

</li>
<li>

Build your class library in Release, zip the output folder, and upload the ZIP via **Settings → Plugin Manager → Install Plugin**.

</li>
<li>

Restart the server from the "Restart required" banner.

</li>
<li>

Go to **Integrations**, find your plugin card, click **Configure →**, enter the test credentials, save and enable.

</li>
<li>

Create a test client, raise an invoice, and pay it. Verify the payment appears in **Gateway Transaction Log**.

</li>
</Steps>

## Share it

Use semantic versioning (`major.minor.patch`). Include a changelog so operators know what changed. Operators install via the Plugin Manager by uploading the ZIP.

<Callout type="danger">
Never paste live credentials (API keys, secrets) into a screenshot, an issue or a pull request. Use test credentials or mask them.
</Callout>
