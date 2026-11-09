using System.Net;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Endpoints;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// End to end, #757: a plugin that both answers endpoint calls and fulfils invoices sees one client under one
/// reference. The client calls the plugin's route over HTTP and the plugin reads <c>PluginCaller.ClientRef</c>; the
/// dispatcher then tells the same plugin id about that client's paid invoice, and the context carries the same text.
/// A second plugin sees the same client under a different reference on both doors.
/// </summary>
/// <remarks>
/// The endpoint side is the fixture's real app plugin, loaded through the registry and called through the client
/// door. The fulfilment side is a <see cref="StubFulfilmentPlugin"/> resolved under the same plugin id, because the
/// host keys every reference on the plugin id alone and does not yet load one class as both types (the SDK's
/// <c>endpoints</c> capability, #818); once it does, this test keeps its assertions and swaps the resolver for the
/// loaded plugin.
/// </remarks>
public sealed class FulfilmentClientRefTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the app plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing.</summary>
    /// <param name="fixture">The host, the database and the app plugins.</param>
    public FulfilmentClientRefTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>
    /// The reference a paid-invoice context carries equals the one an endpoint call of the same plugin for the same
    /// client receives, and two plugins see that client under two different references on both doors.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PaidContextReference_EqualsTheEndpointCallerReference_PerPlugin()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "fulfilment-ref");
        using var http = _fixture.ClientLike(owner.Client);
        _fixture.Probe.Behaviour = call => PluginReply.Json(new { clientRef = call.Caller.ClientRef });

        var endpointRef = await CallerRefAsync(http, _fixture.PluginId);
        var otherEndpointRef = await CallerRefAsync(http, _fixture.OtherPluginId);

        var fulfilment = new StubFulfilmentPlugin();
        var otherFulfilment = new StubFulfilmentPlugin();
        var resolver = new FakeFulfilmentPluginResolver();
        resolver.Register(_fixture.PluginId, fulfilment);
        resolver.Register(_fixture.OtherPluginId, otherFulfilment);
        using var host = _fixture.Derive(services =>
        {
            services.RemoveAll<IFulfilmentPluginResolver>();
            services.AddSingleton<IFulfilmentPluginResolver>(resolver);
        });

        await SeedPaidInvoiceAsync(host, owner.ClientId);
        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<FulfilmentDispatcher>().RunAsync(CancellationToken.None);
        }

        fulfilment.LastPaid!.ClientId.Should().Be(owner.ClientId);
        fulfilment.LastPaid.ClientRef.Should().Be(endpointRef);
        otherFulfilment.LastPaid!.ClientRef.Should().Be(otherEndpointRef);
        endpointRef.Should().NotBe(otherEndpointRef);
    }

    /// <summary>Calls the plugin's <c>ping</c> route as the client and reads the reference the plugin echoed.</summary>
    /// <param name="http">The signed-in client.</param>
    /// <param name="pluginId">The plugin to call.</param>
    /// <returns>The <c>PluginCaller.ClientRef</c> the plugin saw.</returns>
    private static async Task<string> CallerRefAsync(HttpClient http, string pluginId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/client/plugins/{pluginId}/ping");
        using var response = await http.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var clientRef = json.RootElement.GetProperty("clientRef").GetString();
        clientRef.Should().NotBeNullOrWhiteSpace();
        return clientRef!;
    }

    /// <summary>
    /// Saves a paid invoice for the client and a due paid record for each of the two plugins, as the payment's own
    /// transaction would.
    /// </summary>
    /// <param name="host">The host whose database to write.</param>
    /// <param name="clientId">The signed-in client's id.</param>
    /// <returns>A task that completes when the rows are saved.</returns>
    private async Task SeedPaidInvoiceAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var invoice = FulfilmentInvoiceSeeds.Build(clientId, now);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        db.FulfilmentRecords.Add(FulfilmentRecord.Create(invoice.Id, _fixture.PluginId, FulfilmentKind.Paid, now));
        db.FulfilmentRecords.Add(FulfilmentRecord.Create(invoice.Id, _fixture.OtherPluginId, FulfilmentKind.Paid, now));
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
