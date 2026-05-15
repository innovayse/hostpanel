namespace Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Repository abstraction for <see cref="Department"/> entity persistence.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>
    /// Finds a department by its primary key.
    /// </summary>
    /// <param name="id">The department identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="Department"/>, or <see langword="null"/> if not found.</returns>
    Task<Department?> FindByIdAsync(int id, CancellationToken ct);

    /// <summary>
    /// Stages a new department for insertion on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="department">The department to add.</param>
    void Add(Department department);

    /// <summary>
    /// Marks an existing department as modified on the next <c>SaveChanges</c> call.
    /// </summary>
    /// <param name="department">The department to update.</param>
    void Update(Department department);

    /// <summary>
    /// Returns all departments.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all departments.</returns>
    Task<IReadOnlyList<Department>> ListAllAsync(CancellationToken ct);
}
