using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.DeleteCancellationRequest;

/// <summary>Validates <see cref="DeleteCancellationRequestCommand"/> before the handler executes.</summary>
public sealed class DeleteCancellationRequestValidator : LocalizedValidator<DeleteCancellationRequestCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteCancellationRequestValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["CancellationRequestIdInvalid"]);
    }
}
