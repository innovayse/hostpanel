namespace Innovayse.Application.Support.Commands.UpdateDepartment;

/// <summary>Command to update an existing support department's name and email.</summary>
/// <param name="Id">The department primary key.</param>
/// <param name="Name">The new department display name.</param>
/// <param name="Email">The new department email address.</param>
public record UpdateDepartmentCommand(int Id, string Name, string Email);
