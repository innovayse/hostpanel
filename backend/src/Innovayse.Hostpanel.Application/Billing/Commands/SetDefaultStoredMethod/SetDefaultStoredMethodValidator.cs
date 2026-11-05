using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SetDefaultStoredMethod;

/// <summary>
/// Validator for <see cref="SetDefaultStoredMethodCommand"/>. Checks only the shape of the id; whether the consent is
/// the caller's is the handler's answer.
/// </summary>
public sealed class SetDefaultStoredMethodValidator : LocalizedValidator<SetDefaultStoredMethodCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetDefaultStoredMethodValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ConsentId).GreaterThan(0).WithMessage(L["CardConsentIdInvalid"]);
    }
}
