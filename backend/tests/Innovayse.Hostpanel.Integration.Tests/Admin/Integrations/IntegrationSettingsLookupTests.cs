using Innovayse.Hostpanel.Application.Admin.Integrations.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Integrations;

/// <summary>
/// <see cref="IntegrationSettingsLookup"/>, the reader of stored integration settings that the list,
/// detail and connection-test use cases share, run against the real PostgreSQL host.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class IntegrationSettingsLookupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Stores one field of an integration.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="slug">The integration slug.</param>
    /// <param name="field">The field key.</param>
    /// <param name="value">The value to store.</param>
    private static void Seed(IHostpanelDbContext context, string slug, string field, string value)
    {
        context.Settings.Add(Setting.Create(IntegrationSettingKeys.FieldKey(slug, field), value, null));
    }

    /// <summary>The enabled flag is read case-insensitively and a blank value does not count as filled in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LoadAsync_ReadsTheEnabledFlagAndFieldValuesAsync()
    {
        var slug = $"lookup-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        Seed(context, slug, IntegrationSettingKeys.IsEnabledField, "TRUE");
        Seed(context, slug, "host", "h.example.test");
        Seed(context, slug, "port", "   ");
        await context.SaveChangesAsync(CancellationToken.None);

        var settings = await IntegrationSettingsLookup.LoadAsync(
            context, IntegrationSettingKeys.Prefix(slug), CancellationToken.None);

        settings.IsEnabled(slug).Should().BeTrue();
        settings.GetValue(slug, "host").Should().Be("h.example.test");
        settings.HasValue(slug, "host").Should().BeTrue();
        settings.HasValue(slug, "port").Should().BeFalse();
        settings.GetValue(slug, "missing").Should().BeNull();
        settings.HasValue(slug, "missing").Should().BeFalse();
    }

    /// <summary>An integration with no enabled flag stored is not enabled.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsEnabled_WithoutAFlag_IsFalseAsync()
    {
        var slug = $"lookup-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var settings = await IntegrationSettingsLookup.LoadAsync(
            context, IntegrationSettingKeys.Prefix(slug), CancellationToken.None);

        settings.IsEnabled(slug).Should().BeFalse();
    }

    /// <summary>Loading with one integration's prefix hides every other integration's settings.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LoadAsync_KeepsOnlyTheRequestedPrefixAsync()
    {
        var mine = $"lookup-{Guid.NewGuid():N}";
        var other = $"lookup-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        Seed(context, mine, "host", "mine.example.test");
        Seed(context, other, "host", "other.example.test");
        await context.SaveChangesAsync(CancellationToken.None);

        var settings = await IntegrationSettingsLookup.LoadAsync(
            context, IntegrationSettingKeys.Prefix(mine), CancellationToken.None);

        settings.HasValue(mine, "host").Should().BeTrue();
        settings.HasValue(other, "host").Should().BeFalse();
    }
}
