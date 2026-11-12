using System.Net;
using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Resilience.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The plugin contract requires a host not to follow redirects on any HTTP client it hands to a plugin: a client that
/// followed one would resend the plugin's credential, and for some handlers its body, to whatever address the reply names.
/// The client a loaded plugin asks for by its id and the factory's unnamed client both return a 3xx reply as it is.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, whose registrations are the application's own.</param>
public sealed class PluginHttpClientRedirectTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A reply that redirects the caller to another stub server.</summary>
    /// <param name="target">The server the redirect points at.</param>
    /// <returns>The scripted reply.</returns>
    private static StubReply RedirectTo(StubHttpServer target)
    {
        return new StubReply(
            HttpStatusCode.Found,
            string.Empty,
            Headers: new Dictionary<string, string> { ["Location"] = new Uri(target.BaseAddress, "second").ToString() });
    }

    /// <summary>Sends a request with a credential to the first server and checks that the redirect was not followed.</summary>
    /// <param name="client">The client under test.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertRedirectIsNotFollowedAsync(HttpClient client)
    {
        await using var first = await StubHttpServer.StartAsync();
        await using var second = await StubHttpServer.StartAsync();
        first.Enqueue(RedirectTo(second));
        second.Enqueue("{}");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(first.BaseAddress, "first")) { Content = new StringContent("{}") };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        first.Requests.Should().ContainSingle();
        second.Requests.Should().BeEmpty();
    }

    /// <summary>The client a loaded plugin asks for by its own id returns a redirect as it is.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheClientOfALoadedPlugin_DoesNotFollowARedirect()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<HttpResilienceOptions>();
        var plugin = PluginActivationTestSeeds.Plugin(PluginActivationTestSeeds.NewId("redirect"), PluginType.Payment, typeof(ConfigurationCapturingPaymentPlugin));
        PluginLoader.RegisterPluginType(services, plugin.Manifest, typeof(ConfigurationCapturingPaymentPlugin), NullLogger.Instance);
        await using var provider = services.BuildServiceProvider();

        await AssertRedirectIsNotFollowedAsync(provider.GetRequiredService<IHttpClientFactory>().CreateClient(plugin.Manifest.Id));
    }

    /// <summary>The unnamed client, which a plugin that names none receives, returns a redirect as it is, in the application's own registration.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheUnnamedClient_DoesNotFollowARedirect()
    {
        await AssertRedirectIsNotFollowedAsync(factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient());
    }
}
