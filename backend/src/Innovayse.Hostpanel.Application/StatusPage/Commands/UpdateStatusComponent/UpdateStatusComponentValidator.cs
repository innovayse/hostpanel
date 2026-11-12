using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.UpdateStatusComponent;

/// <summary>Validates <see cref="UpdateStatusComponentCommand"/> before it reaches the handler.</summary>
public sealed class UpdateStatusComponentValidator : LocalizedValidator<UpdateStatusComponentCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateStatusComponentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["StatusNameRequired"])
            .MaximumLength(StatusComponent.NameMaxLength).WithMessage(L["StatusNameTooLong", StatusComponent.NameMaxLength]);
        RuleFor(x => x.Description).MaximumLength(StatusComponent.DescriptionMaxLength)
            .WithMessage(L["StatusDescriptionTooLong", StatusComponent.DescriptionMaxLength]);
    }
}
