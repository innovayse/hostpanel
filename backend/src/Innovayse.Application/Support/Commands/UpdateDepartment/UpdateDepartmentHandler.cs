namespace Innovayse.Application.Support.Commands.UpdateDepartment;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Updates an existing support department's name and email.
/// </summary>
public sealed class UpdateDepartmentHandler(IDepartmentRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateDepartmentCommand"/>.
    /// </summary>
    /// <param name="cmd">The update department command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the department is not found.</exception>
    public async Task HandleAsync(UpdateDepartmentCommand cmd, CancellationToken ct)
    {
        var department = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Department {cmd.Id} not found.");

        department.Update(cmd.Name, cmd.Email);
        repo.Update(department);
        await uow.SaveChangesAsync(ct);
    }
}
