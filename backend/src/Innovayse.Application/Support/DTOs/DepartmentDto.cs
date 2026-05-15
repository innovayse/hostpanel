namespace Innovayse.Application.Support.DTOs;

/// <summary>DTO representing a support department.</summary>
/// <param name="Id">Department primary key.</param>
/// <param name="Name">The department display name.</param>
/// <param name="Email">The department email address.</param>
public record DepartmentDto(int Id, string Name, string Email);
