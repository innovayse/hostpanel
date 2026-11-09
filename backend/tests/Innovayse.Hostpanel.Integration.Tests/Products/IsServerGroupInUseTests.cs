using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Queries.IsServerGroupInUse;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary><see cref="IsServerGroupInUseQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class IsServerGroupInUseTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A group a product provisions into is reported in use.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_GroupUsedByAProduct_IsInUseAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var groupId = await ServersTestSeeds.CreateServerGroupAsync(client, $"Group-{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var productGroup = ProductGroup.Create($"group-in-use-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(productGroup);
            await context.SaveChangesAsync(CancellationToken.None);
            context.Products.Add(Product.Create(
                productGroup.Id, $"Plan {Guid.NewGuid():N}", null, null, null, "plan", ProductType.SharedHosting, 0m, 0m,
                DateTimeOffset.UtcNow, serverGroupId: groupId));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var result = await AskAsync(groupId);

        result.Value.Should().BeTrue();
    }

    /// <summary>A group no product names is not in use.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_UnreferencedGroup_IsNotInUseAsync()
    {
        var client = await ServersTestSeeds.AdminClientAsync(factory);
        var groupId = await ServersTestSeeds.CreateServerGroupAsync(client, $"Group-{Guid.NewGuid():N}");

        var result = await AskAsync(groupId);

        result.Value.Should().BeFalse();
    }

    /// <summary>Dispatches the query through the bus in its own scope.</summary>
    /// <param name="groupId">The group to check.</param>
    /// <returns>The query's answer.</returns>
    private async Task<Result<bool>> AskAsync(int groupId)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        return await bus.InvokeAsync<Result<bool>>(new IsServerGroupInUseQuery(groupId));
    }
}
