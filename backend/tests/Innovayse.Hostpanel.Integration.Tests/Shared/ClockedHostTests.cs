using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

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

    /// <summary>What the test configures on the host reaches it, and the clock is still the test's.</summary>
    [Fact]
    public void Configure_IsAppliedToTheHost()
    {
        using var host = new ClockedHost(factory, configure: builder => builder.UseSetting("Orders:InvoicePaymentTermDays", "45"));
        using var scope = host.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IConfiguration>()["Orders:InvoicePaymentTermDays"].Should().Be("45");
        scope.ServiceProvider.GetRequiredService<IClock>().UtcNow.Should().Be(TestMoments.Seeded);
    }

    /// <summary>The registrar stub reports its dates from its host's clock, so a clocked host and the shared host each get their own.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RegistrarStub_ReportsDatesFromTheClockOfItsHost()
    {
        var order = new DomainRenewalOrder("clock.test", "REF", Years: 2);
        using var host = new ClockedHost(factory);
        using var clocked = host.Services.CreateScope();
        using var shared = factory.Services.CreateScope();

        var onClockedHost = await clocked.ServiceProvider.GetRequiredService<IRegistrarProvider>().RenewAsync(order, CancellationToken.None);
        var onSharedHost = await shared.ServiceProvider.GetRequiredService<IRegistrarProvider>().RenewAsync(order, CancellationToken.None);

        onClockedHost.ExpiresAt.Should().Be(TestMoments.Seeded.AddYears(3));
        onSharedHost.ExpiresAt.Should().BeCloseTo(TestMoments.Present().AddYears(3), TimeSpan.FromMinutes(1));
    }

    /// <summary>The shared host keeps the system clock, so tokens and cookies minted against the real time stay valid.</summary>
    [Fact]
    public void SharedHost_KeepsTheSystemClock()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IClock>().UtcNow.Should().BeCloseTo(TestMoments.Present(), TimeSpan.FromMinutes(1));
    }
}
