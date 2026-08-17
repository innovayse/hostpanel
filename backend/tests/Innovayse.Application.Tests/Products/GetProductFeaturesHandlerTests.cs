namespace Innovayse.Application.Tests.Products;

using Innovayse.Application.Products.Queries.GetProductFeatures;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Moq;
using Xunit;

/// <summary>Tests for <see cref="GetProductFeaturesHandler"/>.</summary>
public class GetProductFeaturesHandlerTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IProductFeatureRepository> _features = new();

    /// <summary>Builds the handler under test.</summary>
    private GetProductFeaturesHandler Handler() => new(_products.Object, _features.Object);

    /// <summary>Builds a product; its Id stays 0, which is all these tests need.</summary>
    /// <param name="name">Product name.</param>
    private static Product AProduct(string name) =>
        Product.Create(1, name, null, null, null, null, ProductType.SharedHosting, 5m, 50m, null);

    /// <summary>
    /// A group with no products asks for no lines at all.
    /// <para>
    /// Worth pinning: passing an empty id set to a repository that turns it into
    /// <c>WHERE product_id IN ()</c> would either fail or, worse, return the whole
    /// table — and this endpoint is served to anonymous visitors.
    /// </para>
    /// </summary>
    [Fact]
    public async Task HandleAsync_NoProducts_ReturnsEmptyAndAsksForNoLines()
    {
        _products
            .Setup(r => r.ListAsync(It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _features
            .Setup(r => r.ListForProductsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await Handler().HandleAsync(new GetProductFeaturesQuery(1, null), CancellationToken.None);

        Assert.Empty(result);
        _features.Verify(
            r => r.ListForProductsAsync(It.Is<IEnumerable<int>>(ids => !ids.Any()), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>The group filter is passed through to the product repository.</summary>
    [Fact]
    public async Task HandleAsync_PassesGroupFilterThrough()
    {
        _products
            .Setup(r => r.ListAsync(It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AProduct("Start")]);
        _features
            .Setup(r => r.ListForProductsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Handler().HandleAsync(new GetProductFeaturesQuery(4, null, false), CancellationToken.None);

        _products.Verify(r => r.ListAsync(4, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Each stored line is mapped to a DTO with its text and position intact.</summary>
    [Fact]
    public async Task HandleAsync_MapsLinesToDtos()
    {
        _products
            .Setup(r => r.ListAsync(It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([AProduct("Start")]);
        _features
            .Setup(r => r.ListForProductsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                ProductFeature.Create(0, "Disk", "10 GB", 0),
                ProductFeature.Create(0, "Bandwidth", "1 TB", 1),
            ]);

        var result = await Handler().HandleAsync(new GetProductFeaturesQuery(1, null), CancellationToken.None);

        Assert.Collection(
            result,
            line =>
            {
                Assert.Equal("Disk", line.Label);
                Assert.Equal("10 GB", line.Value);
                Assert.Equal(0, line.SortOrder);
            },
            line =>
            {
                Assert.Equal("Bandwidth", line.Label);
                Assert.Equal("1 TB", line.Value);
                Assert.Equal(1, line.SortOrder);
            });
    }
}
