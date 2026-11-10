using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builds a host whose clock a test holds and whose data context refuses chosen saves, for the
/// scheduled-job tests that prove one failing item never stops the rest of a pass.
/// </summary>
internal static class FailingSaveHost
{
    /// <summary>Derives a host from the shared one with a fixed clock and a save interceptor.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="clock">The clock every handler of the host reads.</param>
    /// <param name="refuses">Tells whether a pending change set must be refused.</param>
    /// <returns>The derived host; the caller disposes it.</returns>
    public static WebApplicationFactory<Program> Create(
        IntegrationTestFactory factory, ManualTimeProvider clock, Func<ChangeTracker, bool> refuses) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(new FailingSaveInterceptor(refuses)));
        }));
}
