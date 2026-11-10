using Innovayse.Hostpanel.Application.Auth.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The pending token between the password step and the one-time-code step of a local sign-in.
/// </summary>
/// <remarks>
/// A pending token proves that the password step succeeded, so it must be unforgeable, short-lived,
/// spendable once and impossible to brute force against: anyone who can mint or replay one can guess
/// the six-digit code without ever knowing the password. Every refusal is the same silent
/// <c>null</c>/<c>false</c>, so a caller cannot tell a forged token from an expired or a spent one.
/// </remarks>
public interface ITwoFactorChallengeService
{
    /// <summary>Opens a challenge for an account whose password has just been verified.</summary>
    /// <param name="userId">The account that passed the password step.</param>
    /// <returns>An opaque, signed, encrypted, expiring token to hand to the client.</returns>
    public string Issue(string userId);

    /// <summary>Reads a pending token without spending anything.</summary>
    /// <param name="token">The token the client presented.</param>
    /// <returns>The open challenge; <c>null</c> when the token is forged, tampered with, expired, spent or out of attempts.</returns>
    public TwoFactorChallengeDto? TryRead(string? token);

    /// <summary>
    /// Takes one attempt from a challenge's budget. Call it before checking the code, so concurrent
    /// guesses cannot overshoot the budget.
    /// </summary>
    /// <param name="challenge">The challenge returned by <see cref="TryRead"/>.</param>
    /// <returns><c>true</c> when an attempt was available; <c>false</c> when the challenge is dead.</returns>
    public bool TryBeginAttempt(TwoFactorChallengeDto challenge);

    /// <summary>Spends the challenge after a correct code, so the same token cannot sign in twice.</summary>
    /// <param name="challenge">The challenge that was just completed.</param>
    public void Complete(TwoFactorChallengeDto challenge);
}
