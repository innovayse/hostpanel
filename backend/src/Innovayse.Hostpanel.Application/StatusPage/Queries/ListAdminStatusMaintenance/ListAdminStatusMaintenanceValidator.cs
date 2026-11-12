using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Queries.ListAdminStatusMaintenance;

/// <summary>Validates <see cref="ListAdminStatusMaintenanceQuery"/> before it reaches the handler. The query carries no input, so there are no rules.</summary>
public sealed class ListAdminStatusMaintenanceValidator : LocalizedValidator<ListAdminStatusMaintenanceQuery>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListAdminStatusMaintenanceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
