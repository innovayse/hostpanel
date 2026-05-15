namespace Innovayse.Application.Admin.Servers.Queries.TestServerConnection;

/// <summary>Result of a server connection test.</summary>
/// <param name="Connected">Whether the connection succeeded.</param>
/// <param name="AccountsCount">Total accounts on server, or null if unavailable.</param>
/// <param name="Version">Server software version, or null if unavailable.</param>
/// <param name="ErrorMessage">Error description if connection failed, or null.</param>
/// <param name="TestedAt">UTC timestamp of the test.</param>
public record TestServerConnectionResultDto(
    bool Connected,
    int? AccountsCount,
    string? Version,
    string? ErrorMessage,
    DateTimeOffset TestedAt);
