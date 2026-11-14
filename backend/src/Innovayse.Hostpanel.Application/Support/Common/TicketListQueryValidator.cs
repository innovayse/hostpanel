using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>
/// Validates the part every ticket list query shares: the page, page size and search (<see cref="ListQueryValidator{T}"/>)
/// and the status, priority and department filters (<see cref="ITicketListFilters"/>). A list derives from this and
/// adds the rules of its own members.
/// </summary>
/// <typeparam name="T">The ticket list query being validated.</typeparam>
public abstract class TicketListQueryValidator<T> : ListQueryValidator<T>
    where T : IListQuery, ITicketListFilters
{
    /// <summary>Initializes the validator and declares the ticket filter rules.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    protected TicketListQueryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Status).IsInEnum().WithMessage(L["TicketStatusInvalid"]);
        RuleFor(x => x.Priority).IsInEnum().WithMessage(L["TicketPriorityInvalid"]);
        RuleFor(x => x.DepartmentId).MustBePositiveId(L["TicketDepartmentIdInvalid"]);
    }
}
