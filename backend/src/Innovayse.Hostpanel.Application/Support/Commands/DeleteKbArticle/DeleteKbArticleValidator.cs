using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Commands.DeleteKbArticle;

/// <summary>Validator for <see cref="DeleteKbArticleCommand"/>.</summary>
public sealed class DeleteKbArticleValidator : LocalizedValidator<DeleteKbArticleCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteKbArticleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["KbArticleIdInvalid"]);
    }
}
