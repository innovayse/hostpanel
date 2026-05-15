# Plugin System — Plan A: SDK + Plugin Loading

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the `Innovayse.SDK` NuGet-ready class library and the startup-time plugin loading infrastructure in `Innovayse.Infrastructure`.

**Architecture:** New `Innovayse.SDK` classlib with interfaces and base classes. `Innovayse.Infrastructure` gains a `Plugins/` folder with `PluginManifestReader`, `PluginRegistry`, and `PluginLoader`. `DependencyInjection.cs` calls `PluginLoader.DiscoverAndRegister` at startup.

**Tech Stack:** C# 12, ASP.NET Core 8, `System.Reflection`, `System.Text.Json`, keyed DI (`AddKeyedScoped`), Microsoft.Extensions.Logging

---

### Task 1: Create `Innovayse.SDK` class library project

**Files:**
- Create: `backend/src/Innovayse.SDK/Innovayse.SDK.csproj`

- [ ] **Step 1: Scaffold the project**

```bash
cd backend
dotnet new classlib -n Innovayse.SDK -o src/Innovayse.SDK --framework net8.0
dotnet sln add src/Innovayse.SDK/Innovayse.SDK.csproj
rm src/Innovayse.SDK/Class1.cs
```

- [ ] **Step 2: Set csproj content**

Replace `src/Innovayse.SDK/Innovayse.SDK.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <RootNamespace>Innovayse.SDK</RootNamespace>
  </PropertyGroup>

</Project>
```

- [ ] **Step 3: Verify build**

```bash
cd backend
dotnet build src/Innovayse.SDK/Innovayse.SDK.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Innovayse.SDK/ backend/Innovayse.sln
git commit -m "feat(sdk): scaffold Innovayse.SDK classlib"
```

---

### Task 2: Plugin manifest POCOs

**Files:**
- Create: `backend/src/Innovayse.SDK/Plugins/PluginType.cs`
- Create: `backend/src/Innovayse.SDK/Plugins/PluginField.cs`
- Create: `backend/src/Innovayse.SDK/Plugins/PluginManifest.cs`

- [ ] **Step 1: Create `PluginType.cs`**

```csharp
namespace Innovayse.SDK.Plugins;

/// <summary>
/// Categorises the service a plugin provides.
/// </summary>
public enum PluginType
{
    /// <summary>Hosting / server provisioning (cPanel, Plesk, CWP, etc.).</summary>
    Provisioning,

    /// <summary>Payment gateway (Stripe, PayPal, etc.).</summary>
    Payment,

    /// <summary>Domain registrar (Namecheap, ENOM, etc.).</summary>
    Registrar,
}
```

- [ ] **Step 2: Create `PluginField.cs`**

```csharp
namespace Innovayse.SDK.Plugins;

/// <summary>
/// Describes a single configuration field exposed by a plugin.
/// </summary>
public sealed class PluginField
{
    /// <summary>Gets or sets the settings-table key for this field.</summary>
    public required string Key { get; init; }

    /// <summary>Gets or sets the human-readable label shown in the admin UI.</summary>
    public required string Label { get; init; }

    /// <summary>Gets or sets the field input type: text | secret | select | textarea.</summary>
    public required string Type { get; init; }

    /// <summary>Gets or sets a value indicating whether this field must be non-empty before the integration can be enabled.</summary>
    public bool Required { get; init; }
}
```

- [ ] **Step 3: Create `PluginManifest.cs`**

```csharp
namespace Innovayse.SDK.Plugins;

/// <summary>
/// Deserialised representation of a plugin's <c>plugin.json</c> manifest file.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>Gets or sets the unique plugin identifier (e.g. "innovayse-cwp").</summary>
    public required string Id { get; init; }

    /// <summary>Gets or sets the human-readable display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the semver version string.</summary>
    public required string Version { get; init; }

    /// <summary>Gets or sets the author name or organisation.</summary>
    public required string Author { get; init; }

    /// <summary>Gets or sets a short description shown in the admin panel.</summary>
    public required string Description { get; init; }

    /// <summary>Gets or sets the plugin category type.</summary>
    public required PluginType Type { get; init; }

    /// <summary>Gets or sets the display category label (e.g. "Hosting / Provisioning").</summary>
    public required string Category { get; init; }

    /// <summary>Gets or sets the hex colour used for the plugin logo block.</summary>
    public string Color { get; init; } = "#6366f1";

    /// <summary>Gets or sets the fully-qualified type name of the provider implementation.</summary>
    public required string EntryPoint { get; init; }

    /// <summary>Gets or sets the SDK version this plugin was compiled against.</summary>
    public required string SdkVersion { get; init; }

    /// <summary>Gets or sets the list of configuration fields for this plugin.</summary>
    public IReadOnlyList<PluginField> Fields { get; init; } = [];
}
```

