using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServerModules;

/// <summary>
/// Validates <see cref="ListServerModulesQuery"/>. The query carries no input, so there is no rule to declare; the validator
/// exists so every query has one, and a rule added later lands here.
/// </summary>
public sealed class ListServerModulesValidator : LocalizedValidator<ListServerModulesQuery>
{
    /// <summary>Initializes the validator with the localized messages.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListServerModulesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
