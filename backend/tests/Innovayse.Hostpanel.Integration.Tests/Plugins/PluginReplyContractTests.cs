using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// What the host lets through of a plugin's reply and of a request body, through the whole pipeline: the status of a reply
/// is checked again by the host (not trusted to the SDK factories), and a body above the web server's default limit but
/// within the configured cap reaches the plugin, while one above the cap still answers 413 <c>PLUGIN_REQUEST_TOO_LARGE</c>.
/// </summary>
public sealed class PluginReplyContractTests : IClassFixture<AppPluginFixture>
{
    /// <summary>A body larger than the web server's default limit of 30,000,000 bytes.</summary>
    private const int AboveServerDefault = 31_000_000;

    /// <summary>The cap of the host that serves a body above the server's default: 40 MiB.</summary>
    private const int LargeCap = 40 * 1024 * 1024;

    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing and echoes.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public PluginReplyContractTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>Calls a client area route of the plugin as a new account holder.</summary>
    /// <param name="host">The host to call, or <see langword="null"/> for the fixture's own.</param>
    /// <param name="label">Distinguishes the registered address.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path below the plugin's prefix.</param>
    /// <param name="content">The body, or <see langword="null"/>.</param>
    /// <returns>The response.</returns>
    private async Task<HttpResponseMessage> CallAsync(
        WebApplicationFactory<Program>? host, string label, HttpMethod method, string path, HttpContent? content = null)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, label);
        var http = host is null ? _fixture.ClientLike(owner.Client) : AppPluginFixture.WithBearerOf(host, owner.Client);
        using var request = new HttpRequestMessage(method, $"/api/client/plugins/{_fixture.PluginId}/{path}") { Content = content };
        return await http.SendAsync(request);
    }

    /// <summary>A reply whose status is outside what its shape may have is a plugin fault, whatever type carried it.</summary>
    /// <param name="kind">The shape of the reply.</param>
    /// <param name="status">A status the contract does not allow for it.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(PluginReplyKind.Json, 199)]
    [InlineData(PluginReplyKind.Json, 204)]
    [InlineData(PluginReplyKind.Json, 302)]
    [InlineData(PluginReplyKind.Json, 500)]
    [InlineData(PluginReplyKind.NoContent, 200)]
    [InlineData(PluginReplyKind.NoContent, 205)]
    [InlineData(PluginReplyKind.NoContent, 404)]
    [InlineData(PluginReplyKind.Problem, 0)]
    [InlineData(PluginReplyKind.Problem, 200)]
    [InlineData(PluginReplyKind.Problem, 399)]
    [InlineData(PluginReplyKind.Problem, 401)]
    [InlineData(PluginReplyKind.Problem, 407)]
    [InlineData(PluginReplyKind.Problem, 600)]
    public async Task AStatusOutsideTheContract_IsAPluginFailure(PluginReplyKind kind, int status)
    {
        _fixture.Probe.Behaviour = _ => ForgedPluginReply.Create(kind, status, body: new { ok = true }, code: "some_code", detail: "some text");

        var response = await CallAsync(null, $"forged-{kind}-{status}", HttpMethod.Get, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_FAILED);
    }

    /// <summary>A forged reply that does keep to the contract is passed on with its own status.</summary>
    /// <param name="kind">The shape of the reply.</param>
    /// <param name="status">A status the contract allows for it.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(PluginReplyKind.Json, 203)]
    [InlineData(PluginReplyKind.NoContent, 204)]
    [InlineData(PluginReplyKind.Problem, 400)]
    [InlineData(PluginReplyKind.Problem, 418)]
    [InlineData(PluginReplyKind.Problem, 503)]
    public async Task AStatusInsideTheContract_IsPassedOn(PluginReplyKind kind, int status)
    {
        _fixture.Probe.Behaviour = _ => ForgedPluginReply.Create(kind, status, body: new { ok = true }, code: "some_code", detail: "some text");

        var response = await CallAsync(null, $"allowed-{kind}-{status}", HttpMethod.Get, "ping");

        ((int)response.StatusCode).Should().Be(status);
    }

    /// <summary>A body above the server's default limit, within the configured cap, reaches the plugin whole, declared or chunked.</summary>
    /// <param name="chunked">Whether the body declares no length.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ABodyAboveTheServersDefault_WithinTheCap_ReachesThePlugin(bool chunked)
    {
        using var host = _fixture.Derive(services => services.Configure<PluginApiOptions>(options => options.MaxBodyBytes = LargeCap));
        _fixture.Probe.Behaviour = call => PluginReply.Json(new { length = call.Body.Length });
        var bytes = new byte[AboveServerDefault];
        HttpContent content = chunked ? new UnknownLengthContent(bytes) : new ByteArrayContent(bytes);

        var response = await CallAsync(host, $"large-body-{chunked}", HttpMethod.Post, "echo", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain($"\"length\":{AboveServerDefault}");
    }

    /// <summary>A body above a large cap still answers the plugin's 413, not the server's own.</summary>
    /// <param name="chunked">Whether the body declares no length.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ABodyAboveALargeCap_Answers413WithThePluginCode(bool chunked)
    {
        using var host = _fixture.Derive(services => services.Configure<PluginApiOptions>(options => options.MaxBodyBytes = LargeCap));
        var bytes = new byte[LargeCap + 1];
        HttpContent content = chunked ? new UnknownLengthContent(bytes) : new ByteArrayContent(bytes);

        var response = await CallAsync(host, $"over-large-cap-{chunked}", HttpMethod.Post, "echo", content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE);
        _fixture.Probe.Constructed.Should().Be(0);
    }
}
