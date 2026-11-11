using Innovayse.Hostpanel.Application.Clients.Commands.UpdateClient;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Queries.GetClient;
using Innovayse.Hostpanel.Application.Clients.Queries.GetMyProfile;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Commands.UpdateMyProfile;

/// <summary>
/// Resolves the caller's own client, overlays the provided members on the stored profile, applies
/// the result through the shared write, and answers with the profile as it now stands.
/// </summary>
public sealed class UpdateMyProfileHandler
{
    /// <summary>Wolverine bus used to reach the profile read and the shared write.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Wolverine bus used to reach the profile read and the shared write.</param>
    public UpdateMyProfileHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Handles <see cref="UpdateMyProfileCommand"/>.</summary>
    /// <param name="cmd">The command. It names no account: this updates the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The updated profile, <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no client record,
    /// or any refusal the shared write answers with.
    /// </returns>
    public async Task<Result<MyProfileDto>> HandleAsync(UpdateMyProfileCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var profile = await _bus.InvokeAsync<Result<MyProfileDto>>(new GetMyProfileQuery(), ct);
        if (profile.IsFailure)
        {
            return profile;
        }

        // The caller's own profile carries no staff-owned fields, so the stored record they are
        // written back from is read through the full staff read, by the id the caller resolved to.
        var stored = await _bus.InvokeAsync<Result<ClientDto>>(new GetClientQuery(profile.Value!.Id), ct);
        if (stored.IsFailure)
        {
            return Result<MyProfileDto>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
        }

        // Absent members keep what is stored; the staff-owned fields are not on the command and
        // are always written back unchanged. Email, currency and status stay null, which the shared
        // write reads as "leave it alone", as does a null language. The sign-in address never
        // changes here: that takes the password and a mailed confirmation.
        var current = stored.Value!;
        var update = new UpdateClientCommand(
            current.Id,
            null,
            cmd.FirstName ?? current.FirstName,
            cmd.LastName ?? current.LastName,
            cmd.CompanyName ?? current.CompanyName,
            cmd.Phone ?? current.Phone,
            new PostalAddress(
                cmd.Address?.Street ?? current.Address.Street,
                cmd.Address?.Address2 ?? current.Address.Address2,
                cmd.Address?.City ?? current.Address.City,
                cmd.Address?.State ?? current.Address.State,
                cmd.Address?.PostCode ?? current.Address.PostCode,
                cmd.Address?.Country ?? current.Address.Country),
            current.Preferences with
            {
                Language = cmd.Language,
                Currency = null,
                PaymentMethod = cmd.PaymentMethod ?? current.Preferences.PaymentMethod,
            },
            cmd.Notifications ?? current.Notifications,
            current.Settings,
            null,
            current.ClientGroupId);

        var written = await _bus.InvokeAsync<Result>(update, ct);
        if (written.IsFailure)
        {
            return Result<MyProfileDto>.FailureCode(written.ErrorCode!);
        }

        return await _bus.InvokeAsync<Result<MyProfileDto>>(new GetMyProfileQuery(), ct);
    }
}
