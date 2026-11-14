using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Application.Support.Common;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListTickets;

/// <summary>
/// Validates <see cref="ListTicketsQuery"/>. The page, page size, search and the status, priority and department
/// filters are the shared ticket list rules (<see cref="TicketListQueryValidator{T}"/>); the flagged switch needs none.
/// </summary>
public sealed class ListTicketsValidator : TicketListQueryValidator<ListTicketsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListTicketsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
