using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The API host run on a clock the test moves by hand. Handlers read the moment through
/// <c>IClock</c>, which reads the registered <see cref="TimeProvider"/>, so a scheduled job can be
/// run "days later" without waiting and without backdating rows past what the domain allows.
/// </summary>
internal sealed class ClockedHost : IDisposable
{
    /// <summary>The host running on <see cref="Clock"/>.</summary>
    private readonly WebApplicationFactory<Program> _host;

    /// <summary>Initializes a new instance of the <see cref="ClockedHost"/> class.</summary>
    /// <param name="factory">The shared API + PostgreSQL host the clocked host is derived from.</param>
    public ClockedHost(IntegrationTestFactory factory)
        : this(factory, DateTimeOffset.UtcNow)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ClockedHost"/> class with the clock at a given moment.</summary>
    /// <param name="factory">The shared API + PostgreSQL host the clocked host is derived from.</param>
    /// <param name="start">The moment the clock stands at until the test moves it.</param>
    public ClockedHost(IntegrationTestFactory factory, DateTimeOffset start)
    {
        Clock = new ManualTimeProvider(start);
        _host = factory.WithWebHostBuilder(builder =>
        {
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
