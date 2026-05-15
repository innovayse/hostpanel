# CWP Plugin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first official Innovayse plugin — a CentOS Web Panel (CWP) provisioning provider — as a standalone C# class library that compiles to a ZIP installable via the Plugin Manager.

**Architecture:** A new `Innovayse.Providers.CWP` class library extends `ProvisioningProviderBase` from the SDK. It wraps the CWP REST API (`POST /v1/account`) via `HttpClient`. A `pack.sh` script compiles the DLL and zips it with `plugin.json` ready for upload. No changes to the main backend.

**Tech Stack:** C# 12, .NET 8, `Innovayse.SDK` (project reference for dev), `System.Net.Http`, xUnit + Moq for tests

---

## File Map

```
plugins/innovayse-cwp/                     ← runtime location after install (generated)
  plugin.json
  Innovayse.Providers.CWP.dll

backend/src/Innovayse.Providers.CWP/       ← source project
  Innovayse.Providers.CWP.csproj
  plugin.json
  CwpApiClient.cs                          ← HTTP wrapper for CWP REST API
  CwpProvisioningProvider.cs               ← entry point — extends ProvisioningProviderBase
  Models/
    CwpAccountRequest.cs                   ← request model for account operations
    CwpApiResponse.cs                      ← parsed CWP API response

backend/tests/Innovayse.CWP.Tests/
  Innovayse.CWP.Tests.csproj
  CwpApiClientTests.cs                     ← unit tests with mock HttpMessageHandler

scripts/pack-cwp-plugin.sh                 ← build + zip script
```

---

### Task 1: Project scaffold + plugin.json

**Files:**
- Create: `backend/src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj`
- Create: `backend/src/Innovayse.Providers.CWP/plugin.json`

- [ ] **Step 1: Create the `.csproj`**

Create `backend/src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <RootNamespace>Innovayse.Providers.CWP</RootNamespace>
    <AssemblyName>Innovayse.Providers.CWP</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Innovayse.SDK\Innovayse.SDK.csproj" />
  </ItemGroup>

  <ItemGroup>
    <None Include="plugin.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create `plugin.json`**

Create `backend/src/Innovayse.Providers.CWP/plugin.json`:

```json
{
  "id": "innovayse-cwp",
  "name": "CentOS Web Panel",
  "version": "1.0.0",
  "author": "Innovayse",
  "description": "Provisioning provider for CentOS Web Panel (CWP).",
  "type": "Provisioning",
  "category": "Hosting / Provisioning",
  "color": "#1a73e8",
  "entryPoint": "Innovayse.Providers.CWP.CwpProvisioningProvider",
  "sdkVersion": "1.0",
  "fields": [
    { "key": "host",    "label": "CWP Host",  "type": "text",   "required": true  },
    { "key": "port",    "label": "Port",       "type": "text",   "required": false },
    { "key": "api_key", "label": "API Key",    "type": "secret", "required": true  }
  ]
}
```

- [ ] **Step 3: Add project to solution**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet sln add src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 4: Verify build (empty project)**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Providers.CWP/
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Providers.CWP/
git add backend/Innovayse.sln
git commit -m "feat(cwp): scaffold CWP plugin project + plugin.json"
```

---

### Task 2: Models — `CwpAccountRequest` + `CwpApiResponse`

**Files:**
- Create: `backend/src/Innovayse.Providers.CWP/Models/CwpAccountRequest.cs`
- Create: `backend/src/Innovayse.Providers.CWP/Models/CwpApiResponse.cs`

CWP REST API overview:
- Endpoint: `POST https://{host}:{port}/v1/account`
- Auth: `key` field in form body
- Action field selects operation: `add`, `susp`, `unsp`, `del`, `detail`
- Response: JSON `{ "status": "OK" | "Error", "msj": "..." }`

- [ ] **Step 1: Create `CwpAccountRequest.cs`**

