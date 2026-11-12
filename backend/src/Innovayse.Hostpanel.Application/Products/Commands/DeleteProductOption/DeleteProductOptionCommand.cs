namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductOption;

/// <summary>Removes one choice from an option group.</summary>
/// <param name="Id">Option primary key.</param>
public sealed record DeleteProductOptionCommand(int Id);
