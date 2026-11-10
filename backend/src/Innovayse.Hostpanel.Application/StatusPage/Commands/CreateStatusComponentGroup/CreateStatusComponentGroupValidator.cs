using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.StatusPage;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CreateStatusComponentGroup;

/// <summary>Validates <see cref="CreateStatusComponentGroupCommand"/> before it reaches the handler.</summary>
public sealed class CreateStatusComponentGroupValidator : LocalizedValidator<CreateStatusComponentGroupCommand>
{
    /// <summary>Initializes the validator with the localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateStatusComponentGroupValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["StatusNameRequired"])
            .MaximumLength(StatusComponentGroup.NameMaxLength).WithMessage(L["StatusNameTooLong", StatusComponentGroup.NameMaxLength]);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).WithMessage(L["ContentSortOrderInvalid"]);
    }
}
