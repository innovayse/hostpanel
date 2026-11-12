namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductOptionGroup;

/// <summary>Removes one option group together with its options.</summary>
/// <param name="Id">Option group primary key.</param>
public sealed record DeleteProductOptionGroupCommand(int Id);