- [ ] **Step 4: Build**

```bash
cd backend
dotnet build src/Innovayse.SDK/Innovayse.SDK.csproj
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.SDK/
git commit -m "feat(sdk): add PluginType, PluginField, PluginManifest POCOs"
```

---

### Task 3: Marker interfaces

**Files:**
- Create: `backend/src/Innovayse.SDK/Plugins/IProvisioningPlugin.cs`
- Create: `backend/src/Innovayse.SDK/Plugins/IPaymentPlugin.cs`
- Create: `backend/src/Innovayse.SDK/Plugins/IRegistrarPlugin.cs`

- [ ] **Step 1: Create `IProvisioningPlugin.cs`**

```csharp
namespace Innovayse.SDK.Plugins;

/// <summary>
/// Marker interface for all provisioning provider plugins.
/// Implement via <see cref="Base.ProvisioningProviderBase"/>.
/// </summary>
public interface IProvisioningPlugin { }
```

- [ ] **Step 2: Create `IPaymentPlugin.cs`**

```csharp
namespace Innovayse.SDK.Plugins;

/// <summary>
/// Marker interface for all payment gateway plugins.
/// Implement via <see cref="Base.PaymentGatewayBase"/>.
/// </summary>
public interface IPaymentPlugin { }
```

- [ ] **Step 3: Create `IRegistrarPlugin.cs`**

```csharp
namespace Innovayse.SDK.Plugins;

/// <summary>
/// Marker interface for all domain registrar plugins.
/// Implement via <see cref="Base.RegistrarProviderBase"/>.
/// </summary>
public interface IRegistrarPlugin { }
```

- [ ] **Step 4: Build**

```bash
cd backend
dotnet build src/Innovayse.SDK/Innovayse.SDK.csproj
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.SDK/
git commit -m "feat(sdk): add IProvisioningPlugin, IPaymentPlugin, IRegistrarPlugin marker interfaces"
```

---

### Task 4: Abstract base classes

**Files:**
- Create: `backend/src/Innovayse.SDK/Base/ProvisioningProviderBase.cs`
- Create: `backend/src/Innovayse.SDK/Base/PaymentGatewayBase.cs`
- Create: `backend/src/Innovayse.SDK/Base/RegistrarProviderBase.cs`

The base classes take `IConfiguration` and `ILogger` via primary constructors and expose `GetConfig(key)`.

- [ ] **Step 1: Create `ProvisioningProviderBase.cs`**

```csharp
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Innovayse.SDK.Base;

/// <summary>
/// Base class for all provisioning provider plugins.
/// Provides <see cref="GetConfig"/> and a structured <see cref="Logger"/>.
/// </summary>
public abstract class ProvisioningProviderBase(
    string pluginId,
    IConfiguration configuration,
    ILogger logger) : IProvisioningPlugin
{
    /// <summary>Gets the structured logger pre-injected for this provider.</summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Reads a configuration value from the Settings table via the standard key
    /// <c>integration:{pluginId}:{key}</c>.
    /// </summary>
    /// <param name="key">The field key as declared in <c>plugin.json</c>.</param>
    /// <returns>The stored value, or <see langword="null"/> if not set.</returns>
    protected string? GetConfig(string key)
        => configuration[$"integration:{pluginId}:{key}"];
}
```

- [ ] **Step 2: Create `PaymentGatewayBase.cs`**

