using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;

namespace Innovayse.Hostpanel.Infrastructure.Provisioning;

/// <summary>
/// The username rules of the control panels this build drives: cPanel through
/// <see cref="WhmInputRules"/>, the same rules the cPanel provider applies when it provisions, so the
/// two cannot drift apart. Every other module accepts every name.
/// </summary>
public sealed class PanelAccountRules : IPanelAccountRules
{
    /// <summary>Whether the panel can create a new account under the username.</summary>
    /// <param name="module">The control panel module of the server the account is to live on.</param>
    /// <param name="username">The username.</param>
    /// <returns><see langword="true"/> when the panel accepts it for a new account, or has no rule for the module.</returns>
    public bool AcceptsNewUsername(ServerModule module, string? username)
    {
        return module is not ServerModule.CPanel || WhmInputRules.IsNewUsername(username);
    }

    /// <summary>Whether the panel can be addressed by the username of an account that already exists.</summary>
    /// <param name="module">The control panel module of the server the account lives on.</param>
    /// <param name="username">The account's username.</param>
    /// <returns><see langword="true"/> when the name is safe to send to the panel, or the panel has no rule for the module.</returns>
    public bool AcceptsAccountUsername(ServerModule module, string? username)
    {
        return module is not ServerModule.CPanel || WhmInputRules.IsAccountReference(username);
    }
}
