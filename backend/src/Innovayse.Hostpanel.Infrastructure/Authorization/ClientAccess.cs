using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;

namespace Innovayse.Hostpanel.Infrastructure.Authorization;

/// <summary>
/// The caller's permissions and account for the client portal, answered from <see cref="ICallerClientResolver"/>.
/// </summary>
public sealed class ClientAccess : IClientAccess
{
    /// <summary>The single source of truth for the caller's account and permissions.</summary>
    private readonly ICallerClientResolver _resolver;

    /// <summary>Who is asking; only to refuse a request that carries no subject.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Initializes the reader.</summary>
    /// <param name="resolver">The single source of truth for the caller's account and permissions.</param>
    /// <param name="caller">Who is asking; only to refuse a request that carries no subject.</param>
    public ClientAccess(ICallerClientResolver resolver, ICurrentUserService caller)
    {
        _resolver = resolver;
        _caller = caller;
    }

    /// <inheritdoc/>
    public async Task<ClientPermission> GetCallerPermissionsAsync(CancellationToken ct)
    {
        var access = await _resolver.ResolveAsync(ct);

        return access.Permissions;
    }

    /// <inheritdoc/>
    public async Task<bool> IsIdentityUnavailableAsync(CancellationToken ct)
    {
        var access = await _resolver.ResolveAsync(ct);

        return access.IdentityUnavailable;
    }

    /// <inheritdoc/>
    public async Task<Result<int>> RequireClientIdAsync(CancellationToken ct)
    {
        _caller.RequireUserId();

        var access = await _resolver.ResolveAsync(ct);

        if (access.IdentityUnavailable)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }

        return access.ClientId is { } clientId
            ? Result<int>.Success(clientId)
            : Result<int>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
    }

    /// <inheritdoc/>
    public async Task<Result<int>> RequireOwnClientIdAsync(CancellationToken ct)
    {
        _caller.RequireUserId();

        var access = await _resolver.ResolveAsync(ct);

        if (access.IdentityUnavailable)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }

        return access is { IsOwner: true, ClientId: { } clientId }
            ? Result<int>.Success(clientId)
            : Result<int>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
    }
}
