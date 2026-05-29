namespace Innovayse.Application.Support.Queries.ListDepartments;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns all support departments mapped to <see cref="DepartmentDto"/>.
/// </summary>
public sealed class ListDepartmentsHandler(IDepartmentRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListDepartmentsQuery"/>.
    /// </summary>
    /// <param name="query">The list departments query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of department DTOs.</returns>
    public async Task<IReadOnlyList<DepartmentDto>> HandleAsync(ListDepartmentsQuery query, CancellationToken ct)
    {
        var departments = await repo.ListAllAsync(ct);
        return departments
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Email))
            .ToList();
    }
}
