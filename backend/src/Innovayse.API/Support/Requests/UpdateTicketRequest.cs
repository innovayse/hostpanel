namespace Innovayse.API.Support.Requests;

/// <summary>Request body for updating ticket metadata.</summary>
public sealed class UpdateTicketRequest
{
    /// <summary>Gets the new status, or <see langword="null"/> to leave unchanged.</summary>
    public string? Status { get; init; }

    /// <summary>Gets the new priority, or <see langword="null"/> to leave unchanged.</summary>
    public string? Priority { get; init; }

    /// <summary>Gets the new department FK, or <see langword="null"/> to leave unchanged.</summary>
    public int? DepartmentId { get; init; }

    /// <summary>Gets the new staff assignment FK, or <see langword="null"/> to leave unchanged.</summary>
    public int? AssignedToStaffId { get; init; }
}