```csharp
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Innovayse.SDK.Base;

/// <summary>
/// Base class for all payment gateway plugins.
/// Provides <see cref="GetConfig"/> and a structured <see cref="Logger"/>.
/// </summary>
public abstract class PaymentGatewayBase(
    string pluginId,
    IConfiguration configuration,
    ILogger logger) : IPaymentPlugin
{
    /// <summary>Gets the structured logger pre-injected for this provider.</summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Reads a configuration value from the Settings table via the standard key
    /// <c>integration:{pluginId}:{key}</c>.
    /// </summary>
    /// <param name="key">The field key as declared in <c>plugin.json</c>.</param>
    /// <returns>The stored value, or <see langword="null"/> if not set.</returns>
    protected string? GetConfig(string key)
        => configuration[$"integration:{pluginId}:{key}"];
}
```

- [ ] **Step 3: Create `RegistrarProviderBase.cs`**

```csharp
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Innovayse.SDK.Base;

/// <summary>
/// Base class for all domain registrar plugins.
/// Provides <see cref="GetConfig"/> and a structured <see cref="Logger"/>.
/// </summary>
public abstract class RegistrarProviderBase(
    string pluginId,
    IConfiguration configuration,
    ILogger logger) : IRegistrarPlugin
{
    /// <summary>Gets the structured logger pre-injected for this provider.</summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Reads a configuration value from the Settings table via the standard key
    /// <c>integration:{pluginId}:{key}</c>.
    /// </summary>
    /// <param name="key">The field key as declared in <c>plugin.json</c>.</param>
    /// <returns>The stored value, or <see langword="null"/> if not set.</returns>
    protected string? GetConfig(string key)
        => configuration[$"integration:{pluginId}:{key}"];
}
```

- [ ] **Step 4: Add `Microsoft.Extensions.Configuration.Abstractions` and `Microsoft.Extensions.Logging.Abstractions` to SDK**

Edit `backend/src/Innovayse.SDK/Innovayse.SDK.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <RootNamespace>Innovayse.SDK</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Build**

```bash
cd backend
dotnet build src/Innovayse.SDK/Innovayse.SDK.csproj
```

Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Innovayse.SDK/
git commit -m "feat(sdk): add ProvisioningProviderBase, PaymentGatewayBase, RegistrarProviderBase"
```

---

### Task 5: Add SDK project reference to Infrastructure and API

**Files:**
- Modify: `backend/src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj`
- Modify: `backend/src/Innovayse.API/Innovayse.API.csproj`

- [ ] **Step 1: Add SDK reference to Infrastructure**

```bash
cd backend
dotnet add src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj reference src/Innovayse.SDK/Innovayse.SDK.csproj
```

- [ ] **Step 2: Add SDK reference to API**

```bash
dotnet add src/Innovayse.API/Innovayse.API.csproj reference src/Innovayse.SDK/Innovayse.SDK.csproj
```

- [ ] **Step 3: Build the full solution**

```bash
cd backend
dotnet build
```

Expected: 0 errors, all projects build.

- [ ] **Step 4: Commit**

```bash
git add backend/src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj
git add backend/src/Innovayse.API/Innovayse.API.csproj
git commit -m "feat(sdk): reference Innovayse.SDK from Infrastructure and API"
```

---

### Task 6: Plugin infrastructure in `Innovayse.Infrastructure`

**Files:**
- Create: `backend/src/Innovayse.Infrastructure/Plugins/PluginManifestReader.cs`
- Create: `backend/src/Innovayse.Infrastructure/Plugins/PluginRegistry.cs`
- Create: `backend/src/Innovayse.Infrastructure/Plugins/PluginLoader.cs`

