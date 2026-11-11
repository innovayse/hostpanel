using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Commands.ToggleEmailTemplate;

/// <summary>
/// Validator for <see cref="ToggleEmailTemplateCommand"/>. <c>Active</c> is a flag every value of
/// which is valid, so only <c>Id</c> carries a rule.
/// </summary>
public sealed class ToggleEmailTemplateValidator : LocalizedValidator<ToggleEmailTemplateCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ToggleEmailTemplateValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["EmailTemplateIdInvalid"]);
    }
}
