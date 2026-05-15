namespace Innovayse.Application.Admin.Servers.Queries.TestServerConnection;

/// <summary>Query to test connectivity to a specific server and persist the result.</summary>
/// <param name="ServerId">The server to test.</param>
public record TestServerConnectionQuery(int ServerId);
