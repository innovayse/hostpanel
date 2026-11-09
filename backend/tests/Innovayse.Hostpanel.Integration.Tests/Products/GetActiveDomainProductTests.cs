using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Queries.GetActiveDomainProduct;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <see cref="GetActiveDomainProductQuery"/> through the real <see cref="IMessageBus"/>: the product the
/// Domains module places renew and transfer orders against.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetActiveDomainProductTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>With an active domain product stored, the first one by name is answered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ActiveDomainProduct_ReturnsTheFirstByNameAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = ProductGroup.Create($"domains-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        context.Products.Add(Product.Create(group.Id, "Domain Registration", null, null, null, null, ProductType.Domain, 0m, 0m, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);

        var expected = await context.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active && p.Type == ProductType.Domain)
            .OrderBy(p => p.Name)
            .Select(p => p.Id)
            .FirstAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<DomainProductDto>>(new GetActiveDomainProductQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(expected);
    }

    /// <summary>
    /// With no product stored at all, the query refuses with <c>DOMAIN_PRODUCT_NOT_CONFIGURED</c>. Runs on
    /// a factory of its own, since the shared database holds the domain products other tests add.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_NoActiveDomainProduct_ReturnsDomainProductNotConfiguredAsync()
    {
        await using var isolated = new IntegrationTestFactory();
        await isolated.InitializeAsync();
        using var scope = isolated.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<DomainProductDto>>(new GetActiveDomainProductQuery());

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_PRODUCT_NOT_CONFIGURED);
    }

    /// <summary>
    /// An inactive domain product and an active hosting product are never picked: with only those
    /// stored, the query still refuses with <c>DOMAIN_PRODUCT_NOT_CONFIGURED</c>. Runs on a factory of
    /// its own, since the shared database holds the active domain products other tests add.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_InactiveDomainAndHostingProducts_AreNotPickedAsync()
    {
        await using var isolated = new IntegrationTestFactory();
        await isolated.InitializeAsync();
        using var scope = isolated.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var group = ProductGroup.Create($"domains-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var inactiveDomain = Product.Create(group.Id, "A Domain", null, null, null, null, ProductType.Domain, 0m, 0m, DateTimeOffset.UtcNow);
        inactiveDomain.Deactivate();
        context.Products.Add(inactiveDomain);
        context.Products.Add(Product.Create(group.Id, "A Hosting", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(CancellationToken.None);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<DomainProductDto>>(new GetActiveDomainProductQuery());

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_PRODUCT_NOT_CONFIGURED);
    }
}