```csharp
namespace Innovayse.Providers.CWP.Models;

/// <summary>Parameters for a CWP account operation sent as form fields.</summary>
internal sealed class CwpAccountRequest
{
    /// <summary>Gets or initializes the CWP API key for authentication.</summary>
    public required string Key { get; init; }

    /// <summary>Gets or initializes the action to perform (add, susp, unsp, del, detail).</summary>
    public required string Action { get; init; }

    /// <summary>Gets or initializes the primary domain for the account.</summary>
    public string? Domain { get; init; }

    /// <summary>Gets or initializes the cPanel username for the account.</summary>
    public string? User { get; init; }

    /// <summary>Gets or initializes the account password (used only on creation).</summary>
    public string? Pass { get; init; }

    /// <summary>Gets or initializes the hosting package name assigned to the account.</summary>
    public string? Package { get; init; }

    /// <summary>Gets or initializes the contact email address for the account.</summary>
    public string? Email { get; init; }

    /// <summary>Converts this request into URL-encoded form content for the HTTP POST body.</summary>
    /// <returns>Form content ready to send as the HTTP request body.</returns>
    public FormUrlEncodedContent ToFormContent()
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("key",    Key),
            new("action", Action),
        };

        if (Domain  is not null) fields.Add(new("domain",  Domain));
        if (User    is not null) fields.Add(new("user",    User));
        if (Pass    is not null) fields.Add(new("pass",    Pass));
        if (Package is not null) fields.Add(new("package", Package));
        if (Email   is not null) fields.Add(new("email",   Email));

        return new FormUrlEncodedContent(fields);
    }
}
```

- [ ] **Step 2: Create `CwpApiResponse.cs`**

```csharp
namespace Innovayse.Providers.CWP.Models;

using System.Text.Json.Serialization;

/// <summary>Parsed response from the CWP REST API.</summary>
internal sealed class CwpApiResponse
{
    /// <summary>Gets or initializes the result status — "OK" on success, "Error" on failure.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Gets or initializes the human-readable message returned by CWP.</summary>
    [JsonPropertyName("msj")]
    public string Message { get; init; } = string.Empty;

    /// <summary>Returns true when <see cref="Status"/> equals "OK" (case-insensitive).</summary>
    public bool IsSuccess =>
        string.Equals(Status, "OK", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 3: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Providers.CWP/
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Providers.CWP/Models/
git commit -m "feat(cwp): add CwpAccountRequest and CwpApiResponse models"
```

---

### Task 3: `CwpApiClient` — HTTP wrapper

**Files:**
- Create: `backend/src/Innovayse.Providers.CWP/CwpApiClient.cs`

CWP API endpoint pattern: `POST https://{host}:{port}/v1/account`
Default CWP API port: `2031`

- [ ] **Step 1: Create `CwpApiClient.cs`**

