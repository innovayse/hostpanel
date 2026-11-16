using Innovayse.Hostpanel.Infrastructure.Resilience.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Infrastructure.Currency.Extensions;

/// <summary>
/// The one registration of the two bank clients, used by the application and by the tests that prove
/// what it does, so a test can never describe a chain the application does not build.
/// </summary>
public static class ExchangeRateHttpClientExtensions
{
    /// <summary>
    /// Registers the typed clients of <see cref="CbaExchangeRateSource"/> and <see cref="EcbExchangeRateSource"/>:
    /// the bank's origin, the body cap of <see cref="ExchangeRateFeed"/>, and the bank's own read only resilience profile.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <remarks>
    /// Each call is one idempotent GET the daily job or an operator makes, so a retry costs nothing; the
    /// profiles are argued on <c>HttpResilienceOptions.Cba</c> and <c>HttpResilienceOptions.Ecb</c>.
    /// </remarks>
    public static IServiceCollection AddExchangeRateHttpClients(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<CbaExchangeRateSource>(client =>
        {
            ExchangeRateFeed.Configure(client, new Uri(CbaExchangeRateSource.BaseAddress));
        })
            .AddReadOnlyResilience(o => o.Cba);
        services.AddHttpClient<EcbExchangeRateSource>(client =>
        {
            ExchangeRateFeed.Configure(client, new Uri(EcbExchangeRateSource.BaseAddress));
        })
            .AddReadOnlyResilience(o => o.Ecb);

        return services;
    }
}
