using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateMySetupIntent;

/// <summary>Validates <see cref="CreateMySetupIntentCommand"/> before the handler executes.</summary>
public sealed class CreateMySetupIntentValidator : LocalizedValidator<CreateMySetupIntentCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateMySetupIntentValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ConsentAccepted).Equal(true).WithMessage(L["CardConsentRequired"]);
    }
}
