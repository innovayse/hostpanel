using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// The free-trial settings of a product through <c>POST /api/products</c> and <c>PUT /api/products/{id}</c>:
/// the defaults of a product that has none, storing and returning them, an update that leaves them out
/// keeping them, the reminder days kept in descending order, and the refusals.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProductTrialTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A product created without trial settings has none enabled and carries the defaults.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateProduct_WithoutTrial_HasTheTrialOffWithTheDefaults()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var id = await ProductsTestSeeds.CreateProductAsync(client);

        var trial = (await ProductsTestSeeds.GetProductAsync(client, id)).GetProperty("trial");

        trial.GetProperty("enabled").GetBoolean().Should().BeFalse();
        trial.GetProperty("days").GetInt32().Should().Be(14);
        trial.GetProperty("autoConvert").GetBoolean().Should().BeTrue();
        trial.GetProperty("reminderDays").EnumerateArray().Select(d => d.GetInt32()).Should().Equal(3, 1);
        trial.GetProperty("gracePeriodDays").GetInt32().Should().Be(3);
    }

    /// <summary>The trial settings sent on create are stored and returned, the reminder days in descending order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateProduct_WithTrial_StoresItAndReturnsTheReminderDaysDescending()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var created = await client.PostAsJsonAsync("/api/products", CreateBody(groupId, TrialBody(true, 30, false, [1, 7, 3], 5)));
        var id = await created.Content.ReadFromJsonAsync<int>();

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var trial = (await ProductsTestSeeds.GetProductAsync(client, id)).GetProperty("trial");
        trial.GetProperty("enabled").GetBoolean().Should().BeTrue();
        trial.GetProperty("days").GetInt32().Should().Be(30);
        trial.GetProperty("autoConvert").GetBoolean().Should().BeFalse();
        trial.GetProperty("reminderDays").EnumerateArray().Select(d => d.GetInt32()).Should().Equal(7, 3, 1);
        trial.GetProperty("gracePeriodDays").GetInt32().Should().Be(5);
    }

    /// <summary>An update replaces the trial settings, and an update that leaves them out keeps what is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateProduct_Trial_ReplacesItAndAnUpdateWithoutItKeepsIt()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var id = await ProductsTestSeeds.CreateProductAsync(client);

        var changed = await client.PutAsJsonAsync($"/api/products/{id}", UpdateBody(TrialBody(true, 21, true, [5, 2], 4)));
        changed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterChange = (await ProductsTestSeeds.GetProductAsync(client, id)).GetProperty("trial");
        afterChange.GetProperty("enabled").GetBoolean().Should().BeTrue();
        afterChange.GetProperty("days").GetInt32().Should().Be(21);
        afterChange.GetProperty("reminderDays").EnumerateArray().Select(d => d.GetInt32()).Should().Equal(5, 2);
        afterChange.GetProperty("gracePeriodDays").GetInt32().Should().Be(4);

        var renamed = await client.PutAsJsonAsync($"/api/products/{id}", UpdateBody(null));
        renamed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var afterRename = (await ProductsTestSeeds.GetProductAsync(client, id)).GetProperty("trial");
        afterRename.GetProperty("enabled").GetBoolean().Should().BeTrue();
        afterRename.GetProperty("days").GetInt32().Should().Be(21);
    }

    /// <summary>The trial settings can be turned off again without losing the days.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateProduct_TurningTheTrialOff_KeepsTheOtherSettings()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var id = await ProductsTestSeeds.CreateProductAsync(client);
        (await client.PutAsJsonAsync($"/api/products/{id}", UpdateBody(TrialBody(true, 10, true, [2], 1)))).EnsureSuccessStatusCode();

        (await client.PutAsJsonAsync($"/api/products/{id}", UpdateBody(TrialBody(false, 10, true, [2], 1)))).EnsureSuccessStatusCode();

        var trial = (await ProductsTestSeeds.GetProductAsync(client, id)).GetProperty("trial");
        trial.GetProperty("enabled").GetBoolean().Should().BeFalse();
        trial.GetProperty("days").GetInt32().Should().Be(10);
    }

    /// <summary>Settings outside the allowed ranges are refused on create, naming the trial field.</summary>
    /// <param name="days">The trial length.</param>
    /// <param name="reminders">The reminder days.</param>
    /// <param name="grace">The grace period days.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(0, new[] { 3, 1 }, 3)]
    [InlineData(366, new[] { 3, 1 }, 3)]
    [InlineData(14, new[] { 3, 1 }, -1)]
    [InlineData(14, new[] { 3, 1 }, 31)]
    [InlineData(14, new[] { 14 }, 3)]
    [InlineData(14, new[] { 0 }, 3)]
    [InlineData(14, new[] { 3, 3 }, 3)]
    [InlineData(30, new[] { 6, 5, 4, 3, 2, 1 }, 3)]
    public async Task CreateProduct_TrialOutOfRange_IsRefused(int days, int[] reminders, int grace)
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var groupId = await ProductsTestSeeds.CreateGroupAsync(client);

        var response = await client.PostAsJsonAsync("/api/products", CreateBody(groupId, TrialBody(true, days, true, reminders, grace)));

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "trial");
    }

    /// <summary>The same refusals apply to an update, and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateProduct_TrialOutOfRange_IsRefusedAndKeepsTheStoredSettings()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var id = await ProductsTestSeeds.CreateProductAsync(client);

        var response = await client.PutAsJsonAsync($"/api/products/{id}", UpdateBody(TrialBody(true, 0, true, [1], 3)));

        await ProductsTestSeeds.AssertFieldRefusedAsync(response, "trial");
        (await ProductsTestSeeds.GetProductAsync(client, id)).GetProperty("trial").GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    /// <summary>The body of a create with an optional trial object.</summary>
    /// <param name="groupId">The group the product joins.</param>
    /// <param name="trial">The trial object, or null to leave it out.</param>
    /// <returns>The anonymous body the endpoint binds.</returns>
    private static object CreateBody(int groupId, object? trial) => new
    {
        groupId,
        name = "Trial plan",
        type = ProductType.SharedHosting,
        monthlyPrice = 1m,
        annualPrice = 10m,
        trial,
    };

    /// <summary>The body of an update that keeps the seeded name and prices, with an optional trial object.</summary>
    /// <param name="trial">The trial object, or null to leave it out.</param>
    /// <returns>The anonymous body the endpoint binds.</returns>
    private static object UpdateBody(object? trial) => new
    {
        name = "Featured",
        monthlyPrice = 1m,
        annualPrice = 10m,
        trial,
    };

    /// <summary>A trial object as the wire spells it.</summary>
    /// <param name="enabled">Whether the trial is offered.</param>
    /// <param name="days">The trial length in days.</param>
    /// <param name="autoConvert">Whether the trial converts to a paid service at its end.</param>
    /// <param name="reminderDays">The days before the end a reminder is sent.</param>
    /// <param name="gracePeriodDays">The days a converted trial's invoice may stay unpaid.</param>
    /// <returns>The anonymous trial object.</returns>
    private static object TrialBody(bool enabled, int days, bool autoConvert, int[] reminderDays, int gracePeriodDays) => new
    {
        enabled,
        days,
        autoConvert,
        reminderDays,
        gracePeriodDays,
    };
}
