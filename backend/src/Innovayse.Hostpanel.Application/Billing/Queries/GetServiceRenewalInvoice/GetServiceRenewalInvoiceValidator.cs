using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetServiceRenewalInvoice;

/// <summary>Validates <see cref="GetServiceRenewalInvoiceQuery"/>: the service must be a real id.</summary>
public sealed class GetServiceRenewalInvoiceValidator : LocalizedValidator<GetServiceRenewalInvoiceQuery>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public GetServiceRenewalInvoiceValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
