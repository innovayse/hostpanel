using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ChargeClientRenewalInvoices;

/// <summary>Validator for <see cref="ChargeClientRenewalInvoicesCommand"/>. Checks only the shape of the id.</summary>
public sealed class ChargeClientRenewalInvoicesValidator : LocalizedValidator<ChargeClientRenewalInvoicesCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ChargeClientRenewalInvoicesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).GreaterThan(0).WithMessage(L["ClientIdInvalid"]);
    }
}
