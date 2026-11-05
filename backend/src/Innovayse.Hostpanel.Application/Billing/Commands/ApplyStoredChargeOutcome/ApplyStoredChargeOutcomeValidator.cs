using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ApplyStoredChargeOutcome;

/// <summary>
/// Validator for <see cref="ApplyStoredChargeOutcomeCommand"/>. Refuses an id that is not positive and an outcome the
/// capability does not define (no outcome at all is allowed: it means the key lookup found no charge) (a default-initialised value means the plugin set nothing).
/// </summary>
public sealed class ApplyStoredChargeOutcomeValidator : LocalizedValidator<ApplyStoredChargeOutcomeCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ApplyStoredChargeOutcomeValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClaimId).GreaterThan(0).WithMessage(L["StoredChargeInvalid"]);
        RuleFor(x => x.Outcome).IsInEnum().WithMessage(L["StoredChargeInvalid"]);
    }
}
