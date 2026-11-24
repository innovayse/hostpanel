namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>Result of a server connection test.</summary>
/// <param name="Connected">Whether the connection succeeded.</param>
/// <param name="AccountsCount">Total accounts on server, or null if unavailable.</param>
/// <param name="Version">Server software version, or null if unavailable.</param>
/// <param name="ErrorCode">The error code naming why the connection failed (the same code an error response carries), or null when it connected.</param>
/// <param name="ErrorMessage">The sentence for <paramref name="ErrorCode"/> in the request language, or null when it connected.</param>
/// <param name="TestedAt">UTC timestamp of the test.</param>
public sealed record TestServerConnectionResultDto(
    bool Connected,
    int? AccountsCount,
    string? Version,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset TestedAt);
