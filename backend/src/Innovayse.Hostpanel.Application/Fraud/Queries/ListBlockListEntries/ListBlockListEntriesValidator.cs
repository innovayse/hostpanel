using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.ListBlockListEntries;

/// <summary>
/// Validator for <see cref="ListBlockListEntriesQuery"/>. Page and page size are clamped by the handler, and the type is typed, so
/// an unknown value is refused at binding; no rule is declared here.
/// </summary>
public sealed class ListBlockListEntriesValidator : LocalizedValidator<ListBlockListEntriesQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListBlockListEntriesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
