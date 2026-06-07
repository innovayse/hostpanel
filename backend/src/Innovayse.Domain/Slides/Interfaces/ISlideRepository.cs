namespace Innovayse.Domain.Slides.Interfaces;

/// <summary>Repository abstraction for <see cref="Slide"/> aggregates.</summary>
public interface ISlideRepository
{
    /// <summary>Finds a slide by ID, including translations.</summary>
    /// <param name="id">The slide identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="Slide"/>, or <c>null</c> if not found.</returns>
    Task<Slide?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>Returns all slides ordered by SortOrder, including translations.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All slides ordered by SortOrder ascending.</returns>
    Task<List<Slide>> ListAllAsync(CancellationToken ct);

    /// <summary>Returns active slides visible at the given time, ordered by SortOrder.</summary>
    /// <param name="now">The point in time to check visibility against.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Active slides visible at <paramref name="now"/>, ordered by SortOrder ascending.</returns>
    Task<List<Slide>> ListActiveAsync(DateTimeOffset now, CancellationToken ct);

    /// <summary>Adds a new slide to the persistence store.</summary>
    /// <param name="slide">The slide to add.</param>
    void Add(Slide slide);

    /// <summary>Removes a slide from the persistence store.</summary>
    /// <param name="slide">The slide to remove.</param>
    void Remove(Slide slide);
}
