using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.GetEmailTemplate;

/// <summary>Validates <see cref="GetEmailTemplateQuery"/>.</summary>
public sealed class GetEmailTemplateValidator : LocalizedValidator<GetEmailTemplateQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetEmailTemplateValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["EmailTemplateIdInvalid"]);
    }
}