- [ ] **Step 1: Create `PluginManifestReader.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging;

namespace Innovayse.Infrastructure.Plugins;

/// <summary>
/// Reads and validates <c>plugin.json</c> manifest files from the plugins directory.
/// </summary>
internal sealed class PluginManifestReader(ILogger<PluginManifestReader> logger)
{
    /// <summary>Supported SDK version. Plugins with a different version are skipped.</summary>
    private const string SupportedSdkVersion = "1.0";

    /// <summary>JSON options used to deserialise manifests.</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Attempts to read and validate a <c>plugin.json</c> file.
    /// </summary>
    /// <param name="manifestPath">Absolute path to the <c>plugin.json</c> file.</param>
    /// <returns>
    /// A <see cref="PluginManifest"/> when the file is valid;
    /// <see langword="null"/> when the file is missing, malformed, or incompatible.
    /// </returns>
    public PluginManifest? TryRead(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            logger.LogWarning("Plugin manifest not found: {Path}", manifestPath);
            return null;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions);

            if (manifest is null)
            {
                logger.LogWarning("Plugin manifest deserialised as null: {Path}", manifestPath);
                return null;
            }

            if (manifest.SdkVersion != SupportedSdkVersion)
            {
                logger.LogWarning(
                    "Plugin {Id} requires SDK version {Required}, supported is {Supported} — skipping",
                    manifest.Id, manifest.SdkVersion, SupportedSdkVersion);
                return null;
            }

            return manifest;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read plugin manifest: {Path}", manifestPath);
            return null;
        }
    }
}
```

- [ ] **Step 2: Create `PluginRegistry.cs`**

```csharp
using Innovayse.SDK.Plugins;

namespace Innovayse.Infrastructure.Plugins;

/// <summary>
/// Tracks all plugins that were successfully loaded during application startup.
/// </summary>
public sealed class PluginRegistry
{
    /// <summary>Loaded plugin entries, keyed by plugin id.</summary>
    private readonly Dictionary<string, LoadedPlugin> _plugins = [];

    /// <summary>
    /// Gets all successfully loaded plugins.
    /// </summary>
    public IReadOnlyCollection<LoadedPlugin> LoadedPlugins => _plugins.Values;

    /// <summary>
    /// Registers a loaded plugin.
    /// </summary>
    /// <param name="plugin">The loaded plugin entry to register.</param>
    internal void Register(LoadedPlugin plugin)
        => _plugins[plugin.Manifest.Id] = plugin;

    /// <summary>
    /// Attempts to retrieve a loaded plugin by its identifier.
    /// </summary>
    /// <param name="id">The plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <returns>The <see cref="LoadedPlugin"/>, or <see langword="null"/> if not found.</returns>
    public LoadedPlugin? Find(string id)
        => _plugins.GetValueOrDefault(id);
}

/// <summary>
/// Represents a plugin that was successfully loaded at startup.
/// </summary>
/// <param name="Manifest">The plugin's parsed manifest.</param>
/// <param name="ImplementationType">The concrete provider type loaded from the DLL.</param>
public sealed record LoadedPlugin(PluginManifest Manifest, Type ImplementationType);
```

- [ ] **Step 3: Create `PluginLoader.cs`**

