using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Notifications.Queries.FindEmailTemplate;

/// <summary>Validates <see cref="FindEmailTemplateQuery"/>: the slug is present. The language is free text that falls back to the default.</summary>
public sealed class FindEmailTemplateValidator : LocalizedValidator<FindEmailTemplateQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public FindEmailTemplateValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Slug).NotEmpty().WithMessage(L["EmailTemplateSlugRequired"]);
    }
}
