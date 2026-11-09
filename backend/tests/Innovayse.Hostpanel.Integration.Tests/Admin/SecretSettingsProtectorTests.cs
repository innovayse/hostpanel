using Innovayse.Hostpanel.Application.Admin.Seeding;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <see cref="SecretSettingsProtector"/> against Postgres: a secret stored as plain text by an
/// earlier release is encrypted, a plain setting is left alone, and a second run changes nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SecretSettingsProtectorTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A plain secret row is encrypted in place and still reads back as the original value.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureProtected_EncryptsAPlainSecretAsync()
    {
        var secretKey = $"integration:protector-{Guid.NewGuid():N}:api_key";
        var plainKey = $"integration:protector-{Guid.NewGuid():N}:host";
        await SecretSettingTestSeeds.StoreAsync(factory, secretKey, "legacy-plain-key");
        await SecretSettingTestSeeds.StoreAsync(factory, plainKey, "panel.example.test");

        await RunAsync();

        var raw = await SecretSettingTestSeeds.ReadRawAsync(factory, secretKey);
        raw.Should().NotBeNullOrEmpty().And.NotContain("legacy-plain-key");
        (await SecretSettingTestSeeds.ReadAsync(factory, secretKey)).Should().Be("legacy-plain-key");
        (await SecretSettingTestSeeds.ReadRawAsync(factory, plainKey)).Should().Be("panel.example.test");
    }

    /// <summary>A second run finds nothing left to encrypt and leaves the stored value as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureProtected_IsIdempotentAsync()
    {
        var secretKey = $"integration:protector-{Guid.NewGuid():N}:password";
        await SecretSettingTestSeeds.StoreAsync(factory, secretKey, "legacy-plain-password");

        await RunAsync();
        var first = await SecretSettingTestSeeds.ReadRawAsync(factory, secretKey);
        var second = await RunAsync();

        second.Should().Be(0);
        (await SecretSettingTestSeeds.ReadRawAsync(factory, secretKey)).Should().Be(first);
        (await SecretSettingTestSeeds.ReadAsync(factory, secretKey)).Should().Be("legacy-plain-password");
    }

    /// <summary>Runs the protector once in its own scope.</summary>
    /// <returns>The number of settings it encrypted.</returns>
    private async Task<int> RunAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await SecretSettingsProtector.EnsureProtectedAsync(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>(),
            scope.ServiceProvider.GetRequiredService<ISecretSettingCipher>(),
            scope.ServiceProvider.GetRequiredService<IPluginRegistry>(),
            CancellationToken.None);
    }
}
