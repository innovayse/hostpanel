namespace Innovayse.Application.Tests.Products;

using Innovayse.Application.Common;
using Innovayse.Application.Products.Commands.CreateProductFeature;
using Innovayse.Application.Products.Commands.UpdateProductFeature;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Tests that a product cannot end up with two specification lines under one label.
/// </summary>
/// <remarks>
/// The storefront's comparison table keys its rows on the label and takes the first
/// value it finds, so a duplicate renders nowhere. An operator adding one would see
/// no change and have nothing to explain why, which is worse than a refusal.
/// </remarks>
public class ProductFeatureLabelUniquenessTests
{
    private readonly Mock<IProductFeatureRepository> _features = new();
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    /// <summary>Builds a product for the existence check to pass.</summary>
    private static Product AProduct() =>
        Product.Create(1, "Starter", null, null, null, null, ProductType.SharedHosting, 5m, 50m, null);

    /// <summary>Makes the product lookup succeed.</summary>
    private void ProductExists() =>
        _products
            .Setup(r => r.FindByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AProduct());

    /// <summary>Sets what the label check reports.</summary>
    /// <param name="taken">Whether another line already uses the label.</param>
    private void LabelTaken(bool taken) =>
        _features
            .Setup(r => r.ExistsWithLabelAsync(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(taken);

    /// <summary>Creating a second line under the same label is refused, and nothing is saved.</summary>
    [Fact]
    public async Task Create_RefusesADuplicateLabel()
    {
        ProductExists();
        LabelTaken(true);
        var handler = new CreateProductFeatureHandler(_features.Object, _products.Object, _uow.Object);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new CreateProductFeatureCommand(1, "Disk", "10 GB", 0), CancellationToken.None));

        Assert.Contains("Disk", ex.Message);
        _features.Verify(r => r.Add(It.IsAny<ProductFeature>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A label the product does not use yet is created as before.</summary>
    [Fact]
    public async Task Create_AllowsAFreeLabel()
    {
        ProductExists();
        LabelTaken(false);
        var handler = new CreateProductFeatureHandler(_features.Object, _products.Object, _uow.Object);

        await handler.HandleAsync(new CreateProductFeatureCommand(1, "Disk", "10 GB", 0), CancellationToken.None);

        _features.Verify(r => r.Add(It.IsAny<ProductFeature>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Renaming a line onto a label the product already uses is refused.</summary>
    [Fact]
    public async Task Update_RefusesARenameOntoAnExistingLabel()
    {
        _features
            .Setup(r => r.FindByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProductFeature.Create(1, "Bandwidth", "1 TB", 1));
        LabelTaken(true);
        var handler = new UpdateProductFeatureHandler(_features.Object, _uow.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UpdateProductFeatureCommand(7, "Disk", "10 GB", 1), CancellationToken.None));

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The line under edit is excluded from the check, so saving a line without renaming
    /// it still works — otherwise every edit would collide with itself.
    /// </summary>
    [Fact]
    public async Task Update_ExcludesTheLineUnderEdit()
    {
        _features
            .Setup(r => r.FindByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProductFeature.Create(1, "Disk", "10 GB", 0));
        LabelTaken(false);
        var handler = new UpdateProductFeatureHandler(_features.Object, _uow.Object);

        await handler.HandleAsync(new UpdateProductFeatureCommand(7, "Disk", "20 GB", 0), CancellationToken.None);

        _features.Verify(
            r => r.ExistsWithLabelAsync(It.IsAny<int>(), "Disk", 7, It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
