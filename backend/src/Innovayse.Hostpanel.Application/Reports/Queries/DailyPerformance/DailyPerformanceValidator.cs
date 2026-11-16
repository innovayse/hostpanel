using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Reports.Queries.DailyPerformance;

/// <summary>
/// Validator for <see cref="DailyPerformanceQuery"/>. The handler pads every day between the two
/// bounds; a <c>From</c> after <c>To</c> produced a negative day count and crashed the report
/// with a 500, so that ordering is the one thing checked here.
/// </summary>
public sealed class DailyPerformanceValidator : LocalizedValidator<DailyPerformanceQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DailyPerformanceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.To).MustNotBeBefore(x => x.From, L["DateRangeInvalid"]);
    }
}