```csharp
namespace Innovayse.Providers.CWP;

using System.Net.Http.Json;
using Innovayse.Providers.CWP.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// HTTP client wrapper for the CentOS Web Panel (CWP) REST API.
/// Sends form-encoded POST requests to the /v1/account endpoint.
/// </summary>
internal sealed class CwpApiClient
{
    /// <summary>Default CWP API port when not specified in configuration.</summary>
    private const int DefaultPort = 2031;

    /// <summary>The underlying HTTP client used for all requests.</summary>
    private readonly HttpClient _http;

    /// <summary>Structured logger for request and response events.</summary>
    private readonly ILogger _logger;

    /// <summary>Base URL for the CWP API, e.g. "https://cwp.example.com:2031".</summary>
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new <see cref="CwpApiClient"/> for the given CWP host.
    /// </summary>
    /// <param name="http">Pre-configured HTTP client. Caller owns lifetime.</param>
    /// <param name="host">CWP server hostname or IP address.</param>
    /// <param name="port">CWP API port. Defaults to 2031 when null or empty.</param>
    /// <param name="logger">Logger for request tracing and error reporting.</param>
    public CwpApiClient(HttpClient http, string host, string? port, ILogger logger)
    {
        _http    = http;
        _logger  = logger;
        var resolvedPort = int.TryParse(port, out var p) ? p : DefaultPort;
        _baseUrl = $"https://{host}:{resolvedPort}";
    }

    /// <summary>
    /// Creates a new hosting account on the CWP server.
    /// </summary>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="domain">Primary domain for the account.</param>
    /// <param name="username">cPanel username.</param>
    /// <param name="password">Account password.</param>
    /// <param name="package">Hosting package name.</param>
    /// <param name="email">Contact email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<CwpApiResponse> CreateAccountAsync(
        string apiKey,
        string domain,
        string username,
        string password,
        string package,
        string email,
        CancellationToken ct) =>
        SendAsync(new CwpAccountRequest
        {
            Key     = apiKey,
            Action  = "add",
            Domain  = domain,
            User    = username,
            Pass    = password,
            Package = package,
            Email   = email,
        }, ct);

    /// <summary>
    /// Suspends an existing hosting account.
    /// </summary>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="username">cPanel username of the account to suspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<CwpApiResponse> SuspendAccountAsync(
        string apiKey,
        string username,
        CancellationToken ct) =>
        SendAsync(new CwpAccountRequest
        {
            Key    = apiKey,
            Action = "susp",
            User   = username,
        }, ct);

    /// <summary>
    /// Unsuspends a previously suspended hosting account.
    /// </summary>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="username">cPanel username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<CwpApiResponse> UnsuspendAccountAsync(
        string apiKey,
        string username,
        CancellationToken ct) =>
        SendAsync(new CwpAccountRequest
        {
            Key    = apiKey,
            Action = "unsp",
            User   = username,
        }, ct);

    /// <summary>
    /// Terminates (permanently deletes) a hosting account.
    /// </summary>
    /// <param name="apiKey">CWP API key for authentication.</param>
    /// <param name="username">cPanel username of the account to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The CWP API response indicating success or failure.</returns>
    public Task<CwpApiResponse> TerminateAccountAsync(
        string apiKey,
        string username,
        CancellationToken ct) =>
        SendAsync(new CwpAccountRequest
        {
            Key    = apiKey,
            Action = "del",
            User   = username,
        }, ct);

    /// <summary>
    /// Sends a form-encoded POST to the CWP /v1/account endpoint and parses the JSON response.
    /// </summary>
    /// <param name="request">The request parameters to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed <see cref="CwpApiResponse"/>.</returns>
    /// <exception cref="HttpRequestException">Thrown when the HTTP request fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the response body cannot be parsed.</exception>
    private async Task<CwpApiResponse> SendAsync(CwpAccountRequest request, CancellationToken ct)
    {
        var url = $"{_baseUrl}/v1/account";
        _logger.LogDebug("CWP API request: action={Action} url={Url}", request.Action, url);

        using var content  = request.ToFormContent();
        using var response = await _http.PostAsync(url, content, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CwpApiResponse>(ct)
            ?? throw new InvalidOperationException("CWP API returned an empty response body.");

        _logger.LogDebug("CWP API response: status={Status} message={Message}",
            result.Status, result.Message);

        return result;
    }
}
```

- [ ] **Step 2: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Providers.CWP/
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Providers.CWP/CwpApiClient.cs
git commit -m "feat(cwp): add CwpApiClient — HTTP wrapper for CWP REST API"
```

---

### Task 4: `CwpProvisioningProvider` — plugin entry point

**Files:**
- Create: `backend/src/Innovayse.Providers.CWP/CwpProvisioningProvider.cs`

`ProvisioningProviderBase` primary constructor signature (from SDK):
```csharp
ProvisioningProviderBase(string pluginId, IConfiguration configuration, ILogger logger)
```
`GetConfig(key)` reads `integration:{pluginId}:{key}` from `IConfiguration`.

- [ ] **Step 1: Create `CwpProvisioningProvider.cs`**

```csharp
namespace Innovayse.Providers.CWP;

using Innovayse.SDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Provisioning provider plugin for CentOS Web Panel (CWP).
/// Registered as a keyed <c>IProvisioningPlugin</c> under the key "innovayse-cwp".
/// </summary>
public sealed class CwpProvisioningProvider : ProvisioningProviderBase
{
    /// <summary>Plugin identifier — matches the "id" field in plugin.json.</summary>
    private const string PluginId = "innovayse-cwp";

