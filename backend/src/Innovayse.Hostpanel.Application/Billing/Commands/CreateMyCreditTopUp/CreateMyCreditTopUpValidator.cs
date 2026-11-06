using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateMyCreditTopUp;

/// <summary>
/// Validates <see cref="CreateMyCreditTopUpCommand"/>. Only that the amount is positive is checked here; whether it is inside
/// the admin's limits depends on a setting, so the handler answers that with its own code.
/// </summary>
public sealed class CreateMyCreditTopUpValidator : LocalizedValidator<CreateMyCreditTopUpCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateMyCreditTopUpValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage(L["CreditTopUpAmountInvalid"]);
    }
}
