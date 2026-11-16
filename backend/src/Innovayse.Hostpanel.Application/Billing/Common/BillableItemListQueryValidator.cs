using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Validates the part every billable item list query shares: the page, page size and search (<see cref="ListQueryValidator{T}"/>)
/// and the type filter (<see cref="IBillableItemListFilters"/>). A list derives from this and adds the rules of its own members.
/// </summary>
/// <typeparam name="T">The billable item list query being validated.</typeparam>
public abstract class BillableItemListQueryValidator<T> : ListQueryValidator<T>
    where T : IListQuery, IBillableItemListFilters
{
    /// <summary>Initializes the validator and declares the billable item filter rules.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    protected BillableItemListQueryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Type).IsInEnum().WithMessage(L["BillableItemTypeInvalid"]);
    }
}
