using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.GetPublishedKbArticle;

/// <summary>Validator for <see cref="GetPublishedKbArticleQuery"/>.</summary>
public sealed class GetPublishedKbArticleValidator : LocalizedValidator<GetPublishedKbArticleQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetPublishedKbArticleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["KbArticleIdInvalid"]);
    }
}
