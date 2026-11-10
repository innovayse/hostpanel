namespace Innovayse.Hostpanel.Application.Clients.Commands.UpdateClientGroup;

/// <summary>Command to replace a client group's name, description, discount and colour. Whether it is the default is changed by <c>SetDefaultClientGroupCommand</c>.</summary>
/// <param name="Id">The group primary key. Overlaid from the route.</param>
/// <param name="Name">The new display name.</param>
/// <param name="Description">The new description, or null to clear it.</param>
/// <param name="DiscountPercent">The new discount, in percent from 0 to 100.</param>
/// <param name="Color">The new colour, as <c>#RRGGBB</c>.</param>
public sealed record UpdateClientGroupCommand(int Id, string Name, string? Description, decimal DiscountPercent, string Color);
