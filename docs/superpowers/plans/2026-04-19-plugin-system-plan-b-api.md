# Plugin System — Plan B: Plugin Manager API Endpoints

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add four admin API endpoints — list installed plugins, upload/install a ZIP, remove a plugin, and trigger a graceful server restart — following the exact same controller/handler pattern as `IntegrationsController`.

**Architecture:** Application layer gets a new `Admin/Plugins/` module (queries, commands, DTOs). `PluginRegistry` gains an `IPluginRegistry` interface so Application handlers can check load status without referencing Infrastructure directly. `PluginsController` dispatches to Wolverine bus; the restart action calls `IHostApplicationLifetime.StopApplication()` directly (no handler needed).

**Tech Stack:** ASP.NET Core 8, Wolverine, `System.IO.Compression.ZipArchive`, `System.Text.Json`, keyed DI

---

## File Map

```
New — Application layer
  backend/src/Innovayse.Application/Admin/Plugins/Interfaces/IPluginRegistry.cs
  backend/src/Innovayse.Application/Admin/Plugins/DTOs/PluginListItemDto.cs
  backend/src/Innovayse.Application/Admin/Plugins/DTOs/PluginActionResultDto.cs
  backend/src/Innovayse.Application/Admin/Plugins/Queries/ListPlugins/ListPluginsQuery.cs
  backend/src/Innovayse.Application/Admin/Plugins/Queries/ListPlugins/ListPluginsHandler.cs
  backend/src/Innovayse.Application/Admin/Plugins/Commands/InstallPlugin/InstallPluginCommand.cs
  backend/src/Innovayse.Application/Admin/Plugins/Commands/InstallPlugin/InstallPluginHandler.cs
  backend/src/Innovayse.Application/Admin/Plugins/Commands/RemovePlugin/RemovePluginCommand.cs
  backend/src/Innovayse.Application/Admin/Plugins/Commands/RemovePlugin/RemovePluginHandler.cs

New — API layer
  backend/src/Innovayse.API/Admin/PluginsController.cs
  backend/src/Innovayse.API/Admin/Requests/InstallPluginRequest.cs

Modified
  backend/src/Innovayse.Application/Innovayse.Application.csproj   ← add SDK project reference
  backend/src/Innovayse.Infrastructure/Plugins/PluginRegistry.cs    ← implement IPluginRegistry
  backend/src/Innovayse.Infrastructure/DependencyInjection.cs       ← register IPluginRegistry
```

---

### Task 1: Add SDK reference to Application + `IPluginRegistry` interface

**Files:**
- Modify: `backend/src/Innovayse.Application/Innovayse.Application.csproj`
- Create: `backend/src/Innovayse.Application/Admin/Plugins/Interfaces/IPluginRegistry.cs`

- [ ] **Step 1: Add SDK project reference to Application.csproj**

Replace the full content of `backend/src/Innovayse.Application/Innovayse.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Settings inherited from Directory.Build.props -->
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Innovayse.Domain\Innovayse.Domain.csproj" />
    <ProjectReference Include="..\Innovayse.SDK\Innovayse.SDK.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="FluentValidation" Version="11.11.0" />
    <PackageReference Include="Fluid.Core" Version="2.31.0" />
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.0.1" />
    <PackageReference Include="WolverineFx" Version="5.31.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create `IPluginRegistry.cs`**

```csharp
namespace Innovayse.Application.Admin.Plugins.Interfaces;

/// <summary>
/// Provides read access to the set of plugins that were successfully loaded at startup.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>
    /// Determines whether a plugin was successfully loaded during application startup.
    /// </summary>
    /// <param name="pluginId">The plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <returns><see langword="true"/> if the plugin DLL was loaded; otherwise <see langword="false"/>.</returns>
    bool IsLoaded(string pluginId);
}
```

- [ ] **Step 3: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application/Innovayse.Application.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Application/
git commit -m "feat(plugins): add IPluginRegistry to Application + SDK reference"
```

---

### Task 2: `PluginRegistry` implements `IPluginRegistry` + DI registration

**Files:**
- Modify: `backend/src/Innovayse.Infrastructure/Plugins/PluginRegistry.cs`
- Modify: `backend/src/Innovayse.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Make `PluginRegistry` implement `IPluginRegistry`**

Replace the full content of `backend/src/Innovayse.Infrastructure/Plugins/PluginRegistry.cs`:

```csharp
using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.SDK.Plugins;

