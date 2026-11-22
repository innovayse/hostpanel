namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>The outcome of probing a provisioning server's control-panel API.</summary>
/// <param name="Connected">Whether the server answered with the stored credentials.</param>
/// <param name="AccountsCount">The account (or package) count the server reported; null when it did not answer.</param>
/// <param name="Version">The panel version the server reported, when it reports one.</param>
/// <param name="ErrorCode">The error code naming why the probe failed (resolved to a sentence in the request language at the boundary); null when it succeeded.</param>
public sealed record ServerConnectionTestResult(
    bool Connected,
    int? AccountsCount,
    string? Version,
    string? ErrorCode)
{
    /// <summary>A probe the server answered.</summary>
    /// <param name="accountsCount">The account (or package) count the server reported, or <see langword="null"/> when the panel reports none.</param>
    /// <param name="version">The panel version the server reported, when it reports one.</param>
    /// <returns>A connected result.</returns>
    public static ServerConnectionTestResult Succeeded(int? accountsCount, string? version) =>
        new(true, accountsCount, version, null);

    /// <summary>A probe that could not reach the server or could not run at all.</summary>
    /// <param name="errorCode">The error code naming why the probe failed.</param>
    /// <returns>A disconnected result.</returns>
    public static ServerConnectionTestResult Failed(string errorCode) =>
        new(false, null, null, errorCode);
}
