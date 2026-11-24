using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A configurable <see cref="IServerConnectionTester"/> standing in for a real network call to a
/// provisioning server. Answers connected with a fixed account count by default; setting
/// <see cref="ShouldFail"/> answers a failed connection instead, the way an unreachable server
/// would.
/// </summary>
public sealed class FakeServerConnectionTester : IServerConnectionTester
{
    /// <summary>
    /// When <see langword="true"/>, every call answers a failed connection instead of a
    /// successful one.
    /// </summary>
    public bool ShouldFail { get; set; }

    /// <summary>The error code a failed test reports; unreachable unless a test picks another.</summary>
    public string FailureCode { get; set; } = HostpanelErrorCodes.SERVER_CONNECTION_UNREACHABLE;

    /// <summary>The account count a successful test reports.</summary>
    public int AccountsCount { get; set; } = 3;

    /// <summary>The servers this fake was asked to test, in call order.</summary>
    public List<Server> TestedServers { get; } = [];

    /// <inheritdoc/>
    public Task<ServerConnectionTestResult> TestAsync(Server server, CancellationToken ct)
    {
        TestedServers.Add(server);

        return Task.FromResult(ShouldFail
            ? ServerConnectionTestResult.Failed(FailureCode)
            : ServerConnectionTestResult.Succeeded(AccountsCount, "fake-1.0"));
    }
}
