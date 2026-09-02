namespace Innovayse.Application.Tests.Orders;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Orders.Commands.PlaceOrder;
using Innovayse.Application.Resources;
using Innovayse.Domain.Auth.Interfaces;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Orders.Interfaces;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;
using Microsoft.Extensions.Localization;
using Moq;
using System.Reflection;
using Wolverine;
using Xunit;

/// <summary>
/// Proves a signed-in caller placing their first order gets a client record rather than a refusal.
/// </summary>
/// <remarks>
/// The state these cover is the ordinary one for anyone whose account lives in the SSO: they
/// authenticate, so a subject exists, but nothing has ever created this product's client row for
/// them. The handler used to fall through to the guest-checkout branch, which needs an email and
/// a password the validator never asks a signed-in caller for — so the branch always refused, and
/// the client area they were sent back to told them they had no account to order against. There
/// was no way out of that loop from inside the product.
/// </remarks>
public sealed class PlaceOrderClientProvisioningTests
{
    /// <summary>The caller's subject in every test below.</summary>
    private const string CallerSubject = "sso-subject-1";

    /// <summary>The product every order here is for.</summary>
    private const int ProductId = 3;

    /// <summary>Builds the product the order references.</summary>
    /// <returns>An active hosting product carrying <see cref="ProductId"/>.</returns>
    private static Product HostingProduct()
    {
        var product = Product.Create(
            groupId: 1, name: "Pro Hosting", description: null, website: null, slug: null,
            packageName: null, type: ProductType.SharedHosting,
            monthlyPrice: 19.99m, annualPrice: 199.99m);

        // Create leaves Id at 0 for EF to assign, and the handler looks the product up by id.
        // Entity.Id has a private setter, so the backing field is the only way in — the same
        // approach StartGatewayPaymentHandlerTests uses for the same reason.
        typeof(Innovayse.Domain.Common.Entity)
            .GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(product, ProductId);

        return product;
    }

    /// <summary>An order for one month of <see cref="HostingProduct"/>, as a signed-in caller sends it.</summary>
    /// <remarks>
    /// Name, email and password are all absent on purpose: <c>PlaceOrderValidator</c> requires
    /// them only of a caller with no subject, so this is exactly the shape the checkout form
    /// produces for someone who is signed in.
    /// </remarks>
    /// <returns>The command under test.</returns>
    private static PlaceOrderCommand SignedInOrder() => new(
        FirstName: null, LastName: null, Email: null, Password: null, Phone: null,
        PaymentMethod: "stripe",
        Items: [new PlaceOrderItemDto(ProductId, "monthly", null, null)]);

    /// <summary>
    /// Builds the handler over a world in which the caller's subject has no client row.
    /// </summary>
    /// <param name="added">Receives the client the handler creates, if it creates one.</param>
    /// <param name="provisioning">Observed so a test can assert it was never asked to make an account.</param>
    /// <param name="displayName">The credential's name claim.</param>
    /// <param name="email">The credential's email claim.</param>
    /// <returns>The handler under test.</returns>
    private static PlaceOrderHandler HandlerWithNoClientFor(
        List<Client> added,
        out Mock<IUserProvisioning> provisioning,
        string? displayName = "Ada Lovelace",
        string? email = "ada@example.com")
    {
        var clients = new Mock<IClientRepository>();
        clients.Setup(r => r.FindByUserIdAsync(CallerSubject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Client?)null);
        clients.Setup(r => r.Add(It.IsAny<Client>())).Callback<Client>(added.Add);

        var products = new Mock<IProductRepository>();
        products.Setup(r => r.FindByIdsAsync(It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([HostingProduct()]);

        var orders = new Mock<IOrderRepository>();
        orders.Setup(r => r.GetNextOrderNumberAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var caller = new Mock<ICurrentRequestContext>();
        caller.SetupGet(c => c.UserId).Returns(CallerSubject);
        caller.SetupGet(c => c.UserName).Returns(displayName);
        caller.SetupGet(c => c.UserEmail).Returns(email);

        // Strict: any call to the provisioner is a failure, asserted by the test that says so.
        provisioning = new Mock<IUserProvisioning>(MockBehavior.Strict);

        return new PlaceOrderHandler(
            orders.Object,
            products.Object,
            clients.Object,
            Mock.Of<IInvoiceRepository>(),
            Mock.Of<IUnitOfWork>(),
            provisioning.Object,
            Mock.Of<ISubjectRoleStore>(),
            Mock.Of<IMessageBus>(),
            caller.Object,
            Mock.Of<IStringLocalizer<ValidationMessages>>());
    }

    /// <summary>The order goes through, instead of being refused for a missing account.</summary>
    [Fact]
    public async Task FirstOrderFromASignedInCallerCreatesTheirClientRecord()
    {
        var added = new List<Client>();
        var handler = HandlerWithNoClientFor(added, out _);

        await handler.HandleAsync(SignedInOrder(), CancellationToken.None);

        Assert.Single(added);
        Assert.Equal(CallerSubject, added[0].UserId);
    }

    /// <summary>The row is filled from the credential, not from the order body.</summary>
    /// <remarks>
    /// The identity decides which account every later invoice and service belongs to, and a form
    /// field is something the caller can put anything in.
    /// </remarks>
    [Fact]
    public async Task TheNewClientIsNamedFromTheCredential()
    {
        var added = new List<Client>();
        var handler = HandlerWithNoClientFor(added, out _);

        await handler.HandleAsync(SignedInOrder(), CancellationToken.None);

        Assert.Equal("Ada", added[0].FirstName);
        Assert.Equal("Lovelace", added[0].LastName);
    }

    /// <summary>No account is provisioned: the caller already has one, which is how they got here.</summary>
    /// <remarks>
    /// It matters because where an SSO owns the accounts the provisioner refuses, and calling it
    /// would turn a first order back into that refusal.
    /// </remarks>
    [Fact]
    public async Task NoAccountIsProvisionedForACallerWhoAlreadyHasOne()
    {
        var added = new List<Client>();
        var handler = HandlerWithNoClientFor(added, out var provisioning);

        await handler.HandleAsync(SignedInOrder(), CancellationToken.None);

        provisioning.VerifyNoOtherCalls();
    }

    /// <summary>A one-word display name still produces a usable row.</summary>
    /// <remarks>
    /// Splitting a display name into two fields is a guess, and this is the case where the guess
    /// has nothing to give the second field. Refusing the order over it would be worse than a
    /// blank surname the customer can correct in their profile.
    /// </remarks>
    [Fact]
    public async Task ASingleWordNameLeavesTheSurnameBlankRatherThanFailing()
    {
        var added = new List<Client>();
        var handler = HandlerWithNoClientFor(added, out _, displayName: "Ada");

        await handler.HandleAsync(SignedInOrder(), CancellationToken.None);

        Assert.Equal("Ada", added[0].FirstName);
        Assert.Equal(string.Empty, added[0].LastName);
    }

    /// <summary>A credential carrying no name at all is still enough to order.</summary>
    [Fact]
    public async Task ACredentialWithNoNameStillOrders()
    {
        var added = new List<Client>();
        var handler = HandlerWithNoClientFor(added, out _, displayName: null);

        await handler.HandleAsync(SignedInOrder(), CancellationToken.None);

        Assert.Single(added);
        Assert.Equal(CallerSubject, added[0].UserId);
    }
}
