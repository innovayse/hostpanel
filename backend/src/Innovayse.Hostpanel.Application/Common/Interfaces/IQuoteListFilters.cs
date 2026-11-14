using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The quote filters every quote list shares: the stage and the validity. A list query that offers them implements this
/// next to <see cref="IListQuery"/>, so the validation
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Common.QuoteListQueryValidator{T}"/>) and the SQL
/// (<see cref="Innovayse.Hostpanel.Application.Billing.Extensions.QuoteFilterExtensions"/>) are written once.
/// </summary>
public interface IQuoteListFilters
{
    /// <summary>Gets the lifecycle stage to keep; null keeps every stage.</summary>
    public QuoteStage? Stage { get; }

    /// <summary>Gets whether to keep only the quotes still valid or only the expired ones; null keeps both.</summary>
    public QuoteValidity? Validity { get; }
}
