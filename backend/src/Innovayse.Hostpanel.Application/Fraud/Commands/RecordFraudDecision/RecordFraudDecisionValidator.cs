using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Fraud;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;

/// <summary>Validator for <see cref="RecordFraudDecisionCommand"/>: the kind and decision are known, the score is on its scale, the text fits its column.</summary>
public sealed class RecordFraudDecisionValidator : LocalizedValidator<RecordFraudDecisionCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RecordFraudDecisionValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Kind).IsInEnum().WithMessage(L["FraudCheckKindInvalid"]);
        RuleFor(x => x.Decision).IsInEnum().WithMessage(L["FraudDecisionInvalid"]);
        RuleFor(x => x.Score).InclusiveBetween(0, FraudAssessment.MaxScore).WithMessage(L["FraudScoreOutOfRange"]);
        RuleFor(x => x.Reasons).NotNull();
        RuleFor(x => x.IpAddress).MaximumLength(FraudLogEntry.IpAddressMaxLength);
        RuleFor(x => x.Email).MaximumLength(FraudLogEntry.EmailMaxLength);
    }
}
