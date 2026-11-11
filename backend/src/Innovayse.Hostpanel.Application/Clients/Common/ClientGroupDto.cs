namespace Innovayse.Hostpanel.Application.Clients.Common;

/// <summary>A client group as staff see it, with how many clients belong to it.</summary>
/// <param name="Id">Group primary key.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The free-text description, or null when there is none.</param>
/// <param name="DiscountPercent">The discount members are due, in percent from 0 to 100.</param>
/// <param name="Color">The colour the admin panel shows the group in, as upper-case <c>#RRGGBB</c>.</param>
/// <param name="IsDefault">Whether new clients created without a group join this one.</param>
/// <param name="ClientCount">How many clients belong to the group.</param>
public sealed record ClientGroupDto(
    int Id,
    string Name,
    string? Description,
    decimal DiscountPercent,
    string Color,
    bool IsDefault,
    int ClientCount);
