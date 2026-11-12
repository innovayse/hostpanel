using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.DeleteStatusComponentGroup;

/// <summary>Validates <see cref="DeleteStatusComponentGroupCommand"/> before it reaches the handler.</summary>
public sealed class DeleteStatusComponentGroupValidator : LocalizedValidator<DeleteStatusComponentGroupCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteStatusComponentGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
