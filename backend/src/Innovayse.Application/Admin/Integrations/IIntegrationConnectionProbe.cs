namespace Innovayse.Application.Admin.Integrations;

/// <summary>
/// A live check of a built-in integration's credentials against the provider itself, run by
/// "Test Connection" once every required field is filled in.
/// </summary>
public interface IIntegrationConnectionProbe
{
    /// <summary>Gets the integration slug this probe checks (e.g. <c>nameam</c>).</summary>
    string Slug { get; }

    /// <summary>
    /// Calls the provider with the stored credentials.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the provider accepted the credentials.</returns>
    /// <exception cref="Exception">Thrown when the provider is unreachable or rejects the credentials.</exception>
    Task ProbeAsync(CancellationToken ct);
}
