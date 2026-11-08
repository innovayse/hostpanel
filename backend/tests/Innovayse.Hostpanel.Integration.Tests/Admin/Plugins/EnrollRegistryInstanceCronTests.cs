using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.EnrollRegistryInstanceCron;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>EnrollRegistryInstanceCronCommand</c>, the recurring job that registers this installation with the registry and confirms it
/// without anyone pressing a button: a pass registers and verifies; a pass while the registry is off, or after the identity is
/// already verified, calls nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class EnrollRegistryInstanceCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Runs one pass of the job on a host.</summary>
    /// <param name="host">The host.</param>
    /// <returns>A task that completes when the pass ended.</returns>
    private static async Task RunPassAsync(RegistryTestHost host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new EnrollRegistryInstanceCronCommand());
    }

    /// <summary>Reads the state of a host's identity.</summary>
    /// <param name="host">The host.</param>
    /// <returns>The state as the admin page shows it.</returns>
    private static async Task<RegistryInstanceDto> StatusAsync(RegistryTestHost host)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RegistryInstanceEnrollment>().GetStatusAsync(CancellationToken.None);
    }

    /// <summary>One pass registers the installation and the registry confirms it through the well-known address.</summary>
    [Fact]
    public async Task APass_RegistersAndVerifiesTheInstallationAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);

        await RunPassAsync(host);

        var status = await StatusAsync(host);
        status.State.Should().Be(RegistryInstanceState.Verified);
        status.InstanceId.Should().Be(registry.Instances.Single().Id);
        registry.Instances.Single().Verified.Should().BeTrue();
    }

    /// <summary>A second pass finds the identity verified and calls nothing.</summary>
    [Fact]
    public async Task APassOnAVerifiedIdentity_CallsNothingAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry);
        await RunPassAsync(host);
        var calls = registry.Calls.Count;

        await RunPassAsync(host);

        registry.Calls.Should().HaveCount(calls);
    }

    /// <summary>While the registry is switched off a pass calls nothing and stores nothing.</summary>
    [Fact]
    public async Task WhileTheRegistryIsOff_APassCallsNothingAsync()
    {
        var registry = new FakeRegistryServer();
        using var host = new RegistryTestHost(factory, registry, enabled: false);

        await RunPassAsync(host);

        registry.Calls.Should().BeEmpty();
        (await StatusAsync(host)).State.Should().Be(RegistryInstanceState.NotRegistered);
    }
}
