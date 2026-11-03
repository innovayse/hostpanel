using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.UpdateTldConfig;

/// <summary>
/// Validator for <see cref="UpdateTldConfigCommand"/>. Whether the id names a configured TLD, and
/// whether the registrar module names a known provider, is the handler's answer.
/// </summary>
public sealed class UpdateTldConfigValidator : LocalizedValidator<UpdateTldConfigCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UpdateTldConfigValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Id).NotNull().GreaterThan(0).WithMessage(L["TldConfigIdRequired"]);
        RuleFor(x => x.RegistrarModule).NotEmpty().WithMessage(L["TldRegistrarModuleRequired"]);
        RuleFor(x => x.Currency).MustBeCurrencyCode(L["TldCurrencyInvalid"]);
        RuleFor(x => x.SellCurrency).MustBeCurrencyCode(L["TldSellCurrencyInvalid"]);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).WithMessage(L["TldSortOrderInvalid"]);
    }
}
