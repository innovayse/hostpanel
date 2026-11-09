using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Application.Support.Common;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Commands.CreateKbArticle;

/// <summary>Validates <see cref="CreateKbArticleCommand"/>.</summary>
public sealed class CreateKbArticleValidator : LocalizedValidator<CreateKbArticleCommand>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Resolves the validation messages in the request's language.</param>
    public CreateKbArticleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.CategoryId).MustBePositiveId(L["KbCategoryIdInvalid"]);
        RuleFor(x => x.Translations).NotEmpty().WithMessage(L["ContentTranslationsRequired"]);
        RuleFor(x => x.Translations).MustIncludeDefaultLocale(L["ContentDefaultTranslationRequired"]);
        RuleForEach(x => x.Translations).SetValidator(new KbArticleTranslationValidator(localizer));
    }
}
