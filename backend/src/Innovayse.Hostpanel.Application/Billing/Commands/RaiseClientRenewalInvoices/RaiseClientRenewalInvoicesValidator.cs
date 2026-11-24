using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RaiseClientRenewalInvoices;

/// <summary>
/// Validates <see cref="RaiseClientRenewalInvoicesCommand"/>: a real client, a real trial service when one is named,
/// and a due date only for a trial conversion.
/// </summary>
public sealed class RaiseClientRenewalInvoicesValidator : LocalizedValidator<RaiseClientRenewalInvoicesCommand>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public RaiseClientRenewalInvoicesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).GreaterThan(0).WithMessage(L["ClientIdInvalid"]);

        RuleFor(x => x.TrialServiceId).MustBePositiveId(L["ServiceIdInvalid"]);

        When(x => x.TrialServiceId is null, () =>
        {
            RuleFor(x => x.DueAt).Null().WithMessage(L["RenewalDueAtWithoutTrialService"]);
        });
    }
}
