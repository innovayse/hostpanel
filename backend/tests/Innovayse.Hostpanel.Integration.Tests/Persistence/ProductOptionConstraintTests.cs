using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The check constraints on <c>ProductOptionGroups</c> and <c>ProductOptions</c> against Postgres. The aggregate
/// already refuses these rows, so the cases write them with raw SQL, around the aggregate, to prove the database
/// refuses them too and names the constraint that did.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProductOptionConstraintTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The Postgres state code of a check constraint violation.</summary>
    private const string CheckViolation = "23514";

    /// <summary>Rows that respect every rule are stored, so the refusals below are the constraints and not the insert.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ValidRows_AreStored()
    {
        var groupId = await SeedGroupAsync();

        await InsertOptionAsync(groupId, 0, 150m, 0m, nameof(ProductOptionPriceType.Fixed));
        await InsertOptionAsync(groupId, 1, 100m, 100m, nameof(ProductOptionPriceType.Percentage));
        await InsertGroupAsync(await SeedProductAsync(), 0, 1, 5);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ProductOptionGroups.Where(g => g.Id == groupId).SelectMany(g => g.Options).CountAsync()).Should().Be(2);
    }

    /// <summary>A negative position is refused on an option.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OptionWithNegativeDisplayOrder_IsRefused()
    {
        var groupId = await SeedGroupAsync();

        var act = () => InsertOptionAsync(groupId, -1, 0m, 0m, nameof(ProductOptionPriceType.Fixed));

        await AssertRefusedAsync(act, "CK_ProductOptions_DisplayOrderNotNegative");
    }

    /// <summary>A negative recurring or setup modifier is refused.</summary>
    /// <param name="price">Recurring modifier to write.</param>
    /// <param name="setup">Setup fee modifier to write.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(-0.01, 0)]
    [InlineData(0, -0.01)]
    public async Task OptionWithNegativeModifier_IsRefused(double price, double setup)
    {
        var groupId = await SeedGroupAsync();

        var act = () => InsertOptionAsync(groupId, 0, (decimal)price, (decimal)setup, nameof(ProductOptionPriceType.Fixed));

        await AssertRefusedAsync(act, "CK_ProductOptions_ModifiersNotNegative");
    }

    /// <summary>A percentage above one hundred is refused for either modifier, while a fixed amount is not capped.</summary>
    /// <param name="price">Recurring modifier to write.</param>
    /// <param name="setup">Setup fee modifier to write.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(100.01, 0)]
    [InlineData(0, 100.01)]
    public async Task PercentageOptionAbove100_IsRefused(double price, double setup)
    {
        var groupId = await SeedGroupAsync();

        var act = () => InsertOptionAsync(groupId, 0, (decimal)price, (decimal)setup, nameof(ProductOptionPriceType.Percentage));

        await AssertRefusedAsync(act, "CK_ProductOptions_PercentageAtMost100");
    }

    /// <summary>A negative position is refused on a group.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GroupWithNegativeDisplayOrder_IsRefused()
    {
        var productId = await SeedProductAsync();

        var act = () => InsertGroupAsync(productId, -1, null, null);

        await AssertRefusedAsync(act, "CK_ProductOptionGroups_DisplayOrderNotNegative");
    }

    /// <summary>A minimum below zero, a maximum below one or a maximum under the minimum is refused. A range with one end missing is the aggregate's to refuse: SQL reads its comparison as unknown, which a check lets through.</summary>
    /// <param name="min">Least amount to write, or null for none.</param>
    /// <param name="max">Most amount to write, or null for none.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(-1, 5)]
    [InlineData(0, 0)]
    [InlineData(5, 4)]
    public async Task GroupWithBrokenQuantityRange_IsRefused(int? min, int? max)
    {
        var productId = await SeedProductAsync();

        var act = () => InsertGroupAsync(productId, 0, min, max);

        await AssertRefusedAsync(act, "CK_ProductOptionGroups_QuantityRange");
    }

    /// <summary>Asserts a write is refused by the named check constraint.</summary>
    /// <param name="act">The write that must fail.</param>
    /// <param name="constraint">The constraint that must name itself in the refusal.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    private static async Task AssertRefusedAsync(Func<Task> act, string constraint)
    {
        var error = (await act.Should().ThrowAsync<PostgresException>()).Which;

        error.SqlState.Should().Be(CheckViolation);
        error.ConstraintName.Should().Be(constraint);
    }

    /// <summary>Creates a product through the API.</summary>
    /// <returns>The new product's id.</returns>
    private async Task<int> SeedProductAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        return await ProductsTestSeeds.CreateProductAsync(admin);
    }

    /// <summary>Creates a product and one option group on it through the API.</summary>
    /// <returns>The new group's id.</returns>
    private async Task<int> SeedGroupAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(admin);
        return await ProductsTestSeeds.CreateOptionGroupAsync(admin, productId);
    }

    /// <summary>Writes an option row with raw SQL, bypassing the aggregate.</summary>
    /// <param name="groupId">The group the row belongs to.</param>
    /// <param name="displayOrder">Position within the group.</param>
    /// <param name="price">Recurring modifier.</param>
    /// <param name="setup">Setup fee modifier.</param>
    /// <param name="priceType">The price type's name, as the column stores it.</param>
    /// <returns>A task that completes when the row is written.</returns>
    private async Task InsertOptionAsync(int groupId, int displayOrder, decimal price, decimal setup, string priceType)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "ProductOptions" ("ProductOptionGroupId", "Name", "PriceModifier", "SetupFeeModifier", "PriceType", "DisplayOrder")
            VALUES ({groupId}, 'Raw', {price}, {setup}, {priceType}, {displayOrder})
            """);
    }

    /// <summary>Writes a group row with raw SQL, bypassing the aggregate.</summary>
    /// <param name="productId">The product the group configures.</param>
    /// <param name="displayOrder">Position within the product.</param>
    /// <param name="min">Least amount, or null for none.</param>
    /// <param name="max">Most amount, or null for none.</param>
    /// <returns>A task that completes when the row is written.</returns>
    private async Task InsertGroupAsync(int productId, int displayOrder, int? min, int? max)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "ProductOptionGroups" ("ProductId", "Name", "Type", "IsRequired", "DisplayOrder", "MinQuantity", "MaxQuantity")
            VALUES ({productId}, 'Raw', {nameof(ProductOptionType.Quantity)}, false, {displayOrder}, {min}, {max})
            """);
    }
}
