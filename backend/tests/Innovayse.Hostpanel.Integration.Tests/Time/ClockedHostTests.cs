using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Time;

/// <summary>
/// The clocked test host stands at the shared fixed moment, answers it through <see cref="IClock"/> and follows the
/// test when it sets or advances the moment.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClockedHostTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>With no moment named the host starts at the shared fixed moment, and the clock port reports it.</summary>
    [Fact]
    public void Clock_StartsAtTheSharedFixedMoment()
    {
        using var host = new ClockedHost(factory);
        using var scope = host.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IClock>().UtcNow.Should().Be(TestMoments.Seeded);
    }

    /// <summary>Setting and advancing the test clock moves the clock port of the running host.</summary>
    [Fact]
    public void Clock_FollowsSetAndAdvance()
    {
        var moment = new DateTimeOffset(2031, 1, 31, 23, 30, 0, TimeSpan.Zero);
        using var host = new ClockedHost(factory);
        using var scope = host.Services.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        host.Clock.Set(moment);
        clock.UtcNow.Should().Be(moment);

        host.Clock.Advance(TimeSpan.FromHours(1));
        clock.UtcNow.Should().Be(moment.AddHours(1));
    }

    /// <summary>The shared host keeps the system clock, so tokens and cookies minted against the real time stay valid.</summary>
    [Fact]
    public void SharedHost_KeepsTheSystemClock()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IClock>().UtcNow.Should().BeCloseTo(TestMoments.Present(), TimeSpan.FromMinutes(1));
    }
}
