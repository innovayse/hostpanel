using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Provisioning.Extensions;

/// <summary>
/// The one check every provisioning use case that reaches a service's hosting account on the panel
/// makes first, written once here. A terminated service keeps its reference for the record, and
/// the same reference may later belong to a new live service, so the reference alone never says
/// whether an account is there to ask about.
/// </summary>
public static class HostingAccountExtensions
{
    /// <summary>Reads the panel reference of a service whose hosting account is live.</summary>
    /// <param name="service">The service about to be sent to the panel.</param>
    /// <returns>
    /// The reference to ask the panel about, <c>SERVICE_NOT_PROVISIONED</c> when the service has
    /// never been provisioned, or <c>SERVICE_NOT_ACTIVE</c> when it is terminated and its account
    /// removed. A suspended service still holds its account and is accepted.
    /// </returns>
    public static Result<string> RequireHostingAccount(this ClientService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (service.ProvisioningRef is null)
        {
            return Result<string>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_PROVISIONED);
        }

        if (!service.HoldsHostingAccount)
        {
            return Result<string>.FailureCode(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
        }

        return Result<string>.Success(service.ProvisioningRef);
    }
}
