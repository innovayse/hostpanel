using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Demo.Commands.ResetDemoData;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Auth;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// <see cref="ResetDemoDataHandler"/> against Postgres: on a demo deployment it wipes whatever a
/// visitor left behind and restores the sample data, and it re-arms its own schedule; elsewhere it
/// touches nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ResetDemoDataTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Adds a client the sample data does not contain, standing in for a visitor's work.</summary>
    /// <param name="context">The data context to add through.</param>
    /// <returns>The stray client's id.</returns>
    private static async Task<int> AddStrayClientAsync(IHostpanelDbContext context)
    {
        var stray = Client.Create($"stray-{Guid.NewGuid():N}", "Stray", "Visitor", $"{Guid.NewGuid():N}@example.test", SeededAt);
        context.Clients.Add(stray);
        await context.SaveChangesAsync(CancellationToken.None);
        return stray.Id;
    }

    /// <summary>A reset removes the visitor's rows, restores the sample data and arms the next run.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OnADemo_WipesVisitorDataAndRestoresTheSampleAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var context = provider.GetRequiredService<IHostpanelDbContext>();
        await provider.GetRequiredService<IDemoDataManager>().SeedAsync(CancellationToken.None);
        var invoicesBefore = await context.Invoices.CountAsync();
        var strayId = await AddStrayClientAsync(context);

        await provider.GetRequiredService<IMessageBus>().InvokeAsync(new ResetDemoDataCommand(), CancellationToken.None);

        (await context.Clients.AnyAsync(c => c.Id == strayId)).Should().BeFalse("the visitor's client is wiped");
        (await context.Invoices.CountAsync()).Should().Be(invoicesBefore, "the sample data is deterministic");
        var admin = await provider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync(DemoTestHost.AdminEmail);
        admin.Should().NotBeNull("the published accounts come back after the wipe");
        provider.GetRequiredService<IDemoResetSchedule>().NextResetAt.Should().BeCloseTo(
            DateTimeOffset.UtcNow.AddHours(DemoTestHost.ResetIntervalHours), TimeSpan.FromMinutes(5));
    }

    /// <summary>Outside a demo the command is a no-op, so a stray dispatch cannot wipe a real database.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhenDisabled_TouchesNothingAsync()
    {
        var host = DemoTestHost.EnabledThenSwitchedOff(factory);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IDemoDataManager>()
            .Should().BeOfType<DemoDataManager>("the real manager is registered; only the switch is off");
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var strayId = await AddStrayClientAsync(context);

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new ResetDemoDataCommand(), CancellationToken.None);

        (await context.Clients.AnyAsync(c => c.Id == strayId)).Should().BeTrue();
    }
}
