using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Auth;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;

namespace Innovayse.Hostpanel.Application.Auth.Queries.GetMyAccount;

/// <summary>Resolves the caller's account through the configured provider and reads their local roles.</summary>
public sealed class GetMyAccountHandler
{
    /// <summary>Where this deployment's people are read from.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Local role store; roles are granted here, not by the sign-on provider.</summary>
    private readonly ISubjectRoleStore _roles;

    /// <summary>
    /// Who is calling. Read through the port so that the two spellings of the subject claim stay in
    /// the one adapter that implements it.
    /// </summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Resolves the client account the caller acts for and what they may do on it.</summary>
    private readonly ICallerClientResolver _resolver;

    /// <summary>Reads the external account a client is linked to.</summary>
    private readonly IClientExternalAccounts _externalAccounts;

    /// <summary>Initializes the handler.</summary>
    /// <param name="identity">Where this deployment's people are read from.</param>
    /// <param name="roles">Local role store; roles are granted here, not by the sign-on provider.</param>
    /// <param name="caller">Who is calling.</param>
    /// <param name="resolver">Resolves the client account the caller acts for and what they may do on it.</param>
    /// <param name="externalAccounts">Reads the external account a client is linked to.</param>
    public GetMyAccountHandler(
        IIdentityProvider identity,
        ISubjectRoleStore roles,
        ICurrentUserService caller,
        ICallerClientResolver resolver,
        IClientExternalAccounts externalAccounts)
    {
        _identity = identity;
        _roles = roles;
        _caller = caller;
        _resolver = resolver;
        _externalAccounts = externalAccounts;
    }

    /// <summary>Handles <see cref="GetMyAccountQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The caller's account, or <c>UNAUTHORIZED</c> when no subject or no account answers the credential.</returns>
    public async Task<Result<MyAccountDto>> HandleAsync(GetMyAccountQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var subject = _caller.UserId;
        if (subject is null)
        {
            return Result<MyAccountDto>.FailureCode(SharedErrorCodes.Unauthorized);
        }

        // EmailVerified: the provider's own answer wins (local mode reads the user store); null falls back to the token claim.
        // One lookup: both modes name a person the same way; whatever the configured provider calls them.
        var account = await _identity.FindBySubjectAsync(subject, ct);
        if (account is null)
        {
            return Result<MyAccountDto>.FailureCode(SharedErrorCodes.Unauthorized);
        }

        var held = await _roles.GetRolesAsync(subject, ct);
        var access = await _resolver.ResolveAsync(ct);

        // A plugin member with no client yet carries the account the plugin named; otherwise the link is the
        // one stored on the client, which for a plugin member is the same account the resolver matched.
        var linked = access.Account
            ?? (access.ClientId is { } clientId ? await _externalAccounts.FindAsync(clientId, ct) : null);
        var external = linked is null ? null : new ExternalAccountDto(linked.Provider, linked.Id);

        return Result<MyAccountDto>.Success(new MyAccountDto(
            account.Email,
            held.Select(r => r.ToString()).ToList(),
            account.EmailVerified ?? _caller.IsEmailVerified,
            (int)access.Permissions,
            access.ClientId,
            external));
    }
}
