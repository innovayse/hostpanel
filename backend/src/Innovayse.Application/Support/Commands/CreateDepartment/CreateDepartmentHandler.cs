namespace Innovayse.Application.Support.Commands.CreateDepartment;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Creates a new support department and persists it via <see cref="IDepartmentRepository"/>.
/// </summary>
public sealed class CreateDepartmentHandler(IDepartmentRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateDepartmentCommand"/>.
    /// </summary>
    /// <param name="cmd">The create department command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created department ID.</returns>
    /// <exception cref="ArgumentException">Propagated from domain when name or email is null or whitespace.</exception>
    public async Task<int> HandleAsync(CreateDepartmentCommand cmd, CancellationToken ct)
    {
        var department = Department.Create(cmd.Name, cmd.Email);
        repo.Add(department);
        await uow.SaveChangesAsync(ct);
        return department.Id;
    }
}
