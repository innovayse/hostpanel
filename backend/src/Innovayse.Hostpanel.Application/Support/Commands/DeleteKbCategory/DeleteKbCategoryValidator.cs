using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Commands.DeleteKbCategory;

/// <summary>Validator for <see cref="DeleteKbCategoryCommand"/>.</summary>
public sealed class DeleteKbCategoryValidator : LocalizedValidator<DeleteKbCategoryCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteKbCategoryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["KbCategoryIdInvalid"]);
    }
}
