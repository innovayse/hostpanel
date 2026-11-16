namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The period filter a list of dated rows (a log, a mail history) offers: the first and the last calendar day (UTC) to keep.
/// A list query that has it implements this next to <see cref="IListQuery"/>, so the validation
/// (<see cref="Innovayse.Hostpanel.Application.Common.Validators.PeriodListQueryValidator{T}"/>) is written once and the SQL is
/// <see cref="Innovayse.Hostpanel.Application.Common.Extensions.DateRangeExtensions.InPeriod{T}"/>.
/// </summary>
public interface IPeriodListFilter
{
    /// <summary>Gets the first day to keep, inclusive; null for no lower bound.</summary>
    public DateOnly? From { get; }

    /// <summary>Gets the last day to keep, the whole day included; null for no upper bound.</summary>
    public DateOnly? To { get; }
}
