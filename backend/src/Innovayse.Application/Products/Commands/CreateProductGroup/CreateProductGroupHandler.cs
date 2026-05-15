namespace Innovayse.Application.Products.Commands.CreateProductGroup;

using Innovayse.Application.Common;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Creates a new product group and persists it.</summary>
public sealed class CreateProductGroupHandler(
    IProductGroupRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles the <see cref="CreateProductGroupCommand"/>.
    /// </summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created product group ID.</returns>
    public async Task<int> HandleAsync(CreateProductGroupCommand cmd, CancellationToken ct)
    {
        var group = ProductGroup.Create(cmd.Name, cmd.Description);
        repo.Add(group);
        await uow.SaveChangesAsync(ct);
        return group.Id;
    }
}
