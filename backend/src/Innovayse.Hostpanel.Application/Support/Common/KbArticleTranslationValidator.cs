using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>Validates one locale of an article's translations: a supported locale, a title and a body.</summary>
public sealed class KbArticleTranslationValidator : LocalizedValidator<KeyValuePair<string, KbArticleTranslationDto>>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Resolves the validation messages in the request's language.</param>
    public KbArticleTranslationValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Key).Must(SupportedLanguages.IsSupported).WithMessage(L["ContentTranslationLocaleInvalid"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["ContentTranslationsRequired"]);
        RuleFor(x => x.Value.Title).NotEmpty().WithMessage(L["KbArticleTitleRequired"])
            .MaximumLength(KbArticleTranslation.TitleMaxLength).WithMessage(L["KbArticleTitleTooLong", KbArticleTranslation.TitleMaxLength])
            .When(x => x.Value is not null);
        RuleFor(x => x.Value.Content).NotEmpty().WithMessage(L["KbArticleContentRequired"])
            .When(x => x.Value is not null);
    }
}
