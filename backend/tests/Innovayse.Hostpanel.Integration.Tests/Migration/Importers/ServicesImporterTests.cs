using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="ServicesImporter"/>, driven end to end through <c>MigrationPullWorker.RunAsync</c>
/// against a <see cref="FakeMigrationPullSource"/>: imports a client service matched against an
/// existing product by name, deduplicated by client and domain or panel username, landing in its
/// source state without events.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServicesImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A service for a known client and an existing product is imported.</summary>
    [Fact]
    public async Task RunAsync_Services_ImportsAServiceForAKnownClientAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Hosting = new MigrationHostingTotals(Services: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var group = ProductGroup.Create($"group-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var productName = $"product-{Guid.NewGuid():N}";
        var product = Product.Create(group.Id, productName, null, null, null, null, ProductType.SharedHosting, 9.99m, 99.99m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Services);
        var domain = $"svc-{Guid.NewGuid():N}.test";
        source.SetDatasetJson("services", $$"""
            [{"ClientEmail":"{{email}}","ProductName":"{{productName}}","BillingCycle":"Monthly","Status":"Active","Domain":"{{domain}}","Username":null,"NextDueDate":null,"TerminatedAt":null,"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var service = await context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Domain == domain);
        Assert.NotNull(service);

        // Idempotent re-import: a service matched by client and domain is skipped the second time.
        var job2 = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Services);
        await worker.RunAsync(job2.Id, CancellationToken.None);

        var services = await context.ClientServices.AsNoTracking().Where(s => s.Domain == domain).ToListAsync();
        Assert.Single(services);
    }

    /// <summary>An imported service keeps the registration date the source gives as its creation date.</summary>
    [Fact]
    public async Task RunAsync_Services_KeepsTheSourceRegistrationDateAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Hosting = new MigrationHostingTotals(Services: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var group = ProductGroup.Create($"group-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var productName = $"product-{Guid.NewGuid():N}";
        var product = Product.Create(group.Id, productName, null, null, null, null, ProductType.SharedHosting, 9.99m, 99.99m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Services);
        var domain = $"svc-{Guid.NewGuid():N}.test";
        var registeredAt = new DateTimeOffset(2021, 3, 14, 0, 0, 0, TimeSpan.Zero);
        source.SetDatasetJson("services", $$"""
            [{"ClientEmail":"{{email}}","ProductName":"{{productName}}","BillingCycle":"Monthly","Status":"Active","Domain":"{{domain}}","Username":null,"NextDueDate":null,"TerminatedAt":null,"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null,"RegisteredAt":"2021-03-14T00:00:00+00:00"}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var service = await context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Domain == domain);
        Assert.NotNull(service);
        Assert.Equal(registeredAt, service.CreatedAt);
    }

    /// <summary>
    /// An active service keeps the next due date the source gives, not the one activation anchors on
    /// the registration date.
    /// </summary>
    [Fact]
    public async Task RunAsync_Services_KeepsTheSourceNextDueDateAsync()
    {
        var nextDue = new DateTimeOffset(2031, 7, 9, 0, 0, 0, TimeSpan.Zero);
        var service = await ImportOneAsync(
            "Active", "\"NextDueDate\":\"2031-07-09T00:00:00+00:00\",\"TerminatedAt\":null,\"RegisteredAt\":\"2021-03-14T00:00:00+00:00\"");

        Assert.Equal(nextDue, service.NextRenewalAt);
    }

    /// <summary>A terminated service keeps the termination date the source gives, not the moment of the import.</summary>
    [Fact]
    public async Task RunAsync_Services_KeepsTheSourceTerminationDateAsync()
    {
        var endedAt = new DateTimeOffset(2022, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var service = await ImportOneAsync(
            "Terminated", "\"NextDueDate\":null,\"TerminatedAt\":\"2022-02-01T00:00:00+00:00\",\"RegisteredAt\":null");

        Assert.Equal(ServiceStatus.Terminated, service.Status);
        Assert.Equal(endedAt, service.TerminatedAt);
    }

    /// <summary>
    /// A service the source suspended by hand lands suspended with a manual source, keeps its panel
    /// username as the provisioning reference, and raises no event on the way in.
    /// </summary>
    [Fact]
    public async Task RunAsync_Services_ImportsAnAdminSuspensionAsManualAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var services = await ImportRecordAsync(
            tag, $$"""{"ClientEmail":"{email}","ProductName":"{product}","BillingCycle":"Monthly","Status":"Suspended (Admin)","Domain":null,"Username":"u{{tag}}","NextDueDate":null,"TerminatedAt":null,"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null,"RegisteredAt":null}""",
            runs: 1);

        var service = Assert.Single(services);
        Assert.Equal(ServiceStatus.Suspended, service.Status);
        Assert.Equal(SuspensionSource.Manual, service.SuspensionSource);
        Assert.Equal($"u{tag}", service.ProvisioningRef);
        Assert.NotNull(service.SuspendedAt);
    }

    /// <summary>A plain source suspension does not say why, so it lands as <see cref="SuspensionSource.Other"/>.</summary>
    [Fact]
    public async Task RunAsync_Services_ImportsAPlainSuspensionAsOtherAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var services = await ImportRecordAsync(
            tag, $$"""{"ClientEmail":"{email}","ProductName":"{product}","BillingCycle":"Monthly","Status":"Suspended","Domain":null,"Username":"u{{tag}}","NextDueDate":null,"TerminatedAt":null,"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null,"RegisteredAt":null}""",
            runs: 1);

        Assert.Equal(SuspensionSource.Other, Assert.Single(services).SuspensionSource);
    }

    /// <summary>A record with no username has no provisioning reference, never a made-up one.</summary>
    [Fact]
    public async Task RunAsync_Services_LeavesTheReferenceEmptyWithoutAUsernameAsync()
    {
        var service = await ImportOneAsync("Active", "\"NextDueDate\":null,\"TerminatedAt\":null,\"RegisteredAt\":null");

        Assert.Null(service.ProvisioningRef);
    }

    /// <summary>A record whose product name matches no product here is skipped, not linked to another product.</summary>
    [Fact]
    public async Task RunAsync_Services_SkipsARecordWithAnUnknownProductAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var services = await ImportRecordAsync(
            tag, $$"""{"ClientEmail":"{email}","ProductName":"missing-{{tag}}","BillingCycle":"Monthly","Status":"Active","Domain":null,"Username":"u{{tag}}","NextDueDate":null,"TerminatedAt":null,"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null,"RegisteredAt":null}""",
            runs: 1);

        Assert.Empty(services);
    }

    /// <summary>A record naming a cycle this version cannot bill is skipped, not stored on a guessed cycle.</summary>
    /// <param name="cycleWord">The cycle word the source sends.</param>
    [Theory]
    [InlineData("Triennially")]
    [InlineData("One Time")]
    public async Task RunAsync_Services_SkipsARecordWithAnUnsupportedBillingCycleAsync(string cycleWord)
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var services = await ImportRecordAsync(
            tag, $$"""{"ClientEmail":"{email}","ProductName":"{product}","BillingCycle":"{{cycleWord}}","Status":"Active","Domain":null,"Username":"u{{tag}}","NextDueDate":null,"TerminatedAt":null,"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null,"RegisteredAt":null}""",
            runs: 1);

        Assert.Empty(services);
    }

    /// <summary>
    /// A service on a quarterly, semi-annual or biennial cycle comes in on that cycle, whichever way the source
    /// panel spells the word, instead of being skipped.
    /// </summary>
    /// <param name="cycleWord">The cycle word the source sends.</param>
    /// <param name="expected">The cycle the imported service must hold.</param>
    [Theory]
    [InlineData("Quarterly", BillingCycle.Quarterly)]
    [InlineData("Semi-Annually", BillingCycle.SemiAnnual)]
    [InlineData("SemiAnnual", BillingCycle.SemiAnnual)]
    [InlineData("Biennially", BillingCycle.Biennial)]
    [InlineData("Biennial", BillingCycle.Biennial)]
    public async Task RunAsync_Services_ImportsTheNewCyclesAsync(string cycleWord, BillingCycle expected)
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var services = await ImportRecordAsync(
            tag, $$"""{"ClientEmail":"{email}","ProductName":"{product}","BillingCycle":"{{cycleWord}}","Status":"Active","Domain":null,"Username":"u{{tag}}","NextDueDate":null,"TerminatedAt":null,"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null,"RegisteredAt":null}""",
            runs: 1);

        Assert.Equal(expected, Assert.Single(services).BillingCycle);
    }

    /// <summary>The source's "Annually" spelling is imported as the annual cycle.</summary>
    [Fact]
    public async Task RunAsync_Services_ReadsTheAnnuallySpellingAsAnnualAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var services = await ImportRecordAsync(
            tag, $$"""{"ClientEmail":"{email}","ProductName":"{product}","BillingCycle":"Annually","Status":"Active","Domain":null,"Username":"u{{tag}}","NextDueDate":null,"TerminatedAt":null,"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null,"RegisteredAt":null}""",
            runs: 1);

        Assert.Equal(BillingCycle.Annual, Assert.Single(services).BillingCycle);
    }

    /// <summary>A record with no domain is matched by its panel username, so a second import adds nothing.</summary>
    [Fact]
    public async Task RunAsync_Services_DeduplicatesByUsernameWithoutADomainAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..12];
        var services = await ImportRecordAsync(
            tag, $$"""{"ClientEmail":"{email}","ProductName":"{product}","BillingCycle":"Monthly","Status":"Active","Domain":null,"Username":"u{{tag}}","NextDueDate":null,"TerminatedAt":null,"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null,"RegisteredAt":null}""",
            runs: 2);

        Assert.Single(services);
    }

    /// <summary>
    /// Imports one service record for a fresh client and product, <paramref name="runs"/> times, and
    /// reads back every service whose username carries <paramref name="tag"/>.
    /// </summary>
    /// <param name="tag">The unique token the record's username is built from.</param>
    /// <param name="record">The record as JSON, with <c>{email}</c> and <c>{product}</c> standing for the seeded client and product.</param>
    /// <param name="runs">How many import jobs run over the same record.</param>
    /// <returns>The services stored for the record.</returns>
    private async Task<List<ClientService>> ImportRecordAsync(string tag, string record, int runs)
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Hosting = new MigrationHostingTotals(Services: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var group = ProductGroup.Create($"group-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var productName = $"product-{Guid.NewGuid():N}";
        context.Products.Add(Product.Create(group.Id, productName, null, null, null, null, ProductType.SharedHosting, 9.99m, 99.99m, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);
        source.SetDatasetJson("services", "[" + record.Replace("{email}", email).Replace("{product}", productName) + "]");

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        for (var run = 0; run < runs; run++)
        {
            var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Services);
            await worker.RunAsync(job.Id, CancellationToken.None);
        }

        var username = $"u{tag}";
        return await context.ClientServices.AsNoTracking().Where(s => s.Username == username).ToListAsync();
    }

    /// <summary>Imports one service record for a fresh client and product and reads the stored row back.</summary>
    /// <param name="status">The source status word.</param>
    /// <param name="dates">The record's date members, written as JSON members.</param>
    /// <returns>The imported service.</returns>
    private async Task<ClientService> ImportOneAsync(string status, string dates)
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Hosting = new MigrationHostingTotals(Services: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var group = ProductGroup.Create($"group-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var productName = $"product-{Guid.NewGuid():N}";
        var product = Product.Create(group.Id, productName, null, null, null, null, ProductType.SharedHosting, 9.99m, 99.99m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Services);
        var domain = $"svc-{Guid.NewGuid():N}.test";
        source.SetDatasetJson("services", $$"""
            [{"ClientEmail":"{{email}}","ProductName":"{{productName}}","BillingCycle":"Monthly","Status":"{{status}}","Domain":"{{domain}}","Username":null,{{dates}},"SubscriptionId":null,"AdminNotes":null,"RecurringAmount":9.99,"FirstPaymentAmount":9.99,"PaymentMethod":null}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var service = await context.ClientServices.AsNoTracking().FirstOrDefaultAsync(s => s.Domain == domain);
        Assert.NotNull(service);
        return service;
    }
}
