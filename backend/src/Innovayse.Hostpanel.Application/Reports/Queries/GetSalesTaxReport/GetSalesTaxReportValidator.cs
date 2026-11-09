using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetSalesTaxReport;

/// <summary>
/// Validates <see cref="GetSalesTaxReportQuery"/> inputs before the handler executes. The date
/// bounds are plain <c>WHERE</c> filters, so an inverted range answers with an empty report; only
/// the page is checked.
/// </summary>
public sealed class GetSalesTaxReportValidator : LocalizedValidator<GetSalesTaxReportQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetSalesTaxReportValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["ReportPageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(PagingLimits.MaxReportPageSize, L["ReportPageSizeRange"]);
    }
}