namespace Innovayse.Infrastructure.Plugins;

/// <summary>
/// Tracks all plugins that were successfully loaded during application startup.
/// </summary>
public sealed class PluginRegistry : IPluginRegistry
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

    /// <inheritdoc/>
    public bool IsLoaded(string pluginId)
        => _plugins.ContainsKey(pluginId);
}

/// <summary>
/// Represents a plugin that was successfully loaded at startup.
/// </summary>
/// <param name="Manifest">The plugin's parsed manifest.</param>
/// <param name="ImplementationType">The concrete provider type loaded from the DLL.</param>
public sealed record LoadedPlugin(PluginManifest Manifest, Type ImplementationType);
```

- [ ] **Step 2: Register `IPluginRegistry` in DI**

In `backend/src/Innovayse.Infrastructure/DependencyInjection.cs`, the `PluginLoader.DiscoverAndRegister` call already registers `PluginRegistry` as a singleton. We need to also expose it as `IPluginRegistry`.

Find this block at the end of `AddInfrastructure`:
```csharp
var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
PluginLoader.DiscoverAndRegister(services, pluginsRoot, loggerFactory);
```

Replace it with:
```csharp
var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
PluginLoader.DiscoverAndRegister(services, pluginsRoot, loggerFactory);
services.AddSingleton<IPluginRegistry>(sp => sp.GetRequiredService<PluginRegistry>());
```

Also add this using at the top of `DependencyInjection.cs` if not already present:
```csharp
using Innovayse.Application.Admin.Plugins.Interfaces;
```

- [ ] **Step 3: Build full solution**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Infrastructure/Plugins/PluginRegistry.cs
git add backend/src/Innovayse.Infrastructure/DependencyInjection.cs
git commit -m "feat(plugins): PluginRegistry implements IPluginRegistry, register in DI"
```

---

### Task 3: DTOs

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Plugins/DTOs/PluginListItemDto.cs`
- Create: `backend/src/Innovayse.Application/Admin/Plugins/DTOs/PluginActionResultDto.cs`

- [ ] **Step 1: Create `PluginListItemDto.cs`**

```csharp
namespace Innovayse.Application.Admin.Plugins.DTOs;

/// <summary>
/// Summary item for one installed plugin, returned by the list endpoint.
/// </summary>
/// <param name="Id">Unique plugin identifier from <c>plugin.json</c>.</param>
/// <param name="Name">Human-readable display name.</param>
/// <param name="Version">Semver version string.</param>
/// <param name="Author">Author name or organisation.</param>
/// <param name="Description">Short description shown in the admin panel.</param>
/// <param name="Type">Plugin type string: provisioning | payment | registrar.</param>
/// <param name="Category">Display category label, e.g. "Hosting / Provisioning".</param>
/// <param name="Color">Hex colour for the plugin logo block.</param>
/// <param name="IsLoaded">Whether the DLL was successfully loaded at startup.</param>
public record PluginListItemDto(
    string Id,
    string Name,
    string Version,
    string Author,
    string Description,
    string Type,
    string Category,
    string Color,
    bool IsLoaded);
```

- [ ] **Step 2: Create `PluginActionResultDto.cs`**

```csharp
namespace Innovayse.Application.Admin.Plugins.DTOs;

/// <summary>
/// Result returned by install and remove plugin operations.
/// The client should display a "restart required" banner when <see cref="RequiresRestart"/> is true.
/// </summary>
/// <param name="RequiresRestart">Always true — plugin changes require a server restart to take effect.</param>
public record PluginActionResultDto(bool RequiresRestart);
```

- [ ] **Step 3: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application/Innovayse.Application.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Application/Admin/Plugins/
git commit -m "feat(plugins): add PluginListItemDto, PluginActionResultDto"
```

---

