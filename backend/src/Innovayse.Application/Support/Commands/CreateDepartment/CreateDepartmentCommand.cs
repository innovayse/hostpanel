namespace Innovayse.Application.Support.Commands.CreateDepartment;

/// <summary>Command to create a new support department.</summary>
/// <param name="Name">The department display name.</param>
/// <param name="Email">The department email address.</param>
public record CreateDepartmentCommand(string Name, string Email);
