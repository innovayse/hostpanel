namespace Innovayse.Infrastructure.Support;

using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;
using Innovayse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>EF Core implementation of <see cref="IDepartmentRepository"/>.</summary>
/// <param name="db">The application database context.</param>
public sealed class DepartmentRepository(AppDbContext db) : IDepartmentRepository
{
    /// <inheritdoc/>
    public async Task<Department?> FindByIdAsync(int id, CancellationToken ct) =>
        await db.Departments.FirstOrDefaultAsync(d => d.Id == id, ct);

    /// <inheritdoc/>
    public void Add(Department department) => db.Departments.Add(department);

    /// <inheritdoc/>
    public void Update(Department department) => db.Departments.Update(department);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Department>> ListAllAsync(CancellationToken ct) =>
        await db.Departments.OrderBy(d => d.Name).ToListAsync(ct);
}
