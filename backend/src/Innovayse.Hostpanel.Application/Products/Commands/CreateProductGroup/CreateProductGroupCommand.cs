namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProductGroup;

/// <summary>Command to create a new product group.</summary>
/// <param name="Name">Display name for the group.</param>
/// <param name="Description">Optional description.</param>
/// <param name="AllowedPaymentMethods">
/// Payment method module ids the group may be paid with; <see langword="null"/> or empty leaves every method on offer.
/// </param>
public sealed record CreateProductGroupCommand(
    string Name, string? Description, IReadOnlyList<string>? AllowedPaymentMethods = null);
