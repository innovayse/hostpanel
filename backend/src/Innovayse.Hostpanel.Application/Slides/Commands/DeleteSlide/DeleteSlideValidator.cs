using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Slides.Commands.DeleteSlide;

/// <summary>Validates <see cref="DeleteSlideCommand"/> before it reaches the handler.</summary>
public sealed class DeleteSlideValidator : LocalizedValidator<DeleteSlideCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteSlideValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["SlideIdInvalid"]);
    }
}
