using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Settings;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;

/// <summary>
/// cPanel WHM implementation of <see cref="IProvisioningProvider"/>.
/// Delegates all provisioning operations to <see cref="CPanelClient"/>.
/// </summary>
/// <remarks>
/// A panel refusal is not always a failure: a retry after a lost answer meets the account the
/// first attempt already made, and a suspend, unsuspend or terminate can find the account
/// already in the wanted state. After a refusal the provider therefore asks WHM what the account
/// looks like and treats "already as asked" as success; any other refusal is rethrown.
/// </remarks>
public sealed class CPanelProvisioningProvider : IProvisioningProvider
{
    /// <summary>The WHM API client every provisioning call goes through.</summary>
    private readonly CPanelClient _client;

    /// <summary>Resolved cPanel configuration settings.</summary>
    private readonly CPanelOptions _settings;

    /// <summary>Initializes a new instance of the <see cref="CPanelProvisioningProvider"/> class.</summary>
    /// <param name="client">The WHM API client every provisioning call goes through.</param>
    /// <param name="options">The single-server cPanel settings bound at startup.</param>
    public CPanelProvisioningProvider(CPanelClient client, IOptions<CPanelOptions> options)
    {
        _client = client;
        _settings = options.Value;
    }

    /// <inheritdoc/>
    public async Task<ProvisioningResult> ProvisionAsync(ProvisionAccountOrder req, CancellationToken ct)
    {
        try
        {
            await _client.CreateAccountAsync(
                req.DomainName,
                req.Username,
                req.Password,
                req.Package,
                ct);
        }
        catch (InvalidOperationException)
        {
            var existing = await TryFindAccountAsync(req.Username, ct);
            if (existing is null)
            {
                throw;
            }

            return ProvisioningResult.AlreadyExists(existing.Username, existing.Domain);
        }

        return ProvisioningResult.Created(req.Username);
    }

    /// <inheritdoc/>
    public async Task SuspendAsync(SuspendAccountOrder req, CancellationToken ct)
    {
        try
        {
            await _client.SuspendAccountAsync(req.ProvisioningRef, req.Reason, ct);
        }
        catch (InvalidOperationException)
        {
            var existing = await TryFindAccountAsync(req.ProvisioningRef, ct);
            if (existing is not { Suspended: true })
            {
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task UnsuspendAsync(string provisioningRef, CancellationToken ct)
    {
        try
        {
            await _client.UnsuspendAccountAsync(provisioningRef, ct);
        }
        catch (InvalidOperationException)
        {
            var existing = await TryFindAccountAsync(provisioningRef, ct);
            if (existing is not { Suspended: false })
            {
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task TerminateAsync(TerminateAccountOrder req, CancellationToken ct)
    {
        try
        {
            await _client.RemoveAccountAsync(req.ProvisioningRef, ct);
        }
        catch (InvalidOperationException)
        {
            if (!await IsAccountAbsentAsync(req.ProvisioningRef, ct))
            {
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ServiceCredentials> GetCredentialsAsync(string provisioningRef, CancellationToken ct)
    {
        // WHM is the source of truth for the account's primary domain. A missing account is a
        // failure of the read, never a made-up domain.
        var account = await _client.FindAccountAsync(provisioningRef, ct)
            ?? throw new InvalidOperationException("The hosting account was not found on the server.");

        // cPanel does not expose stored passwords via the API, so no password is returned.
        return new ServiceCredentials(
            account.Username,
            null,
            account.Domain,
            _settings.ServerIp,
            $"{_settings.ApiUrl.TrimEnd('/')}/cpanel");
    }

    /// <inheritdoc/>
    public Task<string> GetCPanelSsoUrlAsync(string provisioningRef, string? callerIp, CancellationToken ct)
    {
        return _client.GetCPanelSsoUrlAsync(provisioningRef, ct);
    }

    /// <inheritdoc/>
    public async Task ChangePasswordAsync(string provisioningRef, string newPassword, CancellationToken ct)
    {
        await _client.ChangePasswordAsync(provisioningRef, newPassword, ct);
    }

    /// <inheritdoc/>
    public async Task ChangePackageAsync(string provisioningRef, string newPackage, CancellationToken ct)
    {
        await _client.ChangePackageAsync(provisioningRef, newPackage, ct);
    }

    /// <summary>
    /// Looks an account up after a refusal, treating a lookup that fails as "state unknown".
    /// </summary>
    /// <param name="username">The account to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The account, or <see langword="null"/> when it does not exist or the lookup was refused.</returns>
    private async Task<WhmAccountSummary?> TryFindAccountAsync(string username, CancellationToken ct)
    {
        try
        {
            return await _client.FindAccountAsync(username, ct);
        }
        catch (InvalidOperationException)
        {
            // The refusal is already logged by the client; the caller rethrows the first failure,
            // which is the one that explains what went wrong.
            return null;
        }
    }

    /// <summary>
    /// Confirms WHM positively reports no such account, so a failed removal can be read as
    /// "already removed".
    /// </summary>
    /// <param name="username">The account that was to be removed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> only when the lookup succeeded and found nothing.</returns>
    private async Task<bool> IsAccountAbsentAsync(string username, CancellationToken ct)
    {
        try
        {
            return await _client.FindAccountAsync(username, ct) is null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
