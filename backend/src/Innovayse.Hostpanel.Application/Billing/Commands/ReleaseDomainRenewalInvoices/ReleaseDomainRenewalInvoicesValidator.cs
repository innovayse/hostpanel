using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ReleaseDomainRenewalInvoices;

/// <summary>Validates <see cref="ReleaseDomainRenewalInvoicesCommand"/> before the handler executes.</summary>
public sealed class ReleaseDomainRenewalInvoicesValidator : LocalizedValidator<ReleaseDomainRenewalInvoicesCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ReleaseDomainRenewalInvoicesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.RegisteredDomainId).MustBePositiveId(L["DomainIdRequired"]);
    }
}
