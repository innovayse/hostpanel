using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductPrices;

/// <summary>
///     Validator for UpdateProductPricesCommand.
///     Checks the shape of each code and that none is listed twice, ignoring case; whether one is
///     configured, or is the base, is the handler's answer.
/// </summary>
public sealed class UpdateProductPricesValidator : LocalizedValidator<UpdateProductPricesCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateProductPricesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Currencies)
            .NotEmpty()
            .WithMessage(L["CurrenciesRequired"]);

        RuleForEach(x => x.Currencies).MustBeCurrencyCode(L["CurrencyCodeInvalid"]);

        // "usd" and "USD" name one currency; listing it twice would convert and write it twice.
        RuleFor(x => x.Currencies)
            .Must(codes => codes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == codes.Count)
            .WithMessage(L["CurrencyCodeDuplicate"])
            .When(x => x.Currencies is not null);
    }
}
