namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductGroup;

/// <summary>Command to update a product group's name, description and allowed payment methods.</summary>
/// <param name="Id">
/// Product group primary key; wins over any id in the body. A value type, so it is left
/// non-nullable: a missing <c>int</c> in the body binds to <c>0</c> rather than being treated as
/// a required member, and the route overlay (<c>command with { Id = id }</c>) replaces it.
/// </param>
/// <param name="Name">New display name.</param>
/// <param name="Description">New description.</param>
/// <param name="AllowedPaymentMethods">
/// The payment method module ids the group may be paid with, replacing the earlier set; empty lifts the restriction, and
/// <see langword="null"/> keeps the stored set, so a caller that does not know the field never changes it.
/// </param>
public sealed record UpdateProductGroupCommand(
    int Id, string Name, string? Description, IReadOnlyList<string>? AllowedPaymentMethods = null);
