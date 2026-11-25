using Innovayse.Hostpanel.Application.Provisioning.Common;
using Innovayse.Hostpanel.Application.Provisioning.Queries.GetServiceCredentials;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Provisioning.Queries.GetPanelLoginDetails;

/// <summary>
/// Reads how a person signs in to the panel of a service by hand, for a panel that cannot issue a single sign on link. It asks
/// the one login details read (<see cref="GetServiceCredentialsQuery"/>, which goes through the provider that serves the
/// server) and keeps everything but the password, so a stored password can never reach this route's answer.
/// </summary>
public sealed class GetPanelLoginDetailsHandler
{
    /// <summary>Wolverine bus, used to reach the shared login details read.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="bus">Wolverine bus, used to reach the shared login details read.</param>
    public GetPanelLoginDetailsHandler(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The service to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The details without a password, or the refusal of the shared read: <c>SERVICE_NOT_FOUND</c>, <c>SERVICE_NOT_PROVISIONED</c>,
    /// <c>SERVICE_NOT_ACTIVE</c>, <c>SERVICE_NO_SERVER_ASSIGNED</c>, <c>PANEL_OPERATION_NOT_SUPPORTED</c> when the plugin has no login
    /// capability, the panel's own refusal code, or <c>SERVICE_CREDENTIALS_UPSTREAM_FAILED</c>.
    /// </returns>
    public async Task<Result<PanelLoginDetailsDto>> HandleAsync(GetPanelLoginDetailsQuery query, CancellationToken ct)
    {
        var credentials = await _bus.InvokeAsync<Result<ServiceCredentialsDto>>(new GetServiceCredentialsQuery(query.ServiceId), ct);
        if (credentials.IsFailure)
        {
            return Result<PanelLoginDetailsDto>.FailureCode(credentials.ErrorCode!);
        }

        var value = credentials.Value!;
        return Result<PanelLoginDetailsDto>.Success(new PanelLoginDetailsDto(value.Username, value.Domain, value.ServerIp, value.CpanelUrl));
    }
}
