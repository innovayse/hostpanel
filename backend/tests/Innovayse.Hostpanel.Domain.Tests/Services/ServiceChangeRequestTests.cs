using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Services;

/// <summary>Unit tests for the <see cref="ServiceChangeRequest"/> entity: its creation and its status transitions.</summary>
public sealed class ServiceChangeRequestTests
{
    /// <summary>The moment the tests make and resolve requests at.</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a pending upgrade request for service 7 of client 3, from product 1 to product 2.</summary>
    /// <returns>A new pending request.</returns>
    private static ServiceChangeRequest PendingUpgrade() =>
        ServiceChangeRequest.Create(7, 3, 1, 2, new ProratedPlanChange(ServiceChangeType.Upgrade, 4.5m), "usd", 20m, Now);

    /// <summary>A new request is pending, open, and carries the amounts it was made with.</summary>
    [Fact]
    public void Create_StartsPendingWithTheQuotedAmounts()
    {
        var request = PendingUpgrade();

        request.Status.Should().Be(ServiceChangeStatus.Pending);
        request.IsOpen.Should().BeTrue();
        request.Type.Should().Be(ServiceChangeType.Upgrade);
        request.ServiceId.Should().Be(7);
        request.ClientId.Should().Be(3);
        request.FromProductId.Should().Be(1);
        request.ToProductId.Should().Be(2);
        request.ProrationAmount.Should().Be(4.5m);
        request.NewRecurringAmount.Should().Be(20m);
        request.CurrencyCode.Should().Be("USD");
        request.CreatedAt.Should().Be(Now);
        request.InvoiceId.Should().BeNull();
        request.ResolvedAt.Should().BeNull();
    }

    /// <summary>A plan change moves a service to another product.</summary>
    [Fact]
    public void Create_ToTheSameProduct_Throws()
    {
        var act = () => ServiceChangeRequest.Create(
            7, 3, 1, 1, new ProratedPlanChange(ServiceChangeType.Upgrade, 1m), "USD", 20m, Now);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>An amount below zero is refused.</summary>
    [Fact]
    public void Create_WithANegativeNewPrice_Throws()
    {
        var act = () => ServiceChangeRequest.Create(
            7, 3, 1, 2, new ProratedPlanChange(ServiceChangeType.Upgrade, 1m), "USD", -1m, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>Approving a pending request keeps the staff note and leaves the request open.</summary>
    [Fact]
    public void Approve_FromPending_MovesToApprovedWithTheNote()
    {
        var request = PendingUpgrade();

        request.Approve("Looks fine");

        request.Status.Should().Be(ServiceChangeStatus.Approved);
        request.Note.Should().Be("Looks fine");
        request.IsOpen.Should().BeTrue();
        request.ResolvedAt.Should().BeNull();
    }

    /// <summary>Only a pending request can be approved.</summary>
    /// <param name="status">A status other than pending, reached through the entity's own methods.</param>
    [Theory]
    [InlineData(ServiceChangeStatus.Approved)]
    [InlineData(ServiceChangeStatus.Completed)]
    [InlineData(ServiceChangeStatus.Rejected)]
    public void Approve_FromAnyOtherStatus_Throws(ServiceChangeStatus status)
    {
        var request = InStatus(status);

        var act = () => request.Approve();

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceChangeInvalidState);
    }

    /// <summary>An approved request records the invoice it waits for.</summary>
    [Fact]
    public void AttachInvoice_WhenApproved_RecordsTheInvoice()
    {
        var request = InStatus(ServiceChangeStatus.Approved);

        request.AttachInvoice(55);

        request.InvoiceId.Should().Be(55);
    }

    /// <summary>A pending request has no invoice yet: it is not approved.</summary>
    [Fact]
    public void AttachInvoice_WhenPending_Throws()
    {
        var request = PendingUpgrade();

        var act = () => request.AttachInvoice(55);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceChangeInvalidState);
    }

    /// <summary>Completing an approved request closes it and stamps the moment.</summary>
    [Fact]
    public void Complete_FromApproved_MovesToCompleted()
    {
        var request = InStatus(ServiceChangeStatus.Approved);
        var later = Now.AddHours(2);

        request.Complete(later);

        request.Status.Should().Be(ServiceChangeStatus.Completed);
        request.ResolvedAt.Should().Be(later);
        request.IsOpen.Should().BeFalse();
    }

    /// <summary>A request is completed from Approved only: not before staff or the payment approved it, and not twice.</summary>
    /// <param name="status">A status other than approved.</param>
    [Theory]
    [InlineData(ServiceChangeStatus.Pending)]
    [InlineData(ServiceChangeStatus.Completed)]
    [InlineData(ServiceChangeStatus.Rejected)]
    public void Complete_FromAnyOtherStatus_Throws(ServiceChangeStatus status)
    {
        var request = InStatus(status);

        var act = () => request.Complete(Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceChangeInvalidState);
    }

    /// <summary>A pending or an approved request can be rejected; the note and the moment are kept.</summary>
    /// <param name="status">An open status.</param>
    [Theory]
    [InlineData(ServiceChangeStatus.Pending)]
    [InlineData(ServiceChangeStatus.Approved)]
    public void Reject_FromAnOpenStatus_MovesToRejected(ServiceChangeStatus status)
    {
        var request = InStatus(status);

        request.Reject("Not possible", Now.AddHours(1));

        request.Status.Should().Be(ServiceChangeStatus.Rejected);
        request.Note.Should().Be("Not possible");
        request.ResolvedAt.Should().Be(Now.AddHours(1));
        request.IsOpen.Should().BeFalse();
    }

    /// <summary>Rejecting without a note keeps the note given on approval.</summary>
    [Fact]
    public void Reject_WithoutANote_KeepsTheEarlierOne()
    {
        var request = PendingUpgrade();
        request.Approve("First thought");

        request.Reject(null, Now);

        request.Note.Should().Be("First thought");
    }

    /// <summary>A completed plan change cannot be taken back, and a rejected one is not rejected twice.</summary>
    /// <param name="status">A closed status.</param>
    [Theory]
    [InlineData(ServiceChangeStatus.Completed)]
    [InlineData(ServiceChangeStatus.Rejected)]
    public void Reject_FromAClosedStatus_Throws(ServiceChangeStatus status)
    {
        var request = InStatus(status);

        var act = () => request.Reject(null, Now);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceChangeInvalidState);
    }

