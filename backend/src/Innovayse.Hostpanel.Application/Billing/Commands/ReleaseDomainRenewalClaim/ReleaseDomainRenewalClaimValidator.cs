using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ReleaseDomainRenewalClaim;

/// <summary>Validates <see cref="ReleaseDomainRenewalClaimCommand"/>.</summary>
public sealed class ReleaseDomainRenewalClaimValidator : LocalizedValidator<ReleaseDomainRenewalClaimCommand>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public ReleaseDomainRenewalClaimValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.InvoiceId).MustBePositiveId(L["InvoiceIdInvalid"]);
        RuleFor(x => x.ItemId).MustBePositiveId(L["InvoiceItemIdInvalid"]);
    }
}
