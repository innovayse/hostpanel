using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginHostFactsReader"/> against Postgres: the container resolves it, it reads one client's name parts and
/// owner subject, and lists only that client's newest 50 services and client-visible invoices, newest first.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginHostFactsReaderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A fixed moment the seeded rows are dated from, so no row depends on the wall clock.</summary>
    private static readonly DateTimeOffset Origin = new(2032, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The production container resolves the reader for the port.</summary>
    [Fact]
    public void Container_ResolvesThePluginHostFactsReader()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IPluginHostFactsReader>().Should().BeOfType<PluginHostFactsReader>();
    }

    /// <summary>A client is read with its name parts, company and owner subject; an id with no row reads as null.</summary>
    [Fact]
    public async Task FindClient_ReadsTheNamePartsAndOwnerSubject_AndNullForAMissingClientAsync()
    {
        var owner = $"facts-owner-{Guid.NewGuid():N}";
        var company = $"Facts {Guid.NewGuid():N}";
        var clientId = await SeedClientAsync(owner, company);

        using var scope = factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPluginHostFactsReader>();
        var found = await reader.FindClientAsync(clientId, CancellationToken.None);
        var missing = await reader.FindClientAsync(int.MaxValue, CancellationToken.None);

        found.Should().Be(new PluginHostClientDto("Ada", "Lovelace", company, owner));
        missing.Should().BeNull();
    }

    /// <summary>With 51 services only the newest 50 of that client are listed, newest first, and another client's are not.</summary>
    [Fact]
    public async Task ListNewestServices_IsCappedNewestFirst_AndOnlyTheClientsOwnAsync()
    {
        var clientId = await SeedClientAsync($"facts-svc-{Guid.NewGuid():N}", null);
        var otherId = await SeedClientAsync($"facts-svc-other-{Guid.NewGuid():N}", null);
        List<int> ids;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var productId = await SeedProductAsync(context);
            var rows = Enumerable.Range(0, PluginHostLimits.MaxListed + 1)
                .Select(i => NewService(clientId, productId, Origin.AddMinutes(i)))
                .ToList();
            context.ClientServices.AddRange(rows);
            context.ClientServices.Add(NewService(otherId, productId, Origin.AddDays(1)));
            await context.SaveChangesAsync(CancellationToken.None);
            ids = rows.Select(s => s.Id).ToList();
        }

        using var readScope = factory.Services.CreateScope();
        var listed = await readScope.ServiceProvider.GetRequiredService<IPluginHostFactsReader>()
            .ListNewestServicesAsync(clientId, CancellationToken.None);

        // Row i is dated Origin + i minutes, so the highest index is the newest and the first one is left out.
        listed.Select(s => s.Id).Should().Equal(ids.Skip(1).Reverse());
    }

    /// <summary>Drafts are never listed, and with 51 visible invoices only the newest 50 of that client come back, newest first.</summary>
    [Fact]
    public async Task ListNewestInvoices_HidesDrafts_IsCappedNewestFirst_AndOnlyTheClientsOwnAsync()
    {
        var clientId = await SeedClientAsync($"facts-inv-{Guid.NewGuid():N}", null);
        var otherId = await SeedClientAsync($"facts-inv-other-{Guid.NewGuid():N}", null);
        List<int> ids;
        int draftId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = Enumerable.Range(0, PluginHostLimits.MaxListed + 1)
                .Select(i => NewInvoice(clientId, Origin.AddMinutes(i)))
                .ToList();
            var draft = Invoice.CreateDraft(clientId, Origin.AddDays(14), "USD", Origin.AddDays(1));
            draft.Backdate(Origin.AddDays(1));
            context.Invoices.AddRange(rows);
            context.Invoices.Add(draft);
            context.Invoices.Add(NewInvoice(otherId, Origin.AddDays(2)));
            await context.SaveChangesAsync(CancellationToken.None);
            ids = rows.Select(i => i.Id).ToList();
            draftId = draft.Id;
        }

        using var readScope = factory.Services.CreateScope();
        var listed = await readScope.ServiceProvider.GetRequiredService<IPluginHostFactsReader>()
            .ListNewestInvoicesAsync(clientId, CancellationToken.None);

        listed.Select(i => i.Id).Should().Equal(ids.Skip(1).Reverse());
        listed.Select(i => i.Id).Should().NotContain(draftId);
        listed.Should().OnlyContain(i => i.Status != InvoiceStatus.Draft && i.Currency == "USD");
    }

    /// <summary>A client with no rows, or an id with no client, lists nothing.</summary>
    [Fact]
    public async Task Lists_AreEmptyForAClientWithoutRows_AndForAMissingClientAsync()
    {
        var clientId = await SeedClientAsync($"facts-empty-{Guid.NewGuid():N}", null);

        using var scope = factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPluginHostFactsReader>();

        (await reader.ListNewestServicesAsync(clientId, CancellationToken.None)).Should().BeEmpty();
        (await reader.ListNewestInvoicesAsync(clientId, CancellationToken.None)).Should().BeEmpty();
        (await reader.ListNewestServicesAsync(int.MaxValue, CancellationToken.None)).Should().BeEmpty();
        (await reader.ListNewestInvoicesAsync(int.MaxValue, CancellationToken.None)).Should().BeEmpty();
    }

    /// <summary>Only the client's own active services with a domain on the given products are listed, newest first.</summary>
    [Fact]
    public async Task ListActiveServiceDomains_IsOnlyTheClientsOwnActiveOnes_NewestFirstAsync()
    {
        var clientId = await SeedClientAsync($"facts-dom-{Guid.NewGuid():N}", null);
        var otherId = await SeedClientAsync($"facts-dom-other-{Guid.NewGuid():N}", null);
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            productId = await SeedProductAsync(context);
            var unlistedProductId = await SeedProductAsync(context);
            context.ClientServices.AddRange(
                DomainService(clientId, unlistedProductId, "unlisted.example", active: true, Origin.AddDays(5)),
                DomainService(clientId, productId, "older.example", active: true, Origin),
                DomainService(clientId, productId, "newer.example", active: true, Origin.AddDays(1)),
                DomainService(clientId, productId, "pending.example", active: false, Origin.AddDays(2)),
                DomainService(clientId, productId, null, active: true, Origin.AddDays(3)),
                DomainService(otherId, productId, "foreign.example", active: true, Origin.AddDays(4)));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        using var readScope = factory.Services.CreateScope();
        var reader = readScope.ServiceProvider.GetRequiredService<IPluginHostFactsReader>();
        var listed = await reader.ListActiveServiceDomainsAsync(clientId, [productId], CancellationToken.None);
        var none = await reader.ListActiveServiceDomainsAsync(clientId, [], CancellationToken.None);

        listed.Should().Equal("newer.example", "older.example");
        none.Should().BeEmpty();
    }

    /// <summary>Raises a service on a domain, active or still pending.</summary>
    /// <param name="clientId">The owner.</param>
    /// <param name="productId">The product.</param>
    /// <param name="domain">The domain, or <see langword="null"/> for none.</param>
    /// <param name="active">Whether the service is activated.</param>
    /// <param name="createdAt">The creation moment (UTC).</param>
    /// <returns>The service, not yet saved.</returns>
    private static ClientService DomainService(int clientId, int productId, string? domain, bool active, DateTimeOffset createdAt)
    {
        var service = ClientService.CreateOrdered(clientId, productId, BillingCycle.Monthly, domain, 0m, 0m, null, null, createdAt);
        service.Backdate(createdAt);
        if (active)
        {
            service.Activate("ref", createdAt, createdAt);
        }

        return service;
    }

    /// <summary>Seeds a client named Ada Lovelace.</summary>
    /// <param name="ownerSubject">The owner's identity subject.</param>
    /// <param name="companyName">The company name, or <see langword="null"/>.</param>
    /// <returns>The client id.</returns>
    private async Task<int> SeedClientAsync(string ownerSubject, string? companyName)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = Client.Create(ownerSubject, "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", Origin, companyName);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }

    /// <summary>Seeds a product so services can reference it.</summary>
    /// <param name="context">The context to save into.</param>
    /// <returns>The product id.</returns>
    private static async Task<int> SeedProductAsync(AppDbContext context)
    {
        var group = ProductGroup.Create($"facts-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var product = Product.Create(group.Id, $"Plan {Guid.NewGuid():N}", null, null, null, "plan", ProductType.SharedHosting, 0m, 0m, Origin);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        return product.Id;
    }

    /// <summary>Raises a service dated at a given moment.</summary>
    /// <param name="clientId">The owner.</param>
    /// <param name="productId">The product.</param>
    /// <param name="createdAt">The creation moment (UTC).</param>
    /// <returns>The service, not yet saved.</returns>
    private static ClientService NewService(int clientId, int productId, DateTimeOffset createdAt)
    {
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, createdAt);
        service.Backdate(createdAt);
        return service;
    }

    /// <summary>Raises an unpaid invoice dated at a given moment.</summary>
    /// <param name="clientId">The client billed.</param>
    /// <param name="createdAt">The creation moment (UTC).</param>
    /// <returns>The invoice, not yet saved.</returns>
    private static Invoice NewInvoice(int clientId, DateTimeOffset createdAt)
    {
        var invoice = Invoice.Create(clientId, createdAt.AddDays(14), "USD", createdAt);
        invoice.Backdate(createdAt);
        return invoice;
    }
}