### Task 4: `ListPluginsQuery` + `ListPluginsHandler`

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Plugins/Queries/ListPlugins/ListPluginsQuery.cs`
- Create: `backend/src/Innovayse.Application/Admin/Plugins/Queries/ListPlugins/ListPluginsHandler.cs`

The handler scans the `/plugins/` directory on disk for installed plugins, reads each `plugin.json`, and cross-references with `IPluginRegistry` to set `IsLoaded`.

- [ ] **Step 1: Create `ListPluginsQuery.cs`**

```csharp
namespace Innovayse.Application.Admin.Plugins.Queries.ListPlugins;

/// <summary>
/// Returns all plugins installed on disk, with their load status.
/// </summary>
public record ListPluginsQuery;
```

- [ ] **Step 2: Create `ListPluginsHandler.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Application.Admin.Plugins.DTOs;
using Innovayse.Application.Admin.Plugins.Interfaces;
using Innovayse.SDK.Plugins;

namespace Innovayse.Application.Admin.Plugins.Queries.ListPlugins;

/// <summary>
/// Handles <see cref="ListPluginsQuery"/> by scanning the plugins directory and
/// cross-referencing loaded status from <see cref="IPluginRegistry"/>.
/// </summary>
/// <param name="registry">Registry of plugins loaded at startup.</param>
public sealed class ListPluginsHandler(IPluginRegistry registry)
{
    /// <summary>JSON options for deserialising plugin manifests.</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Scans the plugins directory and returns a summary list.
    /// </summary>
    /// <param name="query">The list query (no parameters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All installed plugins, loaded or not.</returns>
    public async Task<IReadOnlyList<PluginListItemDto>> HandleAsync(ListPluginsQuery query, CancellationToken ct)
    {
        var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        var result = new List<PluginListItemDto>();

        if (!Directory.Exists(pluginsRoot))
            return result;

        foreach (var dir in Directory.GetDirectories(pluginsRoot))
        {
            var manifestPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                var json = await File.ReadAllTextAsync(manifestPath, ct);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions);
                if (manifest is null)
                    continue;

                result.Add(new PluginListItemDto(
                    manifest.Id,
                    manifest.Name,
                    manifest.Version,
                    manifest.Author,
                    manifest.Description,
                    manifest.Type.ToString().ToLowerInvariant(),
                    manifest.Category,
                    manifest.Color,
                    registry.IsLoaded(manifest.Id)));
            }
            catch
            {
                // Malformed manifest — skip silently (logged at startup by PluginLoader)
            }
        }

        return result;
    }
}
```

- [ ] **Step 3: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application/Innovayse.Application.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Application/Admin/Plugins/
git commit -m "feat(plugins): add ListPluginsQuery and handler"
```

---

### Task 5: `InstallPluginCommand` + `InstallPluginHandler`

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Plugins/Commands/InstallPlugin/InstallPluginCommand.cs`
- Create: `backend/src/Innovayse.Application/Admin/Plugins/Commands/InstallPlugin/InstallPluginHandler.cs`

The handler opens the ZIP, reads and validates `plugin.json`, then extracts all entries to `/plugins/{manifest.Id}/`.

- [ ] **Step 1: Create `InstallPluginCommand.cs`**

```csharp
namespace Innovayse.Application.Admin.Plugins.Commands.InstallPlugin;

/// <summary>
/// Installs a plugin from a ZIP archive uploaded by the admin.
/// </summary>
/// <param name="ZipBytes">Raw bytes of the uploaded ZIP file.</param>
public record InstallPluginCommand(byte[] ZipBytes);
```

- [ ] **Step 2: Create `InstallPluginHandler.cs`**

```csharp
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Application.Admin.Plugins.DTOs;
using Innovayse.SDK.Plugins;

namespace Innovayse.Application.Admin.Plugins.Commands.InstallPlugin;

/// <summary>
/// Handles <see cref="InstallPluginCommand"/> — extracts and validates the ZIP,
/// writes plugin files to the plugins directory, and returns a restart-required result.
/// </summary>
public sealed class InstallPluginHandler
{
    /// <summary>Supported SDK version. Plugins with a different version are rejected.</summary>
    private const string SupportedSdkVersion = "1.0";

