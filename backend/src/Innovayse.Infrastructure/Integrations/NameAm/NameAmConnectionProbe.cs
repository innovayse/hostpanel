namespace Innovayse.Infrastructure.Integrations.NameAm;

using Innovayse.Application.Admin.Integrations;

/// <summary>
/// Checks Name.am credentials by logging in with the values saved on the admin Integrations page.
/// </summary>
/// <param name="client">Name.am API client.</param>
public sealed class NameAmConnectionProbe(NameAmClient client) : IIntegrationConnectionProbe
{
    /// <inheritdoc/>
    public string Slug => "nameam";

    /// <inheritdoc/>
    public Task ProbeAsync(CancellationToken ct) => client.VerifyCredentialsAsync(ct);
}
