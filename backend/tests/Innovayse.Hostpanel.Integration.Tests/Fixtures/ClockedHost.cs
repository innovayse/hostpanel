using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The API host run on a clock the test moves by hand. Handlers read the moment through
/// <c>IClock</c>, which reads the registered <see cref="TimeProvider"/>, so a scheduled job can be
/// run "days later" without waiting and without backdating rows past what the domain allows. The clock
/// starts at <see cref="TestMoments.Seeded"/> unless the test names a moment, and the test sets it
/// (<see cref="ManualTimeProvider.Set"/>) or advances it. Only the clocked host is fixed: the shared host
/// keeps the system clock, because the tokens and cookies the tests mint and check against the real time would
/// otherwise be born expired or never expire.
/// </summary>
/// <remarks>
/// A job that runs on this host (a cron the test dispatches) schedules its next run as a delay measured on this clock
/// (<c>RecurringJobs</c>), so a clock standing in the past never makes that run due at once and the job is not repeated
/// behind the test's back. The host it derives is its own and is disposed with it, together with everything the test
/// configured on it through <c>configure</c>: a test that needs a changed host says so here instead of building an
/// intermediate host to wrap.
/// </remarks>
internal sealed class ClockedHost : IDisposable
{
    /// <summary>The host running on <see cref="Clock"/>.</summary>
    private readonly WebApplicationFactory<Program> _host;

    /// <summary>Initializes a new instance of the <see cref="ClockedHost"/> class.</summary>
    /// <param name="factory">The shared API + PostgreSQL host, or a host already derived from it that the caller owns, the clocked host is derived from.</param>
    /// <param name="start">The moment the clock stands at until the test moves it; <see cref="TestMoments.Seeded"/> when omitted.</param>
    /// <param name="zone">The time zone the host reports as its own; the real local one when omitted.</param>
    /// <param name="configure">What else the test changes on the host (a setting, a fake in place of an adapter); nothing when omitted.</param>
    public ClockedHost(
        WebApplicationFactory<Program> factory,
        DateTimeOffset? start = null,
        TimeZoneInfo? zone = null,
        Action<IWebHostBuilder>? configure = null)
    {
        Clock = new ManualTimeProvider(start ?? TestMoments.Seeded, zone);
        _host = factory.WithWebHostBuilder(builder =>
        {
            configure?.Invoke(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
            });
        });
    }

    /// <summary>Gets the clock the host's handlers read.</summary>
    public ManualTimeProvider Clock { get; }

    /// <summary>Gets the services of the clocked host.</summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>Creates a client for the clocked host.</summary>
    /// <returns>An unauthenticated client.</returns>
    public HttpClient CreateClient()
    {
        return _host.CreateClient();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _host.Dispose();
    }
}
