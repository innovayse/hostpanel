using System.Text.RegularExpressions;
using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.GetPublishedKbArticleBySlug;

/// <summary>Validator for <see cref="GetPublishedKbArticleBySlugQuery"/>.</summary>
public sealed class GetPublishedKbArticleBySlugValidator : LocalizedValidator<GetPublishedKbArticleBySlugQuery>
{
    /// <summary>The shape of a slug in any letter case: the stored shape, case insensitive.</summary>
    private static readonly Regex Shape = new(
        KbSlugRules.Pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetPublishedKbArticleBySlugValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage(L["KbArticleSlugInvalid"])
            .MaximumLength(KbSlugRules.MaxLength).WithMessage(L["KbArticleSlugInvalid"])
            .Must(slug => Shape.IsMatch(slug)).WithMessage(L["KbArticleSlugInvalid"]);
    }
}
