using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetServicesReport;

/// <summary>Validates <see cref="GetServicesReportQuery"/> inputs before the handler executes.</summary>
public sealed class GetServicesReportValidator : LocalizedValidator<GetServicesReportQuery>
{
    /// <summary>Largest page size an admin report may ask for.</summary>
    private const int MaxPageSize = PagingLimits.MaxReportPageSize;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetServicesReportValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["ReportPageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(MaxPageSize, L["ReportPageSizeRange"]);
    }
}
