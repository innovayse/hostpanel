using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Application.Support.Common;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListClientTickets;

/// <summary>
/// Validates <see cref="ListClientTicketsQuery"/>. The page, page size, search and the status, priority and
/// department filters are the shared ticket list rules (<see cref="TicketListQueryValidator{T}"/>); this adds the
/// rule of the client id.
/// </summary>
public sealed class ListClientTicketsValidator : TicketListQueryValidator<ListClientTicketsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListClientTicketsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ClientId).MustBePositiveId(L["TicketClientIdInvalid"]);
    }
}
