using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Services.Commands.AddMySshKey;

/// <summary>
/// Validates a public key and installs it on the server the caller's service is assigned to, through
/// <see cref="SshKeyInstaller"/>, the one path a key reaches an account by.
/// </summary>
public sealed class AddMySshKeyHandler
{
    /// <summary>The rule that says a client may only touch their own services.</summary>
    private readonly IServiceOwnership _ownership;

    /// <summary>Resolves server, provider and account once ownership is settled.</summary>
    private readonly ISshServiceContextResolver _resolver;

    /// <summary>Records the key and installs it on the account.</summary>
    private readonly SshKeyInstaller _installer;

    /// <summary>Data context the activity line is saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Stages the activity log line in the same save.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Initialises the handler.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own services.</param>
    /// <param name="resolver">Resolves server, provider and account once ownership is settled.</param>
    /// <param name="installer">Records the key and installs it on the account.</param>
    /// <param name="context">Data context the activity line is saved through.</param>
    /// <param name="activity">Stages the activity log line in the same save.</param>
    public AddMySshKeyHandler(
        IServiceOwnership ownership, ISshServiceContextResolver resolver, SshKeyInstaller installer,
        IHostpanelDbContext context, IActivityLogger activity)
    {
        _ownership = ownership;
        _resolver = resolver;
        _installer = installer;
        _context = context;
        _activity = activity;
    }

    /// <summary>Handles <see cref="AddMySshKeyCommand"/>.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The installed key, or one of <c>SERVICE_NOT_FOUND</c>, <c>SSH_ACCESS_UNAVAILABLE</c>,
    /// <c>SSH_KEY_INVALID</c>, <c>SSH_PRIVATE_KEY_REJECTED</c>, <c>SSH_KEY_ALREADY_INSTALLED</c>,
    /// <c>SSH_KEY_LIMIT_REACHED</c>, <c>SSH_SERVER_UNAVAILABLE</c>.
    /// </returns>
    public async Task<Result<SshKeyDto>> HandleAsync(AddMySshKeyCommand cmd, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(cmd.ServiceId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<SshKeyDto>.FailureCode(ownershipOutcome.ErrorCode ?? HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        var context = await _resolver.ResolveAsync(cmd.ServiceId, ct);
        if (context is null)
        {
            return Result<SshKeyDto>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_FOUND);
        }

        if (context.State is not SshAccessState.Available)
        {
            return Result<SshKeyDto>.FailureCode(HostpanelErrorCodes.SSH_ACCESS_UNAVAILABLE);
        }

        if (!SshPublicKey.TryParse(cmd.PublicKey, out var parsed, out var isPrivate) || parsed is null)
        {
            return Result<SshKeyDto>.FailureCode(isPrivate
                ? HostpanelErrorCodes.SSH_PRIVATE_KEY_REJECTED
                : HostpanelErrorCodes.SSH_KEY_INVALID);
        }

        var installed = await _installer.InstallAsync(context, cmd.Label, parsed, issuedToPluginId: null, ct);
        if (installed.IsFailure || installed.Value is not { } key)
        {
            return Result<SshKeyDto>.FailureCode(installed.ErrorCode ?? HostpanelErrorCodes.SSH_SERVER_UNAVAILABLE);
        }

        await _activity.RecordAsync(
            new ActivityLogEntry(
                context.Service.ClientId,
                $"Added SSH key \"{key.Label}\" ({key.Fingerprint}) to service #{context.Service.Id}"),
            ct);
        await _context.SaveChangesAsync(ct);

        return Result<SshKeyDto>.Success(key.ToDto(context, presentOnServer: true));
    }
}