    /// <summary>Pre-configured CWP API client created from stored settings.</summary>
    private readonly CwpApiClient _client;

    /// <summary>CWP API key read from settings at construction time.</summary>
    private readonly string _apiKey;

    /// <summary>
    /// Initializes the provider by reading host, port, and api_key from settings
    /// and constructing a <see cref="CwpApiClient"/>.
    /// </summary>
    /// <param name="configuration">Application configuration — provides plugin settings.</param>
    /// <param name="logger">Logger for structured output.</param>
    public CwpProvisioningProvider(IConfiguration configuration, ILogger<CwpProvisioningProvider> logger)
        : base(PluginId, configuration, logger)
    {
        var host   = GetConfig("host")    ?? throw new InvalidOperationException("CWP: 'host' setting is required.");
        var port   = GetConfig("port");
        var apiKey = GetConfig("api_key") ?? throw new InvalidOperationException("CWP: 'api_key' setting is required.");

        _apiKey = apiKey;
        _client = new CwpApiClient(new HttpClient(), host, port, logger);
    }

    /// <summary>
    /// Creates a new hosting account on the CWP server.
    /// </summary>
    /// <param name="domain">Primary domain for the account.</param>
    /// <param name="username">cPanel username for the account.</param>
    /// <param name="password">Account password.</param>
    /// <param name="package">Hosting package name assigned to the account.</param>
    /// <param name="email">Contact email address for the account owner.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when CWP reports success; false otherwise.</returns>
    public async Task<bool> CreateAccountAsync(
        string domain,
        string username,
        string password,
        string package,
        string email,
        CancellationToken ct)
    {
        var response = await _client.CreateAccountAsync(_apiKey, domain, username, password, package, email, ct);

        if (!response.IsSuccess)
            Logger.LogWarning("CWP CreateAccount failed: {Message}", response.Message);

        return response.IsSuccess;
    }

    /// <summary>
    /// Suspends a hosting account on the CWP server.
    /// </summary>
    /// <param name="username">cPanel username of the account to suspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when CWP reports success; false otherwise.</returns>
    public async Task<bool> SuspendAccountAsync(string username, CancellationToken ct)
    {
        var response = await _client.SuspendAccountAsync(_apiKey, username, ct);

        if (!response.IsSuccess)
            Logger.LogWarning("CWP SuspendAccount failed for {User}: {Message}", username, response.Message);

        return response.IsSuccess;
    }

    /// <summary>
    /// Unsuspends a hosting account on the CWP server.
    /// </summary>
    /// <param name="username">cPanel username of the account to unsuspend.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when CWP reports success; false otherwise.</returns>
    public async Task<bool> UnsuspendAccountAsync(string username, CancellationToken ct)
    {
        var response = await _client.UnsuspendAccountAsync(_apiKey, username, ct);

        if (!response.IsSuccess)
            Logger.LogWarning("CWP UnsuspendAccount failed for {User}: {Message}", username, response.Message);

        return response.IsSuccess;
    }

    /// <summary>
    /// Permanently terminates a hosting account on the CWP server.
    /// </summary>
    /// <param name="username">cPanel username of the account to terminate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when CWP reports success; false otherwise.</returns>
    public async Task<bool> TerminateAccountAsync(string username, CancellationToken ct)
    {
        var response = await _client.TerminateAccountAsync(_apiKey, username, ct);

        if (!response.IsSuccess)
            Logger.LogWarning("CWP TerminateAccount failed for {User}: {Message}", username, response.Message);

        return response.IsSuccess;
    }
}
```

- [ ] **Step 2: Build**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet build src/Innovayse.Providers.CWP/
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/src/Innovayse.Providers.CWP/CwpProvisioningProvider.cs
git commit -m "feat(cwp): add CwpProvisioningProvider — plugin entry point"
```

---

### Task 5: Unit tests for `CwpApiClient`

**Files:**
- Create: `backend/tests/Innovayse.CWP.Tests/Innovayse.CWP.Tests.csproj`
- Create: `backend/tests/Innovayse.CWP.Tests/CwpApiClientTests.cs`

- [ ] **Step 1: Create test project**

