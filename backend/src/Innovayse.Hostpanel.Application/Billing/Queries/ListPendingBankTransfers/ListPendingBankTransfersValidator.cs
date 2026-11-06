using FluentValidation;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Application.Common.Validators;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListPendingBankTransfers;

/// <summary>
/// Validator for <see cref="ListPendingBankTransfersQuery"/>. Page and page size are clamped by the handler, as on the
/// other staff lists, so no rule is declared for them; the query is accepted as bound.
/// </summary>
public sealed class ListPendingBankTransfersValidator : LocalizedValidator<ListPendingBankTransfersQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListPendingBankTransfersValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
