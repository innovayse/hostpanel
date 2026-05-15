namespace Innovayse.Application.Admin.Servers.DTOs;

/// <summary>
/// DTO for a server group, including all servers assigned to it.
/// </summary>
/// <param name="Id">Unique group identifier.</param>
/// <param name="Name">Display name of the group.</param>
/// <param name="FillType">Account distribution strategy ("LeastFull" or "FillUntilFull").</param>
/// <param name="Servers">Servers belonging to this group.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
public record ServerGroupDto(
    int Id,
    string Name,
    string FillType,
    List<ServerDto> Servers,
    DateTimeOffset CreatedAt);
