namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Ends the server-side sessions this product holds for a person, on the sign-on service's word
/// rather than the person's.
/// </summary>
public interface ISessionRevoker
{
    /// <summary>Ends every session held for a subject. Revoking a subject with none is not an error.</summary>
    /// <param name="subject">How the configured identity provider names the person.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the sessions are gone.</returns>
    public Task RevokeAllAsync(string subject, CancellationToken ct);
}
