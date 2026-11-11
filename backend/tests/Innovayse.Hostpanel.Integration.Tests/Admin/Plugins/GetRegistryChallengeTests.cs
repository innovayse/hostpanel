using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>GET /.well-known/hostpanel-registry/{id}</c>: anonymous, <c>text/plain</c>, never cached, and exactly the challenge the registry
/// gave this installation for that instance id; any other id, and any time before a registration, is a 404.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetRegistryChallengeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route prefix of the challenge.</summary>
    private const string Route = "/.well-known/hostpanel-registry/";

    /// <summary>The challenge is served as plain text with no-store, byte for byte as the registry gave it, with no sign-in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheChallenge_IsServedAsPlainText_NoStore_ExactlyAsGivenAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);
        (await admin.PostAsync("/api/admin/plugins/registry-instance/retry", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var instance = registry.Instances.Single();

        var response = await host.CreateClient().GetAsync(Route + instance.Id.ToString("D"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadAsStringAsync()).Should().Be(instance.Challenge);
    }

    /// <summary>Another id is a 404 with the not found code, and the response does not tell whether an installation is registered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnotherId_IsNotFoundAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var admin = await host.AdminClientAsync(factory);
        (await admin.PostAsync("/api/admin/plugins/registry-instance/retry", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await host.CreateClient().GetAsync(Route + Guid.NewGuid().ToString("D"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_NOT_FOUND);
    }

    /// <summary>Before any registration there is nothing to serve, and a value that is not an id is not a route at all.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BeforeARegistration_ThereIsNothingToServeAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        var client = host.CreateClient();

        var unknown = await client.GetAsync(Route + Guid.NewGuid().ToString("D"));
        var notAnId = await client.GetAsync(Route + "not-an-id");

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notAnId.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
