using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.TaxRules.Commands.DeleteTaxRule;

/// <summary>Validates <see cref="DeleteTaxRuleCommand"/> before the handler executes.</summary>
public sealed class DeleteTaxRuleValidator : LocalizedValidator<DeleteTaxRuleCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public DeleteTaxRuleValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).MustBePositiveId(L["ContentIdInvalid"]);
    }
}
