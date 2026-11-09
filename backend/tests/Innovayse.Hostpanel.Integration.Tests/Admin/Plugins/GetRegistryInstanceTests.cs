using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>GET /api/admin/plugins/registry-instance</c>: where this installation stands with the registry (not registered, verified,
/// failed with a sentence in the caller's language), carrying nothing secret, and closed to callers who are not administrators.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetRegistryInstanceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the state.</summary>
    private const string Route = "/api/admin/plugins/registry-instance";

    /// <summary>Before any registration the state is NotRegistered and nothing is reported as wrong.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BeforeAnyRegistration_TheStateIsNotRegisteredAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);

        var response = await admin.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("state").GetString().Should().Be("NotRegistered");
        body.RootElement.GetProperty("instanceId").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("errorCode").ValueKind.Should().Be(JsonValueKind.Null);
        registry.Calls.Should().BeEmpty("reading the state never calls the registry");
    }

    /// <summary>After a registration the state is Verified with the id, the address and the moment, and no key of any kind.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AfterARegistration_TheStateIsVerified_AndCarriesNoKeyAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);
        (await admin.PostAsync(Route + "/retry", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var text = await admin.GetStringAsync(Route);

        using var body = JsonDocument.Parse(text);
        body.RootElement.GetProperty("state").GetString().Should().Be("Verified");
        body.RootElement.GetProperty("instanceId").GetGuid().Should().Be(registry.Instances.Single().Id);
        body.RootElement.GetProperty("verifiedAt").ValueKind.Should().Be(JsonValueKind.String);
        text.ToLowerInvariant().Should().NotContain("privatekey").And.NotContain("publickey").And.NotContain("challenge");
    }

    /// <summary>The last failure is worded in the caller's language: English, Russian and Armenian.</summary>
    /// <param name="language">The <c>X-Language</c> value.</param>
    /// <param name="fragment">A fragment of the sentence in that language.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("en", "ClientBaseUrl")]
    [InlineData("ru", "публичный https-адрес")]
    [InlineData("hy", "հանրային https հասցեն")]
    public async Task TheLastFailure_IsWordedInTheCallersLanguageAsync(string language, string fragment)
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry, publicUrl: "http://localhost:3000");
        var admin = await host.AdminClientAsync(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.Add("X-Language", language);

        var response = await admin.SendAsync(request);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("state").GetString().Should().Be("Error");
        body.RootElement.GetProperty("errorCode").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID);
        body.RootElement.GetProperty("message").GetString().Should().Contain(fragment);
    }

    /// <summary>An anonymous caller and a caller who is not an administrator are refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACallerWhoIsNotAnAdministrator_IsRefusedAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var asClient = await PluginsTestSeeds.ClientRoleClientAsync(factory, "get-instance");

        var anonymous = await host.CreateClient().GetAsync(Route);
        var client = await asClient.GetAsync(Route);

        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        client.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }
}
