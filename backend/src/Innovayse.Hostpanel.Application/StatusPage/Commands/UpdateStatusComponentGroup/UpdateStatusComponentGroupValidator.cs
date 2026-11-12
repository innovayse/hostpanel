using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusComponentGroup;

/// <summary>Validates <see cref="UpdateStatusComponentGroupCommand"/> before it reaches the handler.</summary>
public sealed class UpdateStatusComponentGroupValidator : LocalizedValidator<UpdateStatusComponentGroupCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateStatusComponentGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["StatusNameRequired"])
            .MaximumLength(StatusComponentGroup.NameMaxLength).WithMessage(L["StatusNameTooLong", StatusComponentGroup.NameMaxLength]);
    }
}