    /// <summary>A rejection that someone else made first can still receive the staff note, and without one it keeps the earlier note.</summary>
    [Fact]
    public void AmendRejectionNote_OnARejectedRequest_SetsTheNoteAndKeepsTheOtherWhenNone()
    {
        var request = PendingUpgrade();
        request.Reject(null, Now);

        request.AmendRejectionNote("Staff reason");
        request.AmendRejectionNote(null);

        request.Note.Should().Be("Staff reason");
        request.Status.Should().Be(ServiceChangeStatus.Rejected);
    }

    /// <summary>The note can only be amended on a rejected request.</summary>
    /// <param name="status">A status other than Rejected.</param>
    [Theory]
    [InlineData(ServiceChangeStatus.Pending)]
    [InlineData(ServiceChangeStatus.Approved)]
    [InlineData(ServiceChangeStatus.Completed)]
    public void AmendRejectionNote_OnARequestThatIsNotRejected_Throws(ServiceChangeStatus status)
    {
        var request = InStatus(status);

        var act = () => request.AmendRejectionNote("Staff reason");

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(ServicesErrors.ServiceChangeInvalidState);
    }

    /// <summary>Builds a request and moves it to a status through the entity's own methods.</summary>
    /// <param name="status">The status to reach.</param>
    /// <returns>A request in that status.</returns>
    private static ServiceChangeRequest InStatus(ServiceChangeStatus status)
    {
        var request = PendingUpgrade();
        if (status is ServiceChangeStatus.Pending)
        {
            return request;
        }

        if (status is ServiceChangeStatus.Rejected)
        {
            request.Reject(null, Now);
            return request;
        }

        request.Approve();
        if (status is ServiceChangeStatus.Completed)
        {
            request.Complete(Now);
        }

        return request;
    }
}
