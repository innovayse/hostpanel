namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The filters every email log list shares: whether the send succeeded and the period it was sent in
/// (<see cref="IPeriodListFilter"/>). A list query that offers them implements this next to <see cref="IListQuery"/>, so the SQL
/// (<see cref="Innovayse.Hostpanel.Application.Notifications.Extensions.EmailLogFilterExtensions"/>) is written once.
/// </summary>
public interface IEmailLogListFilters : IPeriodListFilter
{
    /// <summary>Gets the outcome to keep: true for delivered mail, false for failed mail; null keeps both.</summary>
    public bool? Success { get; }
}
