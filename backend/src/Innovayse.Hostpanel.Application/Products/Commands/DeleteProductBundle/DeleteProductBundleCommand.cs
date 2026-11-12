namespace Innovayse.Hostpanel.Application.Products.Commands.DeleteProductBundle;

/// <summary>Command to delete a product bundle; orders placed from it keep existing and lose the link.</summary>
/// <param name="Id">Product bundle primary key.</param>
public sealed record DeleteProductBundleCommand(int Id);
