using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListMyCreditNotes;

/// <summary>
/// Validator for <see cref="ListMyCreditNotesQuery"/>. The query carries nothing the caller chooses, so no rule is declared
/// here; decided, not forgotten.
/// </summary>
public sealed class ListMyCreditNotesValidator : LocalizedValidator<ListMyCreditNotesQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListMyCreditNotesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
