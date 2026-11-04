using System.Text;
using Innovayse.Hostpanel.Shared.Models.Interfaces;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Application.Auth.Common;

/// <summary>
/// A password sign-in that is not finished: the account has a second factor, so no session is
/// opened yet and the caller holds a pending token to present with the one-time code.
/// </summary>
/// <param name="TwoFactorRequired">Always <see langword="true"/>; it is how a client tells this answer from a finished sign-in.</param>
/// <param name="PendingToken">The signed, expiring, single-use token that proves the password step passed.</param>
public sealed record TwoFactorPendingDto(bool TwoFactorRequired, string PendingToken) : ISessionGrant
{
    /// <summary>No token: a session opens only once the second factor is verified.</summary>
    string? ISessionGrant.SessionToken => null;

    /// <summary>Prints the record without its secret members; see <see cref="SecretMembers"/>.</summary>
    /// <param name="builder">The builder the generated <c>ToString</c> is assembling.</param>
    /// <returns><see langword="true"/> when at least one member was written.</returns>
    private bool PrintMembers(StringBuilder builder)
    {
        return SecretMembers.Print(this, builder);
    }
}
