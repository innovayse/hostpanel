namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The per-account limits on one-time codes, whichever door they arrive through (sign-in, enabling,
/// disabling): a cap on wrong codes across every challenge and session, and a guard that a code
/// accepted once is never accepted again within its time window.
/// </summary>
/// <remarks>
/// Every refusal is a plain <c>false</c>, so a caller answers it exactly like a wrong code and
/// cannot tell a locked account from a mistyped digit.
/// </remarks>
public interface ITwoFactorCodeGuard
{
    /// <summary>
    /// Takes one attempt from the account's budget. Call it before checking the code, so concurrent
    /// guesses cannot overshoot; give it back with <see cref="Forgive"/> when the code was right.
    /// </summary>
    /// <param name="userId">The account the code is for.</param>
    /// <returns><c>true</c> when the account is under its cap; <c>false</c> when it is locked.</returns>
    public bool TryBeginAttempt(string userId);

    /// <summary>Gives back the attempt taken by <see cref="TryBeginAttempt"/> after a correct code.</summary>
    /// <param name="userId">The account the code was for.</param>
    public void Forgive(string userId);

    /// <summary>Records the time step of an accepted code, once.</summary>
    /// <param name="userId">The account the code is for.</param>
    /// <param name="timeStep">The time step the code matched.</param>
    /// <returns><c>true</c> when the step is newer than every step accepted for the account; <c>false</c> for a replay.</returns>
    public bool TryAcceptTimeStep(string userId, long timeStep);
}
