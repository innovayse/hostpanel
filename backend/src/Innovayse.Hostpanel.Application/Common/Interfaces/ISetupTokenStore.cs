namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Issues, checks and retires the one-time setup token that guards the first Admin claim on a local-mode
/// install. The token is kept among the settings, which the Admin module owns, so the Auth use
/// case reaches it through this port rather than reading the settings table itself.
/// </summary>
public interface ISetupTokenStore
{
    /// <summary>
    /// Returns the outstanding setup token or, when none is outstanding (no row, or a row blanked
    /// by a completed setup), stores <paramref name="candidate"/> as the new one and saves at once.
    /// </summary>
    /// <param name="candidate">A freshly generated token, stored only when none is outstanding.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The token that is outstanding after the call.</returns>
    public Task<string> GetOrIssueAsync(string candidate, CancellationToken ct);

    /// <summary>
    /// Compares the presented token with the outstanding one in fixed time and, on a match, blanks
    /// it and saves at once, so a failure after this call leaves the token spent.
    /// </summary>
    /// <param name="presented">The token the caller sent, if any.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the token matched and is now retired; <see langword="false"/>
    /// when none was presented, none is outstanding, or it does not match.
    /// </returns>
    public Task<bool> TryConsumeAsync(string? presented, CancellationToken ct);
}
