using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.EvaluateFraudRisk;

/// <summary>Validator for <see cref="EvaluateFraudRiskQuery"/>: the kind must be one the product knows. The rest is free text a signal reads.</summary>
public sealed class EvaluateFraudRiskValidator : LocalizedValidator<EvaluateFraudRiskQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public EvaluateFraudRiskValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Kind).IsInEnum().WithMessage(L["FraudCheckKindInvalid"]);
    }
}
