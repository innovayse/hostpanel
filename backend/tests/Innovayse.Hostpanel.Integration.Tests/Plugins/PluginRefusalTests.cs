using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The refusals of the client plugin door, against Postgres and the test-only plugins of <see cref="AppPluginFixture"/>:
/// every reason a call is refused as "no such route" answers the very same bytes, and none of them reads the plugin's
/// switch or settings, so neither the body nor the time it takes tells an installed plugin from an absent one.
/// </summary>
public sealed class PluginRefusalTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing and echoes.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public PluginRefusalTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>The raw answer of a refusal: the status, the content type and the body exactly as sent.</summary>
    /// <param name="Status">The status code.</param>
    /// <param name="ContentType">The Content-Type header, parameters included.</param>
    /// <param name="Body">The raw response body.</param>
    private sealed record Answer(HttpStatusCode Status, string? ContentType, string Body);

    /// <summary>Sends a request to a client area plugin route and keeps the raw answer.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The path below the plugin's prefix.</param>
    /// <returns>The answer.</returns>
    private static async Task<Answer> AnswerOfAsync(HttpClient http, HttpMethod method, string pluginId, string path)
    {
        using var request = new HttpRequestMessage(method, $"/api/client/plugins/{pluginId}/{path}");
        using var response = await http.SendAsync(request);
        return new Answer(
            response.StatusCode, response.Content.Headers.ContentType?.ToString(), await response.Content.ReadAsStringAsync());
    }

    /// <summary>Makes a client of a host for a sub-user who holds only the domains permission.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <param name="label">Distinguishes the created addresses.</param>
    /// <returns>The sub-user's client, to dispose when the test ends.</returns>
    private async Task<HttpClient> SubUserAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, string label)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, label);
        var linked = await ClientsTestSeeds.LinkedToAsync(_fixture.Factory, $"{label}-sub", owner.ClientId, ClientPermission.ViewDomains);
        return AppPluginFixture.WithBearerOf(host, linked);
    }

    /// <summary>
    /// An unknown plugin, a disabled one, an undeclared route, a wrong method and a missing permission answer with the same
    /// status, the same Content-Type and the same raw body, byte for byte.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EveryRefusalReason_AnswersTheSameBytes()
    {
        using var sub = await SubUserAsync(_fixture.Host, "refusal-bytes");

        var unknown = await AnswerOfAsync(sub, HttpMethod.Get, "no-such-plugin", "ping");
        var disabled = await AnswerOfAsync(sub, HttpMethod.Get, _fixture.DisabledPluginId, "ping");
        var undeclared = await AnswerOfAsync(sub, HttpMethod.Get, _fixture.PluginId, "nothing/here");
        var wrongMethod = await AnswerOfAsync(sub, HttpMethod.Delete, _fixture.PluginId, "ping");
        var permission = await AnswerOfAsync(sub, HttpMethod.Get, _fixture.PluginId, "invoices/1");

        unknown.Status.Should().Be(HttpStatusCode.NotFound);
        unknown.ContentType.Should().NotBeNullOrEmpty();
        unknown.Body.Should().Contain(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        foreach (var refusal in new[] { disabled, undeclared, wrongMethod, permission })
        {
            refusal.Status.Should().Be(unknown.Status);
            refusal.ContentType.Should().Be(unknown.ContentType);
            refusal.Body.Should().Be(unknown.Body);
        }

        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>
    /// A refusal for an unknown plugin, an undeclared route, a wrong method or a missing permission never reaches the step
    /// that reads the plugin's switch and settings, while a served call does (the rate limit filter and the use case each ask the gate): the order is match, permission, then the read.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARefusalBeforeTheSwitch_NeverPreparesThePlugin()
    {
        using var host = _fixture.Derive(services =>
        {
            services.RemoveAll<IPluginEndpointResolver>();
            services.AddScoped<PluginEndpointResolver>();
            services.AddSingleton<PluginPrepareCounter>();
            services.AddScoped<IPluginEndpointResolver, CountingPluginEndpointResolver>();
        });
        var prepared = host.Services.GetRequiredService<PluginPrepareCounter>();
        using var sub = await SubUserAsync(host, "refusal-no-prepare");

        await AnswerOfAsync(sub, HttpMethod.Get, "no-such-plugin", "ping");
        await AnswerOfAsync(sub, HttpMethod.Get, _fixture.PluginId, "nothing/here");
        await AnswerOfAsync(sub, HttpMethod.Delete, _fixture.PluginId, "ping");
        await AnswerOfAsync(sub, HttpMethod.Get, _fixture.PluginId, "invoices/1");

        prepared.Count.Should().Be(0);

        var served = await AnswerOfAsync(sub, HttpMethod.Get, _fixture.PluginId, "ping");
        served.Status.Should().Be(HttpStatusCode.OK, served.Body);
        prepared.Count.Should().BeGreaterThan(0);
    }
}
