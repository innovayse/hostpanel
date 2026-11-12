using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.ListFraudLog;

/// <summary>
/// Validator for <see cref="ListFraudLogQuery"/>. Page and page size are clamped by the handler, and the filters are typed, so an
/// unknown value is refused at binding; no rule is declared here.
/// </summary>
public sealed class ListFraudLogValidator : LocalizedValidator<ListFraudLogQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListFraudLogValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
