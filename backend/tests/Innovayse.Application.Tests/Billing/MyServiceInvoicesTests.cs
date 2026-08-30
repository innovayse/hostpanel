namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Billing.Queries.GetMyServiceInvoices;
using Innovayse.Application.Services.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Moq;
using Xunit;

/// <summary>
/// Proves that reading "the invoices for this service" is scoped to the caller, and that an
/// empty answer is never dressed up as "nothing was charged".
/// </summary>
public sealed class MyServiceInvoicesTests
{
    /// <summary>The service id every probe asks for.</summary>
    private const int ServiceId = 7;

    /// <summary>The caller's own client id, as the ownership rule reports it.</summary>
    private const int CallerClientId = 31;

    /// <summary>A client id that is deliberately not the caller's.</summary>
    private const int StrangersClientId = 4242;

    /// <summary>An ownership rule that refuses the service outright.</summary>
    /// <returns>A rule that throws <see cref="MyServiceNotFoundException"/>.</returns>
    private static Mock<IServiceOwnership> RefusingOwnership()
    {
        var ownership = new Mock<IServiceOwnership>();
        ownership
            .Setup(o => o.RequireOwnedByCallerAsync(ServiceId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MyServiceNotFoundException(ServiceId));
        return ownership;
    }

    /// <summary>An ownership rule that accepts and reports the caller's own client id.</summary>
    /// <returns>A rule that completes.</returns>
    private static Mock<IServiceOwnership> AcceptingOwnership()
    {
        var ownership = new Mock<IServiceOwnership>();
        ownership
            .Setup(o => o.RequireOwnedByCallerAsync(ServiceId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        ownership
            .Setup(o => o.RequireCallerClientIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerClientId);
        return ownership;
    }

    /// <summary>
    /// The whole point: a service that is not the caller's yields no invoices, and the invoice
    /// repository is never asked -- so there is no read to leak even if the filter were wrong.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_WhenServiceIsNotTheCallers_RefusesBeforeReadingAnyInvoiceAsync()
    {
        var invoices = new Mock<IInvoiceRepository>(MockBehavior.Strict);
        var handler = new GetMyServiceInvoicesHandler(RefusingOwnership().Object, invoices.Object);

        await Assert.ThrowsAsync<MyServiceNotFoundException>(
            () => handler.HandleAsync(new GetMyServiceInvoicesQuery(ServiceId), CancellationToken.None));

        invoices.VerifyNoOtherCalls();
    }

    /// <summary>
    /// The invoice read is scoped to the caller's own client id, taken from the ownership rule
    /// rather than from anything the query carried. A service id that somehow passed the first
    /// check still cannot reach another account's invoices.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_ScopesTheInvoiceReadToTheCallersOwnClientAsync()
    {
        var invoices = new Mock<IInvoiceRepository>();
        invoices
            .Setup(r => r.ListByClientServiceAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        invoices
            .Setup(r => r.CountUnattributedByClientAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new GetMyServiceInvoicesHandler(AcceptingOwnership().Object, invoices.Object);

        await handler.HandleAsync(new GetMyServiceInvoicesQuery(ServiceId), CancellationToken.None);

        invoices.Verify(
            r => r.ListByClientServiceAsync(CallerClientId, ServiceId, It.IsAny<CancellationToken>()),
            Times.Once);
        invoices.Verify(
            r => r.ListByClientServiceAsync(StrangersClientId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        invoices.Verify(
            r => r.CountUnattributedByClientAsync(CallerClientId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// An empty list is reported together with how many invoices could not be attributed to any
    /// service, so the portal can say "not recorded" rather than "never charged". Collapsing
    /// this to a bare list is the regression this test exists to catch.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_WhenNothingIsLinked_StillReportsTheUnattributableCountAsync()
    {
        var invoices = new Mock<IInvoiceRepository>();
        invoices
            .Setup(r => r.ListByClientServiceAsync(
                CallerClientId, ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        invoices
            .Setup(r => r.CountUnattributedByClientAsync(CallerClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var handler = new GetMyServiceInvoicesHandler(AcceptingOwnership().Object, invoices.Object);

        var result = await handler.HandleAsync(
            new GetMyServiceInvoicesQuery(ServiceId), CancellationToken.None);

        Assert.Empty(result.Invoices);
        Assert.Equal(3, result.UnattributedInvoiceCount);
    }

    /// <summary>Linked invoices come back mapped, newest-first order preserved from the repository.</summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task HandleAsync_ReturnsTheLinkedInvoicesAsync()
    {
        var linked = Invoice.Create(CallerClientId, DateTimeOffset.UtcNow.AddDays(14));
        linked.AddItem("Renewal: Starter Hosting", 5000m, 1, clientServiceId: ServiceId);

        var invoices = new Mock<IInvoiceRepository>();
        invoices
            .Setup(r => r.ListByClientServiceAsync(
                CallerClientId, ServiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([linked]);
        invoices
            .Setup(r => r.CountUnattributedByClientAsync(CallerClientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new GetMyServiceInvoicesHandler(AcceptingOwnership().Object, invoices.Object);

        var result = await handler.HandleAsync(
            new GetMyServiceInvoicesQuery(ServiceId), CancellationToken.None);

        var only = Assert.Single(result.Invoices);
        Assert.Equal(CallerClientId, only.ClientId);
        Assert.Equal(5000m, only.Total);
        Assert.Equal(0, result.UnattributedInvoiceCount);
    }

    /// <summary>
    /// The query must not carry a client id. An id in the message is an id a caller can send,
    /// and this one is dispatched from a route whose sibling actions check nothing.
    /// </summary>
    [Fact]
    public void GetMyServiceInvoicesQuery_NamesNoClient()
    {
        var names = typeof(GetMyServiceInvoicesQuery)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("ClientId", names);
        Assert.Contains("ServiceId", names);
    }
}
