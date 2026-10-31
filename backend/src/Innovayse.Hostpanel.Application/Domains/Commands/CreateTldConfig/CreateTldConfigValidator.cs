using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.CreateTldConfig;

/// <summary>
/// Validator for <see cref="CreateTldConfigCommand"/>. Whether the registrar module names a known
/// provider, and whether the TLD is already configured, is the handler's answer.
/// </summary>
public sealed class CreateTldConfigValidator : LocalizedValidator<CreateTldConfigCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateTldConfigValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Tld).NotEmpty().WithMessage(L["TldExtensionRequired"]);
        RuleFor(x => x.RegistrarModule).NotEmpty().WithMessage(L["TldRegistrarModuleRequired"]);
        RuleFor(x => x.Currency).MustBeCurrencyCode(L["TldCurrencyInvalid"]);
        RuleFor(x => x.SellCurrency).MustBeCurrencyCode(L["TldSellCurrencyInvalid"]);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).WithMessage(L["TldSortOrderInvalid"]);
    }
}
