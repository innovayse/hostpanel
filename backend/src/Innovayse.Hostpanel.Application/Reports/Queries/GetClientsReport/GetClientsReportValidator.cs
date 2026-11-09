using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Reports.Queries.GetClientsReport;

/// <summary>Validates <see cref="GetClientsReportQuery"/> inputs before the handler executes.</summary>
public sealed class GetClientsReportValidator : LocalizedValidator<GetClientsReportQuery>
{
    /// <summary>Largest page size an admin report may ask for.</summary>
    private const int MaxPageSize = PagingLimits.MaxReportPageSize;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetClientsReportValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["ReportPageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(MaxPageSize, L["ReportPageSizeRange"]);
    }
}
