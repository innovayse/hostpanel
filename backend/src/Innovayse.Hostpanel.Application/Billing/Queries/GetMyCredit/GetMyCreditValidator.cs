using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyCredit;

/// <summary>
/// Validator for <see cref="GetMyCreditQuery"/>. Page and page size are clamped by the handler, as on the other lists, so no
/// rule is declared for them; the query is accepted as bound.
/// </summary>
public sealed class GetMyCreditValidator : LocalizedValidator<GetMyCreditQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetMyCreditValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
