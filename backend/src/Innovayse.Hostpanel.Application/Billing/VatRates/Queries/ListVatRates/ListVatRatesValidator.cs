using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.VatRates.Queries.ListVatRates;

/// <summary>Validates <see cref="ListVatRatesQuery"/>; the page, page size and search are the shared list rules (<see cref="ListQueryValidator{T}"/>).</summary>
public sealed class ListVatRatesValidator : ListQueryValidator<ListVatRatesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListVatRatesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