Create `backend/tests/Innovayse.CWP.Tests/Innovayse.CWP.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Innovayse.Providers.CWP\Innovayse.Providers.CWP.csproj" />
  </ItemGroup>
</Project>
```

Add to solution:
```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet sln add tests/Innovayse.CWP.Tests/Innovayse.CWP.Tests.csproj
```

- [ ] **Step 2: Write `CwpApiClientTests.cs`**

Note: `CwpApiClient` and its models are `internal`. To test them, add `[assembly: InternalsVisibleTo("Innovayse.CWP.Tests")]` to `Innovayse.Providers.CWP.csproj` — see Step 3.

```csharp
namespace Innovayse.CWP.Tests;

using System.Net;
using System.Text.Json;
using Innovayse.Providers.CWP;
using Innovayse.Providers.CWP.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using FluentAssertions;

/// <summary>Unit tests for CwpApiClient using a mocked HttpMessageHandler.</summary>
public sealed class CwpApiClientTests
{
    /// <summary>
    /// Creates a CwpApiClient wired to a mock handler that returns the given JSON body.
    /// </summary>
    /// <param name="responseBody">JSON string to return as the HTTP response body.</param>
    /// <param name="statusCode">HTTP status code to return. Default 200 OK.</param>
    /// <returns>Configured <see cref="CwpApiClient"/> pointing to "cwp.test:2031".</returns>
    private static CwpApiClient BuildClient(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
            });

        var http = new HttpClient(mockHandler.Object);
        return new CwpApiClient(http, "cwp.test", null, NullLogger.Instance);
    }

    /// <summary>
    /// CreateAccountAsync should return true when CWP responds with status "OK".
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsTrue_WhenCwpRespondsOk()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account created." }""");

        var result = await client.CreateAccountAsync(
            "test-key", "example.com", "testuser", "pass123", "default", "test@example.com",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Account created.");
    }

    /// <summary>
    /// CreateAccountAsync should return false when CWP responds with status "Error".
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_ReturnsFalse_WhenCwpRespondsError()
    {
        var client = BuildClient("""{ "status": "Error", "msj": "User already exists." }""");

        var result = await client.CreateAccountAsync(
            "test-key", "example.com", "testuser", "pass123", "default", "test@example.com",
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Be("User already exists.");
    }

    /// <summary>
    /// SuspendAccountAsync should send action=susp in the form body.
    /// </summary>
    [Fact]
    public async Task SuspendAccountAsync_ReturnsTrue_WhenCwpRespondsOk()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account suspended." }""");

        var result = await client.SuspendAccountAsync("test-key", "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// UnsuspendAccountAsync should return true on OK response.
    /// </summary>
    [Fact]
    public async Task UnsuspendAccountAsync_ReturnsTrue_WhenCwpRespondsOk()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account unsuspended." }""");

        var result = await client.UnsuspendAccountAsync("test-key", "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// TerminateAccountAsync should return true on OK response.
    /// </summary>
    [Fact]
    public async Task TerminateAccountAsync_ReturnsTrue_WhenCwpRespondsOk()
    {
        var client = BuildClient("""{ "status": "OK", "msj": "Account deleted." }""");

        var result = await client.TerminateAccountAsync("test-key", "testuser", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// SendAsync should throw HttpRequestException when the server returns 500.
    /// </summary>
    [Fact]
    public async Task CreateAccountAsync_Throws_WhenServerReturns500()
    {
        var client = BuildClient("Internal Server Error", HttpStatusCode.InternalServerError);

        var act = () => client.CreateAccountAsync(
            "test-key", "example.com", "testuser", "pass123", "default", "test@example.com",
            CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
```

- [ ] **Step 3: Add `InternalsVisibleTo` to CWP csproj**

In `backend/src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj`, add inside `<Project>`:

```xml
  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
      <_Parameter1>Innovayse.CWP.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
```

- [ ] **Step 4: Run tests (expect all pass)**

```bash
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet test tests/Innovayse.CWP.Tests/ -v normal
```

Expected:
```
Passed!  - Failed: 0, Passed: 5, Skipped: 0
```

