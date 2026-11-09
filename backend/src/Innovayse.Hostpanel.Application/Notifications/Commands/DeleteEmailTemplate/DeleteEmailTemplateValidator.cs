using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Commands.DeleteEmailTemplate;

/// <summary>Validator for <see cref="DeleteEmailTemplateCommand"/>.</summary>
public sealed class DeleteEmailTemplateValidator : LocalizedValidator<DeleteEmailTemplateCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteEmailTemplateValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["EmailTemplateIdInvalid"]);
    }
}
