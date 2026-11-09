using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Faqs;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Faqs.Common;

/// <summary>
/// Validates one entry of a question's translation map: the locale it is keyed by and the translation
/// itself; shared by the create and update validators.
/// </summary>
public sealed class FaqTranslationValidator : LocalizedValidator<KeyValuePair<string, FaqTranslationDto>>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public FaqTranslationValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Key).Must(SupportedLanguages.IsSupported).WithMessage(L["ContentTranslationLocaleInvalid"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["ContentTranslationsRequired"]);
        RuleFor(x => x.Value.Question).NotEmpty().WithMessage(L["FaqQuestionRequired"])
            .MaximumLength(FaqTranslation.QuestionMaxLength).WithMessage(L["FaqQuestionTooLong", FaqTranslation.QuestionMaxLength])
            .When(x => x.Value is not null);
        RuleFor(x => x.Value.Answer).NotEmpty().WithMessage(L["FaqAnswerRequired"])
            .MaximumLength(FaqTranslation.AnswerMaxLength).WithMessage(L["FaqAnswerTooLong", FaqTranslation.AnswerMaxLength])
            .When(x => x.Value is not null);
    }
}
