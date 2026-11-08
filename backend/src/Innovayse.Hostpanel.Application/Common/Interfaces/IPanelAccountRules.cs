using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// What a control panel accepts as an account username, asked where staff enter one so a name the
/// panel would refuse fails when it is saved and not when the service is provisioned.
/// </summary>
/// <remarks>
/// A module whose panel has no rule of its own accepts every name, so a rule never blocks a panel it
/// knows nothing about. The panel specific rules live in Infrastructure, behind this port.
/// </remarks>
public interface IPanelAccountRules
{
    /// <summary>Whether the panel can create a new account under the username.</summary>
    /// <param name="module">The control panel module of the server the account is to live on.</param>
    /// <param name="username">The username.</param>
    /// <returns><see langword="true"/> when the panel accepts it for a new account, or has no rule for the module.</returns>
    bool AcceptsNewUsername(ServerModule module, string? username);

    /// <summary>
    /// Whether the panel can be addressed by the username of an account that already exists. Looser
    /// than <see cref="AcceptsNewUsername"/>: an account made by another tool or an older version may
    /// carry a name the panel would no longer create.
    /// </summary>
    /// <param name="module">The control panel module of the server the account lives on.</param>
    /// <param name="username">The account's username.</param>
    /// <returns><see langword="true"/> when the name is safe to send to the panel, or the panel has no rule for the module.</returns>
    bool AcceptsAccountUsername(ServerModule module, string? username);
}