    /// <summary>JSON options for deserialising plugin manifests.</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Extracts the ZIP, validates the manifest, and writes plugin files to disk.
    /// </summary>
    /// <param name="command">Command containing the raw ZIP bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Action result with <c>RequiresRestart = true</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>plugin.json</c> is missing, malformed, or incompatible.
    /// </exception>
    public async Task<PluginActionResultDto> HandleAsync(InstallPluginCommand command, CancellationToken ct)
    {
        using var zipStream = new MemoryStream(command.ZipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("plugin.json")
            ?? throw new InvalidOperationException("plugin.json not found in ZIP root.");

        string json;
        using (var reader = new StreamReader(manifestEntry.Open()))
        {
            json = await reader.ReadToEndAsync(ct);
        }

        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _jsonOptions)
            ?? throw new InvalidOperationException("plugin.json could not be parsed.");

        if (string.IsNullOrWhiteSpace(manifest.Id))
            throw new InvalidOperationException("Plugin manifest is missing required field: id.");

        if (manifest.SdkVersion != SupportedSdkVersion)
            throw new InvalidOperationException(
                $"Plugin requires SDK version '{manifest.SdkVersion}', supported is '{SupportedSdkVersion}'.");

        var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        var targetDir = Path.Combine(pluginsRoot, manifest.Id);
        Directory.CreateDirectory(targetDir);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue; // directory entry — skip

            var targetPath = Path.GetFullPath(Path.Combine(targetDir, entry.Name));

            // Guard against zip-slip path traversal
            if (!targetPath.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Zip entry '{entry.Name}' would escape the plugin directory.");

            entry.ExtractToFile(targetPath, overwrite: true);
        }

        return new PluginActionResultDto(RequiresRestart: true);
    }
}
```

- [ ] **Step 3: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application/Innovayse.Application.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Application/Admin/Plugins/
git commit -m "feat(plugins): add InstallPluginCommand and handler"
```

---

### Task 6: `RemovePluginCommand` + `RemovePluginHandler`

**Files:**
- Create: `backend/src/Innovayse.Application/Admin/Plugins/Commands/RemovePlugin/RemovePluginCommand.cs`
- Create: `backend/src/Innovayse.Application/Admin/Plugins/Commands/RemovePlugin/RemovePluginHandler.cs`

- [ ] **Step 1: Create `RemovePluginCommand.cs`**

```csharp
namespace Innovayse.Application.Admin.Plugins.Commands.RemovePlugin;

/// <summary>
/// Removes a plugin by deleting its directory from the plugins folder.
/// </summary>
/// <param name="Id">The plugin identifier matching the subdirectory name.</param>
public record RemovePluginCommand(string Id);
```

- [ ] **Step 2: Create `RemovePluginHandler.cs`**

```csharp
using Innovayse.Application.Admin.Plugins.DTOs;

namespace Innovayse.Application.Admin.Plugins.Commands.RemovePlugin;

/// <summary>
/// Handles <see cref="RemovePluginCommand"/> — deletes the plugin directory from disk.
/// </summary>
public sealed class RemovePluginHandler
{
    /// <summary>
    /// Deletes the plugin directory for the given plugin id.
    /// </summary>
    /// <param name="command">Command containing the plugin id to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="PluginActionResultDto"/> with <c>RequiresRestart = true</c> when the plugin was removed;
    /// <see langword="null"/> when no plugin with that id is installed.
    /// </returns>
    public Task<PluginActionResultDto?> HandleAsync(RemovePluginCommand command, CancellationToken ct)
    {
        var pluginsRoot = Path.Combine(AppContext.BaseDirectory, "plugins");
        var pluginDir = Path.GetFullPath(Path.Combine(pluginsRoot, command.Id));

        // Guard against path traversal
        if (!pluginDir.StartsWith(Path.GetFullPath(pluginsRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Invalid plugin id: '{command.Id}'.");

        if (!Directory.Exists(pluginDir))
            return Task.FromResult<PluginActionResultDto?>(null);

        Directory.Delete(pluginDir, recursive: true);
        return Task.FromResult<PluginActionResultDto?>(new PluginActionResultDto(RequiresRestart: true));
    }
}
```

- [ ] **Step 3: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Application/Innovayse.Application.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Application/Admin/Plugins/
git commit -m "feat(plugins): add RemovePluginCommand and handler"
```

---

### Task 7: `PluginsController` + `InstallPluginRequest`

**Files:**
- Create: `backend/src/Innovayse.API/Admin/Requests/InstallPluginRequest.cs`
- Create: `backend/src/Innovayse.API/Admin/PluginsController.cs`

- [ ] **Step 1: Create `InstallPluginRequest.cs`**

```csharp
namespace Innovayse.API.Admin.Requests;

