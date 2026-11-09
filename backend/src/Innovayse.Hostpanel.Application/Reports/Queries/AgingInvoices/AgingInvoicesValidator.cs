using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Reports.Queries.AgingInvoices;

/// <summary>Validates <see cref="AgingInvoicesQuery"/> inputs before the handler executes.</summary>
public sealed class AgingInvoicesValidator : LocalizedValidator<AgingInvoicesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public AgingInvoicesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["ReportPageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(PagingLimits.MaxReportPageSize, L["ReportPageSizeRange"]);
    }
}
