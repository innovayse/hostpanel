using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListTransactions;

/// <summary>
/// Validates <see cref="ListTransactionsQuery"/>. The page, page size, search and every filter are the shared transaction
/// list rules (<see cref="TransactionListQueryValidator{T}"/>); the query has no member of its own that needs one.
/// </summary>
public sealed class ListTransactionsValidator : TransactionListQueryValidator<ListTransactionsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListTransactionsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
