using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Common.Validators;

/// <summary>
/// Validates a list that offers a period (<see cref="IPeriodListFilter"/>): the shared page, page size and search
/// (<see cref="ListQueryValidator{T}"/>) and a last day that is not before the first. A list derives from this and adds the
/// rules of its own filters.
/// </summary>
/// <typeparam name="T">The list query being validated.</typeparam>
public abstract class PeriodListQueryValidator<T> : ListQueryValidator<T>
    where T : IListQuery, IPeriodListFilter
{
    /// <summary>Initializes the validator and declares the period rule.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    protected PeriodListQueryValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.To).MustNotBeBefore(x => x.From, L["DateRangeInvalid"]);
    }
}
