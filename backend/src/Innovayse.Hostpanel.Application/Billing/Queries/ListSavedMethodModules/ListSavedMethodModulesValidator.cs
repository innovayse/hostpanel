using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListSavedMethodModules;

/// <summary>Validates <see cref="ListSavedMethodModulesQuery"/>: it carries no input, so there is nothing to refuse.</summary>
public sealed class ListSavedMethodModulesValidator : LocalizedValidator<ListSavedMethodModulesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListSavedMethodModulesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
