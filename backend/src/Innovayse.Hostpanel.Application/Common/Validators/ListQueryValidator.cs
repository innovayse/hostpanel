using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Common.Validators;

/// <summary>
/// Validates the part every list query shares (<see cref="IListQuery"/>): a page from 1, a page size within the
/// use case's limit and a search text of bounded length. A value out of range is a 400, never a silent correction,
/// so a caller who asks for 5000 rows learns the limit instead of getting 100. A list derives from this and adds the
/// rules of its own filters.
/// </summary>
/// <typeparam name="T">The list query being validated.</typeparam>
public abstract class ListQueryValidator<T> : LocalizedValidator<T>
    where T : IListQuery
{
    /// <summary>Initializes the validator and declares the shared page, page size and search rules.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    /// <param name="maxPageSize">The largest page size this list accepts; the ordinary limit when not given.</param>
    protected ListQueryValidator(IStringLocalizer localizer, int maxPageSize = PagingLimits.MaxPageSize)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["ListPageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(maxPageSize, L["ListPageSizeRange", PagingLimits.MinPageSize, maxPageSize]);
        RuleFor(x => x.Search).MaximumLength(PagingLimits.MaxSearchLength).WithMessage(L["ListSearchTooLong", PagingLimits.MaxSearchLength]);
    }
}
