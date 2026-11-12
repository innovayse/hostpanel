using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.UpdateFraudSettings;

/// <summary>
/// Validator for <see cref="UpdateFraudSettingsCommand"/>: both scores lie on the 0 to 100 scale and the review score is below the
/// reject score, the limits are inside the ranges the settings accept. The ranges are the ones of the settings' value patterns.
/// </summary>
public sealed class UpdateFraudSettingsValidator : LocalizedValidator<UpdateFraudSettingsCommand>
{
    /// <summary>Longest velocity window, in minutes: one day.</summary>
    private const int MaxWindowMinutes = 1440;

    /// <summary>Highest limit on orders or accounts per IP address.</summary>
    private const int MaxCount = 100;

    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateFraudSettingsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ManualReviewRiskScore).InclusiveBetween(0, 100).WithMessage(L["FraudScoreOutOfRange"]);
        RuleFor(x => x.AutoRejectRiskScore).InclusiveBetween(0, 100).WithMessage(L["FraudScoreOutOfRange"]);
        RuleFor(x => x.ManualReviewRiskScore)
            .LessThan(x => x.AutoRejectRiskScore)
            .WithMessage(L["FraudReviewBelowReject"]);
        RuleFor(x => x.VelocityWindowMinutes).InclusiveBetween(1, MaxWindowMinutes).WithMessage(L["FraudVelocityWindowOutOfRange"]);
        RuleFor(x => x.VelocityMaxOrders).InclusiveBetween(1, MaxCount).WithMessage(L["FraudCountOutOfRange"]);
        RuleFor(x => x.MaxAccountsPerIp).InclusiveBetween(1, MaxCount).WithMessage(L["FraudCountOutOfRange"]);
    }
}
