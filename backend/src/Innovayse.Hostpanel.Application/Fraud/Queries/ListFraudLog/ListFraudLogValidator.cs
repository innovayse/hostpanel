using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.ListFraudLog;

/// <summary>
/// Validator for <see cref="ListFraudLogQuery"/>. The page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>); this adds the rules of the kind, decision and reason filters.
/// </summary>
public sealed class ListFraudLogValidator : ListQueryValidator<ListFraudLogQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListFraudLogValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Kind).IsInEnum().WithMessage(L["FraudCheckKindInvalid"]);
        RuleFor(x => x.Decision).IsInEnum().WithMessage(L["FraudDecisionInvalid"]);
        RuleFor(x => x.Reason).IsInEnum().WithMessage(L["FraudReasonInvalid"]);
    }
}
