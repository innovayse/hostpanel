using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Commands.CreateVatRate;

/// <summary>Validates <see cref="CreateVatRateCommand"/> before the handler executes.</summary>
public sealed class CreateVatRateValidator : LocalizedValidator<CreateVatRateCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateVatRateValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Country).MustBeMemberState(L["VatRateCountryInvalid"]);
        RuleFor(x => x.Kind).MustBeVatRateKind(L["VatRateKindInvalid"]);
        RuleFor(x => x.RatePercent).MustBeTaxRate(L["VatRateRateInvalid"], L["VatRateRateDecimalsInvalid"]);
        RuleFor(x => x.ValidFrom).MustBeVatRateStart(L["VatRateValidFromInvalid"]);
    }
}
