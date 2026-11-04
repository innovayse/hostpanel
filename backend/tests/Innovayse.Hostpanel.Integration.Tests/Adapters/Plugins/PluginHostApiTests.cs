using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Host;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginHostApi"/> against Postgres: it shows a plugin only the bound client's profile, services and
/// invoices, only what the caller's permissions allow, hides drafts, lists the
/// newest 50 first, and answers a missing client like a call without one.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginHostApiTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The plugin the test bindings name; the host API never reads it.</summary>
    private const string PluginId = "host-api-test";

    /// <summary>The opaque client reference the test bindings carry; the host API never reads it.</summary>
    private const string ClientRef = "host-api-test-client-ref";

    /// <summary>A fixed moment the seeded rows are dated from.</summary>
    private static readonly DateTimeOffset Origin = new(2031, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A caller holding the invoices right reads the invoices; one without it gets none.</summary>
    [Fact]
    public async Task Invoices_FollowTheViewPayInvoicesRightAsync()
    {
        var seeded = await SeedAsync("InvoiceRight", services: 1, invoices: 2);

        var held = await FactsAsync(seeded.ClientId, ClientPermission.ViewPayInvoices);
        using var scope = factory.Services.CreateScope();
        var notHeld = await Api(scope, Binding(seeded.ClientId, ClientPermission.All & ~ClientPermission.ViewPayInvoices))
            .ListInvoicesAsync(CancellationToken.None);

        held.Invoices.Select(i => i.Id).Should().BeEquivalentTo(seeded.InvoiceIds);
        notHeld.Should().BeEmpty();
    }

    /// <summary>A caller holding the services right reads the services; one without it gets none.</summary>
    [Fact]
    public async Task Services_FollowTheViewProductsServicesRightAsync()
    {
        var seeded = await SeedAsync("ServiceRight", services: 2, invoices: 1);

        var held = await FactsAsync(seeded.ClientId, ClientPermission.ViewProductsServices);
        using var scope = factory.Services.CreateScope();
        var notHeld = await Api(scope, Binding(seeded.ClientId, ClientPermission.All & ~ClientPermission.ViewProductsServices))
            .ListServicesAsync(CancellationToken.None);

        held.Services.Select(s => s.Id).Should().BeEquivalentTo(seeded.ServiceIds);
        notHeld.Should().BeEmpty();
    }

    /// <summary>
    /// Every client caller gets the name, currency and language, but the sign-in email only with the master profile right,
    /// as a sub-user without it never sees the address in the portal.
    /// </summary>
    [Fact]
    public async Task ProfileEmail_FollowsTheMasterProfileRight_WhileTheRestIsSharedAsync()
    {
        var registered = await ClientsTestSeeds.RegisterClientAsync(factory, "plugin-host-email-right");

        using var scope = factory.Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<IIdentityProvider>().FindBySubjectAsync(registered.UserId, CancellationToken.None);
        var holder = await Api(scope, Binding(registered.ClientId, ClientPermission.ModifyMasterProfile)).GetProfileAsync(CancellationToken.None);
        var subUser = await Api(scope, Binding(registered.ClientId, ClientPermission.All & ~ClientPermission.ModifyMasterProfile))
            .GetProfileAsync(CancellationToken.None);
        var noRights = await Api(scope, Binding(registered.ClientId, ClientPermission.None)).GetProfileAsync(CancellationToken.None);

        holder!.Email.Should().BeEquivalentTo(registered.Email);
        foreach (var profile in new[] { subUser!, noRights! })
        {
            profile.Email.Should().BeNull();
            profile.Name.Should().Be(holder.Name);
            profile.Currency.Should().Be(holder.Currency);
            profile.Language.Should().Be(account!.Language);
        }
    }

    /// <summary>Each client sees exactly its own services and invoices, and its own name.</summary>
    [Fact]
    public async Task BoundToAClient_ReturnsOnlyThatClientsFactsAsync()
    {
        var a = await SeedAsync("A", services: 2, invoices: 2);
        var b = await SeedAsync("B", services: 3, invoices: 1);

        var forA = await FactsAsync(a.ClientId);
        var forB = await FactsAsync(b.ClientId);

        forA.Services.Select(s => s.Id).Should().BeEquivalentTo(a.ServiceIds);
        forA.Invoices.Select(i => i.Id).Should().BeEquivalentTo(a.InvoiceIds);
        forA.Profile!.Name.Should().Be(a.ExpectedName);
        forB.Services.Select(s => s.Id).Should().BeEquivalentTo(b.ServiceIds);
        forB.Invoices.Select(i => i.Id).Should().BeEquivalentTo(b.InvoiceIds);
        forB.Profile!.Name.Should().Be(b.ExpectedName);
    }

    /// <summary>A client with nothing yet has a profile and empty lists.</summary>
    [Fact]
    public async Task AClientWithNoRows_HasAProfileAndEmptyListsAsync()
    {
        var seeded = await SeedAsync("Empty", services: 0, invoices: 0);

        var facts = await FactsAsync(seeded.ClientId);

        facts.Profile.Should().NotBeNull();
        facts.Services.Should().BeEmpty();
        facts.Invoices.Should().BeEmpty();
    }

    /// <summary>A call without a client gets no profile and empty lists even though other clients have rows.</summary>
    [Fact]
    public async Task WithoutAClient_ReturnsNothingAsync()
    {
        await SeedAsync("Other", services: 2, invoices: 2);

        var facts = await FactsAsync(null);

        facts.Profile.Should().BeNull();
        facts.Services.Should().BeEmpty();
        facts.Invoices.Should().BeEmpty();
    }

    /// <summary>A client id that has no row answers like a call without a client.</summary>
    [Fact]
    public async Task AClientThatDoesNotExist_ReturnsNothingAsync()
    {
        var facts = await FactsAsync(int.MaxValue);

        facts.Profile.Should().BeNull();
        facts.Services.Should().BeEmpty();
        facts.Invoices.Should().BeEmpty();
    }

    /// <summary>A draft invoice is never shared; unpaid and paid ones are, with the SDK status of the same name.</summary>
    [Fact]
    public async Task DraftInvoices_AreHidden_AndStatusesMapAsync()
    {
        var seeded = await SeedAsync("Statuses", services: 0, invoices: 0);
        int draftId, unpaidId, paidId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var draft = Invoice.CreateDraft(seeded.ClientId, Origin.AddDays(14), "USD", Origin);
            var unpaid = Invoice.Create(seeded.ClientId, Origin.AddDays(14), "USD", Origin);
            var paid = Invoice.Create(seeded.ClientId, Origin.AddDays(14), "USD", Origin);
            paid.MarkPaid($"tx-{Guid.NewGuid():N}", Origin);
            context.Invoices.AddRange(draft, unpaid, paid);
            await context.SaveChangesAsync(CancellationToken.None);
            (draftId, unpaidId, paidId) = (draft.Id, unpaid.Id, paid.Id);
        }

        var invoices = (await FactsAsync(seeded.ClientId)).Invoices;

        invoices.Select(i => i.Id).Should().BeEquivalentTo([unpaidId, paidId]).And.NotContain(draftId);
        invoices.Single(i => i.Id == unpaidId).Status.Should().Be(PluginInvoiceStatus.Unpaid);
        invoices.Single(i => i.Id == paidId).Status.Should().Be(PluginInvoiceStatus.Paid);
    }

    /// <summary>With 51 rows of each kind exactly the newest 50 come back, newest first, and the oldest is left out.</summary>
    [Fact]
    public async Task Lists_AreCappedAtFifty_NewestFirstAsync()
    {
        var seeded = await SeedAsync("Many", services: PluginHostLimits.MaxListed + 1, invoices: PluginHostLimits.MaxListed + 1);

        var facts = await FactsAsync(seeded.ClientId);

        facts.Services.Should().HaveCount(PluginHostLimits.MaxListed);
        facts.Invoices.Should().HaveCount(PluginHostLimits.MaxListed);

        // Row i is dated Origin + i minutes, so the highest index is the newest.
        var expectedServices = seeded.ServiceIds.Skip(1).Reverse().ToList();
        var expectedInvoices = seeded.InvoiceIds.Skip(1).Reverse().ToList();
        facts.Services.Select(s => s.Id).Should().Equal(expectedServices);
        facts.Invoices.Select(i => i.Id).Should().Equal(expectedInvoices);
        facts.Services.Select(s => s.Id).Should().NotContain(seeded.ServiceIds[0]);
        facts.Invoices.Select(i => i.Id).Should().NotContain(seeded.InvoiceIds[0]);
    }

    /// <summary>Rows with the same creation moment are ordered by id, highest first.</summary>
    [Fact]
    public async Task RowsCreatedAtTheSameMoment_AreOrderedByIdDescendingAsync()
    {
        var seeded = await SeedAsync("Ties", services: 0, invoices: 0);
        List<int> serviceIds, invoiceIds;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var productId = await SeedProductAsync(context);
            var services = new[] { NewService(seeded.ClientId, productId, Origin), NewService(seeded.ClientId, productId, Origin) };
            var invoices = new[] { NewInvoice(seeded.ClientId, Origin), NewInvoice(seeded.ClientId, Origin) };
            context.ClientServices.AddRange(services);
            context.Invoices.AddRange(invoices);
            await context.SaveChangesAsync(CancellationToken.None);
            serviceIds = services.Select(s => s.Id).OrderByDescending(id => id).ToList();
            invoiceIds = invoices.Select(i => i.Id).OrderByDescending(id => id).ToList();
        }

        var facts = await FactsAsync(seeded.ClientId);

        facts.Services.Select(s => s.Id).Should().Equal(serviceIds);
        facts.Invoices.Select(i => i.Id).Should().Equal(invoiceIds);
    }

    /// <summary>The profile name is the company when one is set, else the first and last name.</summary>
    [Fact]
    public async Task ProfileName_IsTheCompanyWhenSet_ElseFirstAndLastNameAsync()
    {
        var withCompany = await SeedAsync("Co", services: 0, invoices: 0, companyName: $"Acme {Guid.NewGuid():N}");
        var withoutCompany = await SeedAsync("Person", services: 0, invoices: 0);

        (await FactsAsync(withCompany.ClientId)).Profile!.Name.Should().Be(withCompany.ExpectedName);
        (await FactsAsync(withoutCompany.ClientId)).Profile!.Name.Should().Be($"Ada {withoutCompany.LastName}");
    }

    /// <summary>The profile currency is the one the payer currency resolver gives for the client.</summary>
    [Fact]
    public async Task ProfileCurrency_IsTheResolvedPayerCurrencyAsync()
    {
        var seeded = await SeedAsync("Money", services: 0, invoices: 0);

        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>();
        var expected = await resolver.ForClientAsync(seeded.ClientId, CancellationToken.None);
        var profile = await Api(scope, Binding(seeded.ClientId, ClientPermission.All)).GetProfileAsync(CancellationToken.None);

        profile!.Currency.Should().Be(expected.Code);
    }

    /// <summary>A client with an identity account shows its email and language; one whose user has no account shows no email.</summary>
    [Fact]
    public async Task ProfileEmailAndLanguage_ComeFromTheIdentityAccountAsync()
    {
        var registered = await ClientsTestSeeds.RegisterClientAsync(factory, "plugin-host");
        var orphan = await SeedAsync("Orphan", services: 0, invoices: 0);

        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IIdentityProvider>();
        var account = await identity.FindBySubjectAsync(registered.UserId, CancellationToken.None);
        var profile = await Api(scope, Binding(registered.ClientId, ClientPermission.All)).GetProfileAsync(CancellationToken.None);
        var orphanProfile = await Api(scope, Binding(orphan.ClientId, ClientPermission.All)).GetProfileAsync(CancellationToken.None);

        account.Should().NotBeNull();
        profile!.Email.Should().BeEquivalentTo(registered.Email);
        profile.Language.Should().Be(account!.Language);
        orphanProfile!.Email.Should().BeNull();
        orphanProfile.Language.Should().BeNull();
    }

    /// <summary>Builds the host API of one client from a fresh scope and reads all three facts.</summary>
    /// <param name="clientId">The client the API is bound to, or <see langword="null"/>.</param>
    /// <param name="permissions">The permissions the caller holds; every one unless a test narrows them.</param>
    /// <returns>The profile and both lists.</returns>
    private async Task<HostFacts> FactsAsync(int? clientId, ClientPermission permissions = ClientPermission.All)
    {
        using var scope = factory.Services.CreateScope();
        var api = Api(scope, Binding(clientId, permissions));
        return new HostFacts(
            await api.GetProfileAsync(CancellationToken.None),
            await api.ListServicesAsync(CancellationToken.None),
            await api.ListInvoicesAsync(CancellationToken.None));
    }

    /// <summary>A binding of the test plugin to a client, or to none, with the caller's permissions.</summary>
    /// <param name="clientId">The client, or <see langword="null"/> for a call without one.</param>
    /// <param name="permissions">The permissions the caller holds; ignored without a client.</param>
    /// <returns>The binding.</returns>
    private static PluginCallBinding Binding(int? clientId, ClientPermission permissions)
    {
        return clientId is null
            ? PluginCallBinding.WithoutClient(PluginId)
            : PluginCallBinding.ForClient(PluginId, clientId.Value, ClientRef, permissions);
    }

    /// <summary>
    /// The test plugin is not loaded, so no manifest declares SSH host access for it: both SSH members refuse with
    /// <c>NotAllowed</c> before anything is read, even for a client caller holding every permission. The granted path is
    /// covered by <c>IssuePluginSshAccessTests</c> and <c>RevokePluginSshAccessTests</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SshAccess_IsRefusedAsNotAllowed()
    {
        using var scope = factory.Services.CreateScope();
        var api = Api(scope, Binding(1, ClientPermission.All));

        var issue = () => api.IssueSshAccessAsync(1, "label", CancellationToken.None);
        var revoke = () => api.RevokeSshAccessAsync(1, Guid.NewGuid(), CancellationToken.None);

        var issued = await issue.Should().ThrowAsync<PluginHostAccessException>();
        issued.Which.Failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        var revoked = await revoke.Should().ThrowAsync<PluginHostAccessException>();
        revoked.Which.Failure.Should().Be(PluginHostAccessFailure.NotAllowed);
    }

    /// <summary>Builds a <see cref="PluginHostApi"/> from the scope's services.</summary>
    /// <param name="scope">The scope that supplies the facts reader, identity provider, currency resolver and SSH access.</param>
    /// <param name="binding">The call the API is bound to.</param>
    /// <returns>The API.</returns>
    private static PluginHostApi Api(IServiceScope scope, PluginCallBinding binding)
    {
        var sp = scope.ServiceProvider;
        return new PluginHostApi(
            binding,
            sp.GetRequiredService<IPluginHostFactsReader>(),
            sp.GetRequiredService<IIdentityProvider>(),
            sp.GetRequiredService<IPayerCurrencyResolver>(),
            sp.GetRequiredService<PluginSshAccessService>());
    }

    /// <summary>Seeds a product so services can reference it.</summary>
    /// <param name="context">The context to save into.</param>
    /// <returns>The product id.</returns>
    private static async Task<int> SeedProductAsync(AppDbContext context)
    {
        var group = ProductGroup.Create($"plugin-host-{Guid.NewGuid():N}", null);
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

    /// <summary>Seeds a client with a number of services and invoices, the i-th dated i minutes after <see cref="Origin"/>.</summary>
    /// <param name="label">Distinguishes the client's name.</param>
    /// <param name="services">How many services to create.</param>
    /// <param name="invoices">How many non-draft invoices to create.</param>
    /// <param name="companyName">The client's company name, or <see langword="null"/>.</param>
    /// <returns>The ids, oldest first, and the name the profile should show.</returns>
    private async Task<Seeded> SeedAsync(string label, int services, int invoices, string? companyName = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lastName = $"{label}-{Guid.NewGuid():N}";
        var client = Client.Create($"plugin-host-{Guid.NewGuid():N}", "Ada", lastName, $"{Guid.NewGuid():N}@example.test", Origin, companyName);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var serviceRows = new List<ClientService>();
        if (services > 0)
        {
            var productId = await SeedProductAsync(context);
            for (var i = 0; i < services; i++)
            {
                serviceRows.Add(NewService(client.Id, productId, Origin.AddMinutes(i)));
            }
        }

        var invoiceRows = Enumerable.Range(0, invoices).Select(i => NewInvoice(client.Id, Origin.AddMinutes(i))).ToList();
        context.ClientServices.AddRange(serviceRows);
        context.Invoices.AddRange(invoiceRows);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Seeded(
            client.Id,
            serviceRows.Select(s => s.Id).ToList(),
            invoiceRows.Select(i => i.Id).ToList(),
            lastName,
            string.IsNullOrWhiteSpace(companyName) ? $"Ada {lastName}" : companyName);
    }

    /// <summary>What a seeded client holds.</summary>
    /// <param name="ClientId">The client id.</param>
    /// <param name="ServiceIds">The service ids, oldest first.</param>
    /// <param name="InvoiceIds">The invoice ids, oldest first.</param>
    /// <param name="LastName">The client's last name.</param>
    /// <param name="ExpectedName">The name the profile should show.</param>
    private sealed record Seeded(int ClientId, List<int> ServiceIds, List<int> InvoiceIds, string LastName, string ExpectedName);

    /// <summary>What a plugin sees of one client through the host API.</summary>
    /// <param name="Profile">The client's profile, or <see langword="null"/> when the call has no client.</param>
    /// <param name="Services">The client's services, newest first.</param>
    /// <param name="Invoices">The client's invoices without drafts, newest first.</param>
    private sealed record HostFacts(
        PluginClientProfile? Profile,
        IReadOnlyList<PluginClientService> Services,
        IReadOnlyList<PluginClientInvoice> Invoices);
}
