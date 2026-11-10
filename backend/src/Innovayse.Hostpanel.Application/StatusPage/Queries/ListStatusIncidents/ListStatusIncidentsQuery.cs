namespace Innovayse.Hostpanel.Application.StatusPage.Queries.ListStatusIncidents;

/// <summary>Query to retrieve the incidents announced within the last days, open and resolved, newest first.</summary>
/// <param name="Days">How many days back to look, from 1 to <see cref="MaxDays"/>; <see cref="DefaultDays"/> when omitted.</param>
public sealed record ListStatusIncidentsQuery(int Days = ListStatusIncidentsQuery.DefaultDays)
{
    /// <summary>The window used when the caller names none: the span the status page shows its history over.</summary>
    public const int DefaultDays = 90;

    /// <summary>The widest window a caller may ask for.</summary>
    public const int MaxDays = 365;
}
