namespace Innovayse.Application.Support.Queries.GetDepartments;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns all support departments mapped to <see cref="DepartmentDto"/>.
/// </summary>
public sealed class GetDepartmentsHandler(IDepartmentRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetDepartmentsQuery"/>.
    /// </summary>
    /// <param name="query">The get departments query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all department DTOs.</returns>
    public async Task<IReadOnlyList<DepartmentDto>> HandleAsync(GetDepartmentsQuery query, CancellationToken ct)
    {
        var departments = await repo.ListAllAsync(ct);
        return departments
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Email))
            .ToList();
    }
}
