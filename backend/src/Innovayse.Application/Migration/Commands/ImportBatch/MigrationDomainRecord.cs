namespace Innovayse.Application.Migration.Commands.ImportBatch;

/// <summary>A single domain record.</summary>
/// <param name="ClientEmail">Email of the client who owns the domain.</param>
/// <param name="DomainName">Fully-qualified domain name.</param>
/// <param name="RegisteredAt">Domain registration date.</param>
/// <param name="ExpiresAt">Domain expiry date.</param>
public sealed record MigrationDomainRecord(
    string ClientEmail, string DomainName, DateTimeOffset RegisteredAt, DateTimeOffset ExpiresAt);
