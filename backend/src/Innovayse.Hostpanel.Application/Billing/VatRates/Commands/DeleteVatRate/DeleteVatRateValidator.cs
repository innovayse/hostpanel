using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Commands.DeleteVatRate;

/// <summary>Validates <see cref="DeleteVatRateCommand"/> before the handler executes.</summary>
public sealed class DeleteVatRateValidator : LocalizedValidator<DeleteVatRateCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteVatRateValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
