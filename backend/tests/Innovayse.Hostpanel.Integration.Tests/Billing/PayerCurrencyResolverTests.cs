using System.Security.Claims;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="IPayerCurrencyResolver"/> against Postgres: which currency a client, or the current
/// caller, is billed in.
/// </summary>
/// <remarks>
/// The resolver reads <see cref="IHostpanelDbContext"/> and <c>ICurrentUserService</c> directly.
/// The caller-scoped cases drive
/// <c>IHttpContextAccessor</c> with a real <see cref="DefaultHttpContext"/> rather than faking the
/// identity port, so the production <c>HttpCurrentUserService</c> claim reading is exercised too.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PayerCurrencyResolverTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Adds a client with the given recorded currency (or none) and returns its id.</summary>
    /// <param name="context">The data context to add the client to.</param>
    /// <param name="userId">The Identity user id to link the client to.</param>
    /// <param name="currency">The currency code to record, or <see langword="null"/> to leave it unset.</param>
    /// <returns>The new client's primary key.</returns>
    private static async Task<int> SeedClientAsync(IHostpanelDbContext context, string userId, string? currency)
    {
        var client = Client.Create(userId, "Jane", "Doe", $"{userId}@example.test", TestMoments.Seeded);
        if (currency is not null)
        {
            client.SetCurrency(currency);
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client.Id;
    }

    /// <summary>Points the scope's <see cref="IHttpContextAccessor"/> at a signed-in subject.</summary>
    /// <param name="scope">The DI scope the resolver will be pulled from.</param>
    /// <param name="userId">The subject to sign in as.</param>
    private static void SignInAs(IServiceScope scope, string userId)
    {
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "Test"));
        accessor.HttpContext = new DefaultHttpContext { User = principal };
    }

    /// <summary>A client with a currency recorded is billed in it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ForClientAsync_UsesTheClientsCurrency()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await SeedClientAsync(context, $"user-{Guid.NewGuid()}", "AMD");

        var resolver = scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>();
        var currency = await resolver.ForClientAsync(clientId, CancellationToken.None);

        currency.Code.Should().Be("AMD");
    }

    /// <summary>A client with no currency recorded is billed in the base.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ForClientAsync_FallsBackToBaseWhenNoneRecorded()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await SeedClientAsync(context, $"user-{Guid.NewGuid()}", currency: null);

        var resolver = scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>();
        var currency = await resolver.ForClientAsync(clientId, CancellationToken.None);

        currency.Code.Should().Be("USD");
    }

    /// <summary>
    /// A client whose recorded currency is not configured is billed in the base rather than in
    /// something the panel cannot format or charge.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ForClientAsync_FallsBackToBaseWhenRecordedCurrencyIsNotConfigured()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await SeedClientAsync(context, $"user-{Guid.NewGuid()}", "XYZ");

        var resolver = scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>();
        var currency = await resolver.ForClientAsync(clientId, CancellationToken.None);

        currency.Code.Should().Be("USD");
    }

    /// <summary>A client id that names no row is billed in the base, the same as an unset currency.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ForClientAsync_UnknownClientIdFallsBackToBase()
    {
        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>();

        var currency = await resolver.ForClientAsync(int.MaxValue, CancellationToken.None);

        currency.Code.Should().Be("USD");
    }

    /// <summary>An anonymous caller (guest checkout) is billed in the base.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ForCallerAsync_GuestIsBase()
    {
        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>();

        var currency = await resolver.ForCallerAsync(CancellationToken.None);

        currency.Code.Should().Be("USD");
    }

    /// <summary>A signed-in caller is billed in their own client record's currency.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ForCallerAsync_SignedInUsesTheirClient()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var userId = $"user-{Guid.NewGuid()}";
        await SeedClientAsync(context, userId, "AMD");
        SignInAs(scope, userId);

        var resolver = scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>();
        var currency = await resolver.ForCallerAsync(CancellationToken.None);

        currency.Code.Should().Be("AMD");
    }

    /// <summary>The base currency is served directly, no client involved.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task BaseAsync_ReturnsTheConfiguredBase()
    {
        using var scope = factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>();

        var currency = await resolver.BaseAsync(CancellationToken.None);

        currency.Code.Should().Be("USD");
        currency.IsBase.Should().BeTrue();
    }
}
