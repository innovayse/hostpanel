namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The current moment, for every use case and persistence step that needs to know what time it is.
/// </summary>
/// <remarks>
/// Application code asks this port instead of reading the wall clock, so a test can pin or move
/// time and every timestamp written in one request comes from the same source. Domain entities
/// never see it: a domain method that needs the moment takes it as a parameter.
/// </remarks>
public interface IClock
{
    /// <summary>Gets the current moment in UTC.</summary>
    public DateTimeOffset UtcNow { get; }
}
