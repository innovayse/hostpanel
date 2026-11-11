using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListDomainsOnOpenRenewalInvoices;

/// <summary>Validates <see cref="ListDomainsOnOpenRenewalInvoicesQuery"/> before the handler executes.</summary>
public sealed class ListDomainsOnOpenRenewalInvoicesValidator : LocalizedValidator<ListDomainsOnOpenRenewalInvoicesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListDomainsOnOpenRenewalInvoicesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleForEach(x => x.RegisteredDomainIds).MustBePositiveId(L["DomainIdRequired"]);
    }
}
