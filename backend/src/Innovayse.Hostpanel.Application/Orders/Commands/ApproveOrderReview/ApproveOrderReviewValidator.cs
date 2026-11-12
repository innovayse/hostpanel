using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Orders.Commands.ApproveOrderReview;

/// <summary>Validator for <see cref="ApproveOrderReviewCommand"/>. Checks only the shape of the id; whether it names an order under review is the handler's answer.</summary>
public sealed class ApproveOrderReviewValidator : LocalizedValidator<ApproveOrderReviewCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ApproveOrderReviewValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.OrderId).MustBePositiveId(L["OrderIdInvalid"]);
    }
}
