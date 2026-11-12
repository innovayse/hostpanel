using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.RejectOrderReview;

/// <summary>Validator for <see cref="RejectOrderReviewCommand"/>: a positive id and a reason the client can be sent.</summary>
public sealed class RejectOrderReviewValidator : LocalizedValidator<RejectOrderReviewCommand>
{
    /// <summary>Longest reason, in characters; the client reads it in a mail.</summary>
    public const int ReasonMaxLength = 1000;

    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RejectOrderReviewValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
        RuleFor(x => x.Reason).NotEmpty().WithMessage(L["OrderReviewReasonRequired"]);
        RuleFor(x => x.Reason).MaximumLength(ReasonMaxLength).WithMessage(L["OrderReviewReasonTooLong"]);
    }
}
