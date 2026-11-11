namespace Innovayse.Hostpanel.Application.Clients.Commands.CreateClientGroup;

/// <summary>Command to create a client group. A new group is never the default; see <c>SetDefaultClientGroupCommand</c>.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Description">The optional description.</param>
/// <param name="DiscountPercent">The discount members are due, in percent from 0 to 100.</param>
/// <param name="Color">The colour the admin panel shows the group in, as <c>#RRGGBB</c>.</param>
public sealed record CreateClientGroupCommand(string Name, string? Description, decimal DiscountPercent, string Color);
