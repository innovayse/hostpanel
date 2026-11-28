using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.SearchPredefinedReplies;

/// <summary>Validator for <see cref="SearchPredefinedRepliesQuery"/>.</summary>
public sealed class SearchPredefinedRepliesValidator : LocalizedValidator<SearchPredefinedRepliesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SearchPredefinedRepliesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Search)
            .NotEmpty()
            .WithMessage(L["SearchTermRequired"]);
    }
}
