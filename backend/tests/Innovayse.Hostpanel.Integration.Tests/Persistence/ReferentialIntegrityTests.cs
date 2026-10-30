using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Audit;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Migration;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Slides;
using Innovayse.Hostpanel.Domain.Slides.Enums;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The foreign keys that tie a record to the client, invoice, product, service, department,
/// migration job or category it names: which delete each one allows, restricts or follows, checked
/// on the model and against Postgres.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReferentialIntegrityTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Every foreign key added for the plain id columns, with the delete behavior it must have.</summary>
    public static TheoryData<Type, string, Type, DeleteBehavior> ForeignKeys => new()
    {
        { typeof(Invoice), nameof(Invoice.ClientId), typeof(Client), DeleteBehavior.Restrict },
        { typeof(Transaction), nameof(Transaction.ClientId), typeof(Client), DeleteBehavior.Restrict },
        { typeof(ClientService), nameof(ClientService.ClientId), typeof(Client), DeleteBehavior.Restrict },
        { typeof(RegisteredDomain), nameof(RegisteredDomain.ClientId), typeof(Client), DeleteBehavior.Restrict },
        { typeof(Order), nameof(Order.ClientId), typeof(Client), DeleteBehavior.Restrict },
        { typeof(Quote), nameof(Quote.ClientId), typeof(Client), DeleteBehavior.Restrict },
        { typeof(Ticket), nameof(Ticket.ClientId), typeof(Client), DeleteBehavior.Restrict },
        { typeof(BillableItem), nameof(BillableItem.ClientId), typeof(Client), DeleteBehavior.Restrict },
        { typeof(ActivityLog), nameof(ActivityLog.ClientId), typeof(Client), DeleteBehavior.SetNull },
        { typeof(Transaction), nameof(Transaction.InvoiceId), typeof(Invoice), DeleteBehavior.SetNull },
        { typeof(BillableItem), nameof(BillableItem.InvoiceId), typeof(Invoice), DeleteBehavior.SetNull },
        { typeof(Order), nameof(Order.InvoiceId), typeof(Invoice), DeleteBehavior.SetNull },
        { typeof(FulfilmentRecord), nameof(FulfilmentRecord.InvoiceId), typeof(Invoice), DeleteBehavior.Restrict },
        { typeof(ClientService), nameof(ClientService.ProductId), typeof(Product), DeleteBehavior.Restrict },
        { typeof(OrderItem), nameof(OrderItem.ProductId), typeof(Product), DeleteBehavior.Restrict },
        { typeof(CancellationRequest), nameof(CancellationRequest.ServiceId), typeof(ClientService), DeleteBehavior.Cascade },
        { typeof(RegisteredDomain), nameof(RegisteredDomain.LinkedServiceId), typeof(ClientService), DeleteBehavior.SetNull },
        { typeof(Ticket), nameof(Ticket.DepartmentId), typeof(Department), DeleteBehavior.SetNull },
        { typeof(Slide), nameof(Slide.ProductId), typeof(Product), DeleteBehavior.SetNull },
        { typeof(MigrationLog), nameof(MigrationLog.JobId), typeof(MigrationJob), DeleteBehavior.Cascade },
        { typeof(KbCategory), nameof(KbCategory.ParentCategoryId), typeof(KbCategory), DeleteBehavior.Restrict },
        { typeof(DownloadCategory), nameof(DownloadCategory.ParentCategoryId), typeof(DownloadCategory), DeleteBehavior.Restrict },
        { typeof(PredefinedReplyCategory), nameof(PredefinedReplyCategory.ParentCategoryId), typeof(PredefinedReplyCategory), DeleteBehavior.Restrict },
        { typeof(PredefinedReply), nameof(PredefinedReply.CategoryId), typeof(PredefinedReplyCategory), DeleteBehavior.Restrict },
        { typeof(Download), nameof(Download.CategoryId), typeof(DownloadCategory), DeleteBehavior.Restrict },
    };

    /// <summary>The model carries the foreign key, to the right principal, with the delete behavior documented for it.</summary>
    /// <param name="dependent">The entity holding the id.</param>
    /// <param name="property">The id column.</param>
    /// <param name="principal">The entity the id names.</param>
    /// <param name="behavior">The delete behavior the key must have.</param>
    [Theory]
    [MemberData(nameof(ForeignKeys))]
    public void ModelHasTheForeignKeyWithItsDeleteBehavior(
        Type dependent, string property, Type principal, DeleteBehavior behavior)
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;

        var foreignKey = model.FindEntityType(dependent)!.GetForeignKeys()
            .SingleOrDefault(k => k.Properties.Count == 1 && k.Properties[0].Name == property);

        foreignKey.Should().NotBeNull($"{dependent.Name}.{property} names a row of {principal.Name}");
        foreignKey!.PrincipalEntityType.ClrType.Should().Be(principal);
        foreignKey.DeleteBehavior.Should().Be(behavior);
    }

    /// <summary>The activity log's client is optional, so a system-wide entry and a deleted client's entry both fit.</summary>
    [Fact]
    public void ActivityLogClientIsOptional()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;

        model.FindEntityType(typeof(ActivityLog))!.FindProperty(nameof(ActivityLog.ClientId))!
            .IsNullable.Should().BeTrue();
    }

    /// <summary>A client who still has an invoice cannot be deleted: the database refuses and the client stays.</summary>
    [Fact]
    public async Task ClientWithAnInvoiceCannotBeDeleted()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        context.Invoices.Add(Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), TestCurrencies.Base, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);

        // The invoice is forgotten so the delete reaches the database, whose key is what is proven here;
        // with the invoice still tracked, the change tracker would refuse it first.
        context.DetachAll();
        var client = await context.Clients.SingleAsync(c => c.Id == clientId, CancellationToken.None);
        context.Clients.Remove(client);
        var act = () => context.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
        using var check = factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Clients
            .AnyAsync(c => c.Id == clientId, CancellationToken.None)).Should().BeTrue();
    }

    /// <summary>An invoice cannot be written for a client who does not exist.</summary>
    [Fact]
    public async Task InvoiceForAMissingClientIsRefused()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.Invoices.Add(Invoice.Create(int.MaxValue, DateTimeOffset.UtcNow.AddDays(7), TestCurrencies.Base, DateTimeOffset.UtcNow));

        var act = () => context.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>Deleting an invoice keeps the ledger row that settled it and only clears the link.</summary>
    [Fact]
    public async Task DeletingAnInvoiceKeepsItsLedgerRow()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), TestCurrencies.Base, DateTimeOffset.UtcNow);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        var ledgerRow = Transaction.Create(
            clientId, DateTimeOffset.UtcNow, "Payment", $"tx-{Guid.NewGuid():N}", invoice.Id, "manual", 10m, 0m, 0m, false, TestCurrencies.Base);
        context.Transactions.Add(ledgerRow);
        await context.SaveChangesAsync(CancellationToken.None);

        context.Invoices.Remove(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        using var check = factory.Services.CreateScope();
        var reloaded = await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Transactions
            .AsNoTracking().SingleAsync(t => t.Id == ledgerRow.Id, CancellationToken.None);
        reloaded.InvoiceId.Should().BeNull();
    }

    /// <summary>A product that a client holds a service of cannot be deleted.</summary>
    [Fact]
    public async Task ProductWithAServiceCannotBeDeleted()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var productId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        context.ClientServices.Add(ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);

        // The service is forgotten so the delete reaches the database, whose key is what is proven here;
        // with the service still tracked, the change tracker would refuse it first.
        context.DetachAll();
        var product = await context.Products.SingleAsync(p => p.Id == productId, CancellationToken.None);
        context.Products.Remove(product);
        var act = () => context.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// Deleting a service removes its cancellation requests and clears the link of a domain that was
    /// ordered with it, which stays.
    /// </summary>
    [Fact]
    public async Task DeletingAServiceRemovesItsRequestsAndUnlinksItsDomain()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var productId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);
        var request = CancellationRequest.Create(service.Id, CancellationType.Immediate, null);
        context.CancellationRequests.Add(request);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1),
            autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        domain.LinkService(service.Id);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);

        context.ClientServices.Remove(service);
        await context.SaveChangesAsync(CancellationToken.None);

        using var check = factory.Services.CreateScope();
        var reread = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await reread.CancellationRequests.AnyAsync(r => r.Id == request.Id, CancellationToken.None)).Should().BeFalse();
        (await reread.Domains.AsNoTracking().SingleAsync(d => d.Id == domain.Id, CancellationToken.None))
            .LinkedServiceId.Should().BeNull();
    }

    /// <summary>Deleting a department keeps its tickets, which are left without one.</summary>
    [Fact]
    public async Task DeletingADepartmentKeepsItsTickets()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var department = Department.Create($"Dept {Guid.NewGuid():N}", $"{Guid.NewGuid():N}@example.test");
        context.Departments.Add(department);
        await context.SaveChangesAsync(CancellationToken.None);
        var ticket = Ticket.Create(clientId, "Help", "Please help", department.Id, TicketPriority.Medium, DateTimeOffset.UtcNow);
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync(CancellationToken.None);

        context.Departments.Remove(department);
        await context.SaveChangesAsync(CancellationToken.None);

        using var check = factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Tickets
            .AsNoTracking().SingleAsync(t => t.Id == ticket.Id, CancellationToken.None))
            .DepartmentId.Should().BeNull();
    }

    /// <summary>Deleting a product keeps the slide that promoted it, which is left without one.</summary>
    [Fact]
    public async Task DeletingAProductKeepsItsSlide()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var productId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        var slide = Slide.Create(
            new SlideSettings("server", "#336699", "/slides/a.png", null, null, productId, 1, true, SlideAudience.All, null, null),
            DateTimeOffset.UtcNow);
        context.Slides.Add(slide);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = await context.Products.SingleAsync(p => p.Id == productId, CancellationToken.None);
        context.Products.Remove(product);
        await context.SaveChangesAsync(CancellationToken.None);

        using var check = factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Slides
            .AsNoTracking().SingleAsync(s => s.Id == slide.Id, CancellationToken.None))
            .ProductId.Should().BeNull();
    }

    /// <summary>A client's audit entry outlives the client and loses only the link.</summary>
    [Fact]
    public async Task DeletingAClientKeepsItsActivityLogEntry()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var entry = ActivityLog.Create(clientId, "Edited the client", "admin-1", "Admin", "admin@example.test", "203.0.113.1");
        context.ActivityLogs.Add(entry);
        await context.SaveChangesAsync(CancellationToken.None);

        var client = await context.Clients.SingleAsync(c => c.Id == clientId, CancellationToken.None);
        context.Clients.Remove(client);
        await context.SaveChangesAsync(CancellationToken.None);

        using var check = factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<IHostpanelDbContext>().ActivityLogs
            .AsNoTracking().SingleAsync(l => l.Id == entry.Id, CancellationToken.None))
            .ClientId.Should().BeNull();
    }
}
