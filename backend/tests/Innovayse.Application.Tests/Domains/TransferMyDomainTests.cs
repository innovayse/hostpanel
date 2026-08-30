namespace Innovayse.Application.Tests.Domains;

using Innovayse.Application.Common;
using Innovayse.Application.Domains.Commands.TransferDomain;
using Innovayse.Application.Domains.Commands.TransferMyDomain;
using Innovayse.Application.Orders.Commands.PlaceOrder;
using Innovayse.Application.Resources;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wolverine;
using Xunit;

/// <summary>
/// Proves a client cannot transfer a domain in to an account that is not theirs.
/// <para>
/// This is the failure mode the repository has actually shipped: a client-facing route wired to
/// an admin use case whose command takes its target client from the request body. The portal's
/// transfer page had no backend at all, and the tempting fix was to point it at
/// <c>POST /api/domains/transfer</c> and <see cref="TransferDomainCommand"/>. These tests pin
/// the reasons that is not what was built.
/// </para>
/// </summary>
public sealed class TransferMyDomainTests
{
    /// <summary>Identity subject of the caller in every test below.</summary>
    private const string CallerSubject = "user-caller";

    /// <summary>Primary key of the domain product a domain order hangs off.</summary>
    private const int DomainProductId = 9;

    /// <summary>A well-formed transfer request, as the portal's form produces one.</summary>
    /// <returns>The command under test.</returns>
    private static TransferMyDomainCommand ValidCommand() =>
        new("example.com", "EPP-SECRET-123", Years: 1, PaymentMethod: "innovayse-inecobank");

    /// <summary>An active product of type <see cref="ProductType.Domain"/>.</summary>
    /// <returns>The product a domain order line hangs off.</returns>
    private static Product DomainProduct()
    {
        var product = Product.Create(
            groupId: 1, name: "Domain Registration", description: null, website: null,
            slug: null, packageName: null, ProductType.Domain, monthlyPrice: 0m, annualPrice: 0m);

        SetId(product, DomainProductId);
        return product;
    }

