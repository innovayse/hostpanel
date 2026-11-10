using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Projects;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Projects.Common;

/// <summary>
/// Validates one entry of a project's translation map: the locale it is keyed by and the translation
/// itself; shared by the create and update validators.
/// </summary>
public sealed class ProjectTranslationValidator : LocalizedValidator<KeyValuePair<string, ProjectTranslationDto>>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ProjectTranslationValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Key).Must(SupportedLanguages.IsSupported).WithMessage(L["ContentTranslationLocaleInvalid"]);
        RuleFor(x => x.Value).NotNull().WithMessage(L["ContentTranslationsRequired"]);
        RuleFor(x => x.Value.Title).NotEmpty().WithMessage(L["ProjectTitleRequired"])
            .MaximumLength(ProjectTranslation.TitleMaxLength).WithMessage(L["ProjectTitleTooLong", ProjectTranslation.TitleMaxLength])
            .When(x => x.Value is not null);
        RuleFor(x => x.Value.Summary).MaximumLength(ProjectTranslation.SummaryMaxLength).WithMessage(L["ProjectSummaryTooLong", ProjectTranslation.SummaryMaxLength])
            .When(x => x.Value is not null);
        RuleFor(x => x.Value.Description).MaximumLength(ProjectTranslation.DescriptionMaxLength).WithMessage(L["ProjectDescriptionTooLong", ProjectTranslation.DescriptionMaxLength])
            .When(x => x.Value is not null);
    }
}
