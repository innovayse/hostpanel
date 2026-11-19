using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Queries.ListVatRates;

/// <summary>
/// Validates <see cref="ListVatRatesQuery"/>; the page, page size and search are the shared list rules
/// (<see cref="ListQueryValidator{T}"/>), and this adds the rules of the kind and status filters.
/// </summary>
public sealed class ListVatRatesValidator : ListQueryValidator<ListVatRatesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListVatRatesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Kind).IsInEnum().WithMessage(L["VatRateListKindInvalid"]);
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["VatRateListStatusInvalid"]);
    }
}
