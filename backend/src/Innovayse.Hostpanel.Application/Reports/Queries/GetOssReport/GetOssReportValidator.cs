using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetOssReport;

/// <summary>
/// Validates <see cref="GetOssReportQuery"/> inputs before the handler executes. The unvalidated
/// endpoint silently mapped any quarter outside 1-4 onto Q4 (October-December); refusing it here
/// is the same fix the month-range validators elsewhere in this module already apply.
/// </summary>
public sealed class GetOssReportValidator : LocalizedValidator<GetOssReportQuery>
{
    /// <summary>Earliest calendar year a report can be asked for; the scheme this report replaced started in 2015.</summary>
    private const int MinYear = 2015;

    /// <summary>Latest calendar year a report can be asked for.</summary>
    private const int MaxYear = 2100;

    /// <summary>Smallest valid calendar quarter.</summary>
    private const int MinQuarter = 1;

    /// <summary>Largest valid calendar quarter.</summary>
    private const int MaxQuarter = 4;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetOssReportValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Year!.Value)
            .InclusiveBetween(MinYear, MaxYear)
            .When(x => x.Year.HasValue)
            .WithMessage(L["ReportYearInvalid"]);

        RuleFor(x => x.Quarter!.Value)
            .InclusiveBetween(MinQuarter, MaxQuarter)
            .When(x => x.Quarter.HasValue)
            .WithMessage(L["ReportQuarterInvalid"]);
    }
}
