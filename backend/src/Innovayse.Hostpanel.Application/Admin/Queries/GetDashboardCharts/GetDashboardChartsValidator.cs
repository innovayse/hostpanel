using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetDashboardCharts;

/// <summary>Validates <see cref="GetDashboardChartsQuery"/>.</summary>
public sealed class GetDashboardChartsValidator : LocalizedValidator<GetDashboardChartsQuery>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public GetDashboardChartsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Period).IsInEnum().WithMessage(L["DashboardPeriodInvalid"]);
    }
}
