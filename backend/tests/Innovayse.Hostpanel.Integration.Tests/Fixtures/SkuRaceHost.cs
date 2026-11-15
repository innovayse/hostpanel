using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builds a host whose data context loses one SKU race, for the create and update product tests that
/// prove a refusal by the unique index answers <c>PRODUCT_SKU_ALREADY_IN_USE</c> and never a 500.
/// </summary>
internal static class SkuRaceHost
{
    /// <summary>Derives a host from the shared one with <paramref name="race"/> on every data context.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="race">The interceptor that commits the competing product before the handler's save.</param>
    /// <returns>The derived host; the caller disposes it.</returns>
    public static WebApplicationFactory<Program> Create(IntegrationTestFactory factory, SkuRaceSaveInterceptor race) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(race))));
}
