using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Testimonials;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Testimonials.Common;

/// <summary>
/// Validates one entry of a testimonial's translation map: the locale it is keyed by and the translation
/// itself; shared by the create and update validators.
/// </summary>
public sealed class TestimonialTranslationValidator : LocalizedValidator<KeyValuePair<string, TestimonialTranslationDto>>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public TestimonialTranslationValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Key).Must(SupportedLanguages.IsSupported).WithMessage(L["ContentTranslationLocaleInvalid"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["ContentTranslationsRequired"]);
        RuleFor(x => x.Value.Role).MaximumLength(TestimonialTranslation.RoleMaxLength).WithMessage(L["TestimonialRoleTooLong", TestimonialTranslation.RoleMaxLength])
            .When(x => x.Value is not null);
        RuleFor(x => x.Value.Quote).NotEmpty().WithMessage(L["TestimonialQuoteRequired"])
            .MaximumLength(TestimonialTranslation.QuoteMaxLength).WithMessage(L["TestimonialQuoteTooLong", TestimonialTranslation.QuoteMaxLength])
            .When(x => x.Value is not null);
    }
}
