using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Currencies.Commands.SetBaseCurrency;

/// <summary>
///     Validator for SetBaseCurrencyCommand.
///     Checks the shape of the code; whether it names a configured currency is the handler's answer.
/// </summary>
public sealed class SetBaseCurrencyValidator : LocalizedValidator<SetBaseCurrencyCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SetBaseCurrencyValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Code).MustBeCurrencyCode(L["CurrencyCodeInvalid"]);
    }
}