    /// <summary>
    /// Overrides <see cref="Innovayse.Domain.Common.Entity.Id"/> via reflection (its setter is
    /// private) so the product carries an id no factory would give it, and a test asserting
    /// "this product and not that one" cannot pass by both being 0.
    /// </summary>
    /// <param name="entity">The entity to stamp.</param>
    /// <param name="id">The identifier to give it.</param>
    private static void SetId(Innovayse.Domain.Common.Entity entity, int id)
    {
        var backingField = typeof(Innovayse.Domain.Common.Entity).GetField(
            "<Id>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        backingField.SetValue(entity, id);
    }

    /// <summary>
    /// Builds the handler over the given world.
    /// </summary>
    /// <param name="bus">The bus the handler dispatches through.</param>
    /// <param name="products">Active products the repository answers with.</param>
    /// <param name="subject">The caller's subject, or <see langword="null"/> for no credential.</param>
    /// <returns>The handler under test.</returns>
    private static TransferMyDomainHandler HandlerOver(
        Mock<IMessageBus> bus, IReadOnlyList<Product> products, string? subject = CallerSubject)
    {
        var productRepo = new Mock<IProductRepository>();
        productRepo
            .Setup(r => r.ListAsync(null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        var caller = new Mock<ICurrentRequestContext>();
        if (subject is null)
        {
            caller.Setup(c => c.RequireUserId()).Throws<UnauthorizedAccessException>();
        }
        else
        {
            caller.Setup(c => c.RequireUserId()).Returns(subject);
        }

        var localizer = new Mock<IStringLocalizer<ValidationMessages>>();
        localizer
            .Setup(l => l[It.IsAny<string>()])
            .Returns(new LocalizedString("NoDomainProductConfigured", "Refused."));

        return new TransferMyDomainHandler(
            productRepo.Object,
            caller.Object,
            bus.Object,
            localizer.Object,
            NullLogger<TransferMyDomainHandler>.Instance);
    }

    /// <summary>
    /// The command must not carry a client id. This is the whole difference between it and
    /// <see cref="TransferDomainCommand"/>, which does and is therefore admin-only: an id in the
    /// message is an id a caller can send, and a transfer placed against somebody else's account
    /// bills them and lands the domain on their books.
    /// </summary>
    [Fact]
    public void TransferMyDomainCommand_NamesNoClient()
    {
        var mine = typeof(TransferMyDomainCommand).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("ClientId", mine);

        // The admin command is the contrast, and the reason this one exists at all.
        Assert.Contains("ClientId", typeof(TransferDomainCommand).GetProperties().Select(p => p.Name));
    }

    /// <summary>
    /// The handler must not reach <see cref="TransferDomainCommand"/> directly. That command
    /// calls the registrar at once, names a client, and raises no invoice; dispatching it from a
    /// client-facing route would be both the IDOR and a free domain transfer.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_NeverDispatchesTheAdminTransferCommandAsync()
    {
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.InvokeAsync<PlaceOrderResultDto>(
                It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(new PlaceOrderResultDto(1, 2));

        var handler = HandlerOver(bus, [DomainProduct()]);

        await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        bus.Verify(
            b => b.InvokeAsync<int>(
                It.IsAny<TransferDomainCommand>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    /// <summary>
    /// What it does dispatch is an order that also names no client — so the account is resolved
    /// from the credential the whole way down, and the transfer is invoiced rather than given away.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_PlacesAnOrderThatNamesNoClientAsync()
    {
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.InvokeAsync<PlaceOrderResultDto>(
                It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(new PlaceOrderResultDto(11, 22));

        var handler = HandlerOver(bus, [DomainProduct()]);

        var result = await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        Assert.Equal(11, result.OrderId);
        Assert.Equal(22, result.InvoiceId);

        Assert.DoesNotContain(
            "ClientId", typeof(PlaceOrderCommand).GetProperties().Select(p => p.Name));

        bus.Verify(
            b => b.InvokeAsync<PlaceOrderResultDto>(
                It.Is<PlaceOrderCommand>(c =>
                    c.PaymentMethod == "innovayse-inecobank"
                    && c.Email == null
                    && c.Password == null
                    && c.Items.Count == 1
                    && c.Items[0].ProductId == DomainProductId
                    && c.Items[0].Domain == "example.com"
                    && c.Items[0].DomainAction == "transfer"
                    && c.Items[0].EppCode == "EPP-SECRET-123"
                    && c.Items[0].Years == 1),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }

    /// <summary>
    /// A request with no credential is refused here rather than being allowed to fall through.
    /// <c>PlaceOrderHandler</c> reads "no subject" as guest checkout and would register a brand
    /// new account, which behind <c>/api/me/domains</c> would mean a transfer landing on an
    /// account nobody asked for.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_WithNoCredential_RefusesWithoutOrderingAsync()
    {
        var bus = new Mock<IMessageBus>(MockBehavior.Strict);
        var handler = HandlerOver(bus, [DomainProduct()], subject: null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.HandleAsync(ValidCommand(), CancellationToken.None));

        bus.VerifyNoOtherCalls();
    }

    /// <summary>
    /// With no domain product configured there is nothing to sell a transfer as, and the caller
    /// gets a sentence rather than a 500 — and no order is placed.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_WithNoDomainProduct_RefusesWithoutOrderingAsync()
    {
        var bus = new Mock<IMessageBus>(MockBehavior.Strict);

        var hostingOnly = Product.Create(
            groupId: 1, name: "Starter Hosting", description: null, website: null,
            slug: null, packageName: null, ProductType.SharedHosting, 1000m, 10000m);

        var handler = HandlerOver(bus, [hostingOnly]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(ValidCommand(), CancellationToken.None));

        bus.VerifyNoOtherCalls();
    }

    /// <summary>
    /// The domain product is chosen by <see cref="ProductType.Domain"/>, not by whether its name
    /// happens to contain "domain" — which is how the public search page picks it, and is a guess.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_PicksTheDomainProductByTypeNotByNameAsync()
    {
        var bus = new Mock<IMessageBus>();
        bus.Setup(b => b.InvokeAsync<PlaceOrderResultDto>(
                It.IsAny<object>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(new PlaceOrderResultDto(1, 2));

        // A hosting plan whose name contains "Domain" comes first in the list. Name-matching
        // would pick it and bill a transfer as hosting.
        var decoy = Product.Create(
            groupId: 1, name: "Domain + Hosting Bundle", description: null, website: null,
            slug: null, packageName: null, ProductType.SharedHosting, 1000m, 10000m);

        var handler = HandlerOver(bus, [decoy, DomainProduct()]);

        await handler.HandleAsync(ValidCommand(), CancellationToken.None);

        bus.Verify(
            b => b.InvokeAsync<PlaceOrderResultDto>(
                It.Is<PlaceOrderCommand>(c => c.Items[0].ProductId == DomainProductId),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()),
            Times.Once);
    }
}