```csharp
using System.Reflection;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Infrastructure.Plugins;

/// <summary>
/// Scans the <c>/plugins/</c> directory at startup, validates manifests, loads assemblies,
/// and registers provider implementations in the DI container.
/// </summary>
public static class PluginLoader
{
    /// <summary>
    /// Discovers all valid plugins under <paramref name="pluginsRoot"/>, loads their assemblies,
    /// and registers them as keyed scoped services in <paramref name="services"/>.
    /// Also registers a singleton <see cref="PluginRegistry"/> populated with every loaded plugin.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="pluginsRoot">Absolute path to the plugins directory.</param>
    /// <param name="loggerFactory">Logger factory used during startup loading.</param>
    public static void DiscoverAndRegister(
        IServiceCollection services,
        string pluginsRoot,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger(nameof(PluginLoader));
        var manifestLogger = loggerFactory.CreateLogger<PluginManifestReader>();
        var reader = new PluginManifestReader(manifestLogger);
        var registry = new PluginRegistry();

        if (!Directory.Exists(pluginsRoot))
        {
            logger.LogInformation("Plugins directory not found at {Path} — no plugins loaded", pluginsRoot);
            services.AddSingleton(registry);
            return;
        }

        foreach (var pluginDir in Directory.GetDirectories(pluginsRoot))
        {
            var manifestPath = Path.Combine(pluginDir, "plugin.json");
            var manifest = reader.TryRead(manifestPath);
            if (manifest is null)
                continue;

            var dllFiles = Directory.GetFiles(pluginDir, "*.dll");
            if (dllFiles.Length == 0)
            {
                logger.LogError("No DLL found for plugin {Id} in {Dir} — skipping", manifest.Id, pluginDir);
                continue;
            }

            Type? pluginType = null;
            foreach (var dll in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    pluginType = assembly.GetType(manifest.EntryPoint);
                    if (pluginType is not null)
                        break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to load assembly {Dll} for plugin {Id}", dll, manifest.Id);
                }
            }

            if (pluginType is null)
            {
                logger.LogError(
                    "Entry point type '{EntryPoint}' not found for plugin {Id} — skipping",
                    manifest.EntryPoint, manifest.Id);
                continue;
            }

            RegisterPluginType(services, manifest, pluginType, logger);
            registry.Register(new LoadedPlugin(manifest, pluginType));
            logger.LogInformation("Plugin {Id} v{Version} loaded", manifest.Id, manifest.Version);
        }

        services.AddSingleton(registry);
    }

    /// <summary>
    /// Registers a plugin type as a keyed scoped service based on its <see cref="PluginType"/>.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="manifest">The plugin manifest.</param>
    /// <param name="pluginType">The concrete type to register.</param>
    /// <param name="logger">Logger for warnings.</param>
    private static void RegisterPluginType(
        IServiceCollection services,
        PluginManifest manifest,
        Type pluginType,
        ILogger logger)
    {
        switch (manifest.Type)
        {
            case PluginType.Provisioning:
                services.AddKeyedScoped<IProvisioningPlugin>(manifest.Id, pluginType);
                break;
            case PluginType.Payment:
                services.AddKeyedScoped<IPaymentPlugin>(manifest.Id, pluginType);
                break;
            case PluginType.Registrar:
                services.AddKeyedScoped<IRegistrarPlugin>(manifest.Id, pluginType);
                break;
            default:
                logger.LogWarning("Unknown plugin type {Type} for plugin {Id} — skipping DI registration",
                    manifest.Type, manifest.Id);
                break;
        }
    }
}
```

- [ ] **Step 4: Build**

```bash
cd backend
dotnet build src/Innovayse.Infrastructure/Innovayse.Infrastructure.csproj
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.Infrastructure/Plugins/
git commit -m "feat(plugins): add PluginManifestReader, PluginRegistry, PluginLoader"
```

---

### Task 7: Wire PluginLoader into `DependencyInjection.cs`

**Files:**
- Modify: `backend/src/Innovayse.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Read current `DependencyInjection.cs`**

Read: `backend/src/Innovayse.Infrastructure/DependencyInjection.cs`

- [ ] **Step 2: Add plugin loading call**

Inside the `AddInfrastructure` extension method, after existing service registrations, add:

```csharp
// Load plugins from the /plugins/ directory next to the application root.
var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
PluginLoader.DiscoverAndRegister(services, pluginsRoot, loggerFactory);
```

The method signature must accept `ILoggerFactory loggerFactory` — add it as a parameter if not already present. The call site in `Program.cs` must pass `app.Services.GetRequiredService<ILoggerFactory>()` or use `LoggerFactory.Create(...)` before the app is built.

> **Note on timing:** `PluginLoader.DiscoverAndRegister` runs during `builder.Services` setup — before `app.Build()`. Use `LoggerFactory.Create` for a bootstrap logger:

In `Program.cs`, before `builder.Services.AddInfrastructure(...)`:

```csharp
using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
builder.Services.AddInfrastructure(builder.Configuration, bootstrapLoggerFactory);
```

Adjust the `AddInfrastructure` signature in `DependencyInjection.cs` accordingly:

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration,
    ILoggerFactory loggerFactory)
```

- [ ] **Step 3: Build full solution**

```bash
cd backend
dotnet build
```

Expected: 0 errors.

- [ ] **Step 4: Run the API and verify startup log**

```bash
./scripts/manage.sh start api
sleep 3
grep -i "plugin" .logs/api.log || echo "No plugin log lines (expected — no /plugins/ dir yet)"
```

Expected: Either "Plugins directory not found" info line, or no error. API starts cleanly.

- [ ] **Step 5: Commit**

```bash
git add backend/src/Innovayse.Infrastructure/DependencyInjection.cs
git add backend/src/Innovayse.API/Program.cs
git commit -m "feat(plugins): wire PluginLoader into startup — DependencyInjection.cs"
```
