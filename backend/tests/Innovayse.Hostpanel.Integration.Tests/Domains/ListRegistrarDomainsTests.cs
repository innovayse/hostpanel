using System.Net;
using System.Net.Http.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Registrar;
using Hostpanel.Sdk.Plugins.Results;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.ListRegistrarDomains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="ListRegistrarDomainsQuery"/> through <c>GET /api/domains/registrar/{module}</c>: the domains a registrar plugin
/// reports for its account, one page at a time, each marked with whether the panel already has it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListRegistrarDomainsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A name unique to one test run, so a stored domain never collides with another test's.</summary>
    /// <param name="label">A short label for readable failures.</param>
    /// <returns>A domain name.</returns>
    private static string UniqueName(string label) => $"{label}-{Guid.NewGuid():N}"[..24] + ".example";

    /// <summary>A listed domain the panel already stores is marked in the panel, whoever owns it, and one it does not store is not; the expiry and the next cursor come through.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_MarksTheDomainsThePanelAlreadyHasAsync()
    {
        var stored = UniqueName("stored");
        var fresh = UniqueName("fresh");
        var expiry = new DateTimeOffset(2031, 5, 6, 7, 8, 9, TimeSpan.Zero);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await DomainsTestSeeds.SeedClientAsync(context);
            await DomainsTestSeeds.SeedDomainAsync(context, clientId, stored, DomainStatus.Active, TestRegistrarModules.First);
        }

        var plugin = RegistrarImportTestSeeds.PluginListing(_ => new RegistrarDomainPage(
            [
                RegistrarImportTestSeeds.Listed(stored.ToUpperInvariant(), "REF-1", expiry),
                RegistrarImportTestSeeds.Listed(fresh, "REF-2", null),
            ],
            "page-2"));
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);

        var response = await admin.GetAsync($"/api/domains/registrar/{TestRegistrarModules.First}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<RegistrarDomainListDto>();
        page!.Domains.Should().Equal(
            new RegistrarDomainDto(stored.ToUpperInvariant(), expiry, true),
            new RegistrarDomainDto(fresh, null, false));
        page.NextCursor.Should().Be("page-2");
    }

    /// <summary>The cursor in the query reaches the registrar unchanged, so the next page is the one it names.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_SendsTheCursorToTheRegistrarAsync()
    {
        var first = UniqueName("first");
        var second = UniqueName("second");
        var plugin = RegistrarImportTestSeeds.PluginListing(cursor => cursor is null
            ? new RegistrarDomainPage([RegistrarImportTestSeeds.Listed(first, "REF-1", null)], "c-2")
            : new RegistrarDomainPage([RegistrarImportTestSeeds.Listed(second, "REF-2", null)], null));
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);

        var page = await admin.GetFromJsonAsync<RegistrarDomainListDto>($"/api/domains/registrar/{TestRegistrarModules.First}?cursor=c-2");

        page!.Domains.Select(d => d.Name).Should().Equal(second);
        page.NextCursor.Should().BeNull();
        plugin.Received.OfType<RegistrarDomainListContext>().Single().Cursor.Should().Be("c-2");
    }

    /// <summary>A module that is not an installed registrar plugin is refused with 400, and the registrar is not asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_AnUnknownModule_Returns400Async()
    {
        var plugin = RegistrarImportTestSeeds.PluginHolding();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);

        var response = await admin.GetAsync("/api/domains/registrar/no-such-registrar");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        plugin.Received.Should().BeEmpty();
    }

    /// <summary>A cursor longer than a registrar's token may be is refused with 400 before the registrar is asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ATooLongCursor_Returns400Async()
    {
        var plugin = RegistrarImportTestSeeds.PluginHolding();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);
        var cursor = new string('c', ListRegistrarDomainsValidator.MaxCursorLength + 1);

        var response = await admin.GetAsync($"/api/domains/registrar/{TestRegistrarModules.First}?cursor={cursor}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        plugin.Received.Should().BeEmpty();
    }

    /// <summary>A registrar that refuses the saved credentials is answered with the mapped code, not a server error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ARefusedRegistrar_AnswersTheMappedCodeAsync()
    {
        var plugin = RegistrarImportTestSeeds.PluginHolding();
        plugin.DomainPages = _ => ValueResult<RegistrarDomainPage, RegistrarRefusalReason>.Refused(RegistrarRefusalReason.AccessDenied);
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);

        var response = await admin.GetAsync($"/api/domains/registrar/{TestRegistrarModules.First}");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.REGISTRAR_ACCESS_DENIED);
    }

    /// <summary>A registrar plugin that offers no domain list is answered with 409 <c>REGISTRAR_OPERATION_UNSUPPORTED</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ARegistrarWithoutADomainList_Returns409Async()
    {
        var plugin = RegistrarImportTestSeeds.PluginHolding();
        plugin.DomainPages = _ => ValueResult<RegistrarDomainPage, RegistrarRefusalReason>.NotSupported();
        var admin = await RegistrarImportTestSeeds.AdminClientAsync(factory, plugin);

        var response = await admin.GetAsync($"/api/domains/registrar/{TestRegistrarModules.First}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.REGISTRAR_OPERATION_UNSUPPORTED);
    }

    /// <summary>A caller without a token is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_WithoutToken_Returns401Async()
    {
        var response = await factory.CreateClient().GetAsync($"/api/domains/registrar/{TestRegistrarModules.First}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client account that is not staff is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_AsAClientUser_Returns403Async()
    {
        var (client, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "list-registrar-domains");

        var response = await client.GetAsync($"/api/domains/registrar/{TestRegistrarModules.First}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