/// <summary>
/// Multipart form data request for uploading a plugin ZIP archive.
/// </summary>
public sealed class InstallPluginRequest
{
    /// <summary>Gets or sets the uploaded plugin ZIP file.</summary>
    public required IFormFile File { get; init; }
}
```

- [ ] **Step 2: Create `PluginsController.cs`**

```csharp
using Innovayse.Application.Admin.Plugins.Commands.InstallPlugin;
using Innovayse.Application.Admin.Plugins.Commands.RemovePlugin;
using Innovayse.Application.Admin.Plugins.DTOs;
using Innovayse.Application.Admin.Plugins.Queries.ListPlugins;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.API.Admin;

/// <summary>
/// Admin endpoints for managing installable provider plugins.
/// All write operations return <c>requiresRestart: true</c> — the client must trigger a restart
/// via <see cref="RestartAsync"/> before changes take effect.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <param name="lifetime">Host lifetime used to trigger graceful restart.</param>
[ApiController]
[Route("api/admin/plugins")]
[Authorize(Roles = Roles.Admin)]
public sealed class PluginsController(IMessageBus bus, IHostApplicationLifetime lifetime) : ControllerBase
{
    /// <summary>
    /// Returns all installed plugins with their load status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of installed plugin summary items.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PluginListItemDto>>> ListAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<PluginListItemDto>>(
            new ListPluginsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Installs a plugin from an uploaded ZIP archive.
    /// </summary>
    /// <param name="req">Multipart form data containing the ZIP file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>400 on validation error; 200 with <c>requiresRestart: true</c> on success.</returns>
    [HttpPost("install")]
    [RequestSizeLimit(52_428_800)] // 50 MB max
    public async Task<ActionResult<PluginActionResultDto>> InstallAsync(
        [FromForm] InstallPluginRequest req,
        CancellationToken ct)
    {
        if (req.File.Length == 0)
            return BadRequest("Uploaded file is empty.");

        if (!req.File.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .zip files are accepted.");

        byte[] zipBytes;
        using (var ms = new MemoryStream())
        {
            await req.File.CopyToAsync(ms, ct);
            zipBytes = ms.ToArray();
        }

        try
        {
            var result = await bus.InvokeAsync<PluginActionResultDto>(
                new InstallPluginCommand(zipBytes), ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Removes an installed plugin by deleting its files from disk.
    /// </summary>
    /// <param name="id">Plugin identifier as declared in <c>plugin.json</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>404 when not found; 200 with <c>requiresRestart: true</c> on success.</returns>
    [HttpDelete("{id}")]
    public async Task<ActionResult<PluginActionResultDto>> RemoveAsync(string id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<PluginActionResultDto?>(
            new RemovePluginCommand(id), ct);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Triggers a graceful application restart so newly installed or removed plugins take effect.
    /// The process manager (systemd, Docker restart policy) brings the process back up.
    /// </summary>
    /// <returns>200 with a restart message.</returns>
    [HttpPost("restart")]
    public IActionResult RestartAsync()
    {
        lifetime.StopApplication();
        return Ok(new { message = "Server is restarting. Poll /api/health until it responds." });
    }
}
```

- [ ] **Step 3: Build the full solution**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build
```

Expected: 0 errors, all projects build.

- [ ] **Step 4: Start the API and smoke-test the endpoints**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
./scripts/manage.sh start api
sleep 5

# List plugins — should return empty array (no /plugins/ dir yet)
curl -s -X GET http://localhost:5148/api/admin/plugins \
  -H "Authorization: Bearer $(curl -s -X POST http://localhost:5148/api/auth/login \
    -H 'Content-Type: application/json' \
    -d '{"email":"admin@innovayse.com","password":"Admin123!"}' | grep -o '"token":"[^"]*"' | cut -d'"' -f4)"
```

Expected: `[]` (empty array, no error).

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.API/Admin/PluginsController.cs
git add backend/src/Innovayse.API/Admin/Requests/InstallPluginRequest.cs
git commit -m "feat(plugins): add PluginsController — list, install, remove, restart"
```
