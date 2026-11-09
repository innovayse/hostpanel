using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary><see cref="RenewalSettingsReader"/> against seeded and edited rows.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RenewalSettingsReaderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The seeded defaults read back typed; a garbage row falls back to its default.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Reads_Defaults_And_FallsBack()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var reader = scope.ServiceProvider.GetRequiredService<RenewalSettingsReader>();

        var settings = await reader.ReadAsync(CancellationToken.None);
        settings.Should().Be(new RenewalSettings(14, [7, 3, 1], true, 3, 14), "seeded defaults");

        var row = await context.Settings.FirstAsync(s => s.Key == BillingSettingKeys.InvoiceDaysBefore);
        row.UpdateValue("not-a-number");
        await context.SaveChangesAsync(CancellationToken.None);
        try
        {
            (await reader.ReadAsync(CancellationToken.None)).InvoiceDaysBefore.Should().Be(14);
        }
        finally
        {
            row.UpdateValue("14");
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
