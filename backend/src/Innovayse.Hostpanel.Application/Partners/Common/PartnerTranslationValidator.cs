using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Partners;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Partners.Common;

/// <summary>
/// Validates one entry of a partner's translation map: the locale it is keyed by and the translation
/// itself; shared by the create and update validators.
/// </summary>
public sealed class PartnerTranslationValidator : LocalizedValidator<KeyValuePair<string, PartnerTranslationDto>>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public PartnerTranslationValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Key).Must(SupportedLanguages.IsSupported).WithMessage(L["ContentTranslationLocaleInvalid"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["ContentTranslationsRequired"]);
        RuleFor(x => x.Value.Name).NotEmpty().WithMessage(L["PartnerNameRequired"])
            .MaximumLength(PartnerTranslation.NameMaxLength).WithMessage(L["PartnerNameTooLong", PartnerTranslation.NameMaxLength])
            .When(x => x.Value is not null);
    }
}