- [ ] **Step 5: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add backend/tests/Innovayse.CWP.Tests/
git add backend/src/Innovayse.Providers.CWP/Innovayse.Providers.CWP.csproj
git add backend/Innovayse.sln
git commit -m "test(cwp): add unit tests for CwpApiClient"
```

---

### Task 6: Pack script — build + ZIP for Plugin Manager upload

**Files:**
- Create: `scripts/pack-cwp-plugin.sh`

This script publishes the CWP plugin as a self-contained ZIP ready for upload to Plugin Manager.

- [ ] **Step 1: Create `scripts/pack-cwp-plugin.sh`**

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$SCRIPT_DIR/.."
SRC="$ROOT/backend/src/Innovayse.Providers.CWP"
OUT="$ROOT/dist/innovayse-cwp"
ZIP="$ROOT/dist/innovayse-cwp.zip"

echo "Building CWP plugin..."
dotnet publish "$SRC/Innovayse.Providers.CWP.csproj" \
  -c Release \
  -o "$OUT" \
  --no-self-contained \
  -f net8.0

echo "Copying plugin.json..."
cp "$SRC/plugin.json" "$OUT/plugin.json"

echo "Removing unnecessary files..."
rm -f "$OUT"/*.pdb
rm -f "$OUT"/*.xml
# Keep only the plugin DLL + SDK DLL + plugin.json
find "$OUT" -name "Microsoft.*.dll" -delete
find "$OUT" -name "System.*.dll" -delete

echo "Creating ZIP at $ZIP..."
rm -f "$ZIP"
cd "$(dirname "$OUT")"
zip -r "$(basename "$ZIP")" "$(basename "$OUT")"

echo "Done: $ZIP"
```

Make executable and run:
```bash
chmod +x /c/Users/Dell/Desktop/www/innovayse/scripts/pack-cwp-plugin.sh
cd /c/Users/Dell/Desktop/www/innovayse
./scripts/pack-cwp-plugin.sh
```

Expected: `Done: .../dist/innovayse-cwp.zip`

- [ ] **Step 2: Verify ZIP contents**

```bash
unzip -l /c/Users/Dell/Desktop/www/innovayse/dist/innovayse-cwp.zip
```

Expected output includes:
```
  innovayse-cwp/Innovayse.Providers.CWP.dll
  innovayse-cwp/Innovayse.SDK.dll
  innovayse-cwp/plugin.json
```

- [ ] **Step 3: Commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add scripts/pack-cwp-plugin.sh
echo "dist/" >> .gitignore
git add .gitignore
git commit -m "feat(cwp): add pack-cwp-plugin.sh build script"
```

---

### Task 7: Install via Plugin Manager + smoke test

**Pre-condition:** Backend running (`dotnet run --project backend/src/Innovayse.API`) and admin panel running.

- [ ] **Step 1: Upload the ZIP**

1. Open `http://localhost:5173/plugins`
2. Click "Choose file" → select `dist/innovayse-cwp.zip`
3. Click "Install Plugin"
4. Banner: "Restart required to apply plugin changes" appears

- [ ] **Step 2: Restart backend**

Click "Restart Server" in the Plugin Manager banner, OR:
```bash
# Kill the running backend and restart
cd /c/Users/Dell/Desktop/www/innovayse/backend
dotnet run --project src/Innovayse.API
```

- [ ] **Step 3: Verify plugin loaded**

```bash
curl -s http://localhost:5148/api/admin/plugins \
  -H "Authorization: Bearer <token>" | jq .
```

Expected response includes:
```json
[{
  "id": "innovayse-cwp",
  "name": "CentOS Web Panel",
  "version": "1.0.0",
  "isLoaded": true
}]
```

- [ ] **Step 4: Verify in Plugin Manager UI**

Refresh `http://localhost:5173/plugins` — CWP plugin card should appear with green "Loaded" badge.

- [ ] **Step 5: Final commit**

```bash
cd /c/Users/Dell/Desktop/www/innovayse
git add .
git commit -m "feat(cwp): CWP plugin complete — scaffold, client, provider, tests, pack script"
```
