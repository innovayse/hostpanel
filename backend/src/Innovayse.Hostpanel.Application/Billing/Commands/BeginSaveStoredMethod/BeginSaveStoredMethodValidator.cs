using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.BeginSaveStoredMethod;

/// <summary>
/// Validator for <see cref="BeginSaveStoredMethodCommand"/>. Checks the shape of the module id and return address and
/// that the consent box was ticked; whether the module offers stored credentials and whether the address's origin is
/// allowed is the handler's answer.
/// </summary>
public sealed class BeginSaveStoredMethodValidator : LocalizedValidator<BeginSaveStoredMethodCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public BeginSaveStoredMethodValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.GatewayModuleId).NotEmpty().WithMessage(L["GatewayModuleRequired"]);
        RuleFor(x => x.ConsentAccepted).Equal(true).WithMessage(L["CardConsentRequired"]);
        RuleFor(x => x.ConsentTextVersion)
            .NotEmpty()
            .MaximumLength(CardChargeConsent.MaxTextVersionLength)
            .WithMessage(L["ConsentTextVersionInvalid", CardChargeConsent.MaxTextVersionLength]);
        RuleFor(x => x.ReturnUrl).NotEmpty().WithMessage(L["ReturnUrlRequired"])
            .MustBeAbsoluteWebUrl(L["ReturnUrlInvalid"]);
    }
}
