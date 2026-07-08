namespace Innovayse.Domain.Tests.Email;

using FluentAssertions;
using Innovayse.Domain.Email;

/// <summary>Unit tests for the <see cref="EmailDomain"/> aggregate lifecycle.</summary>
public sealed class EmailDomainTests
{
    [Fact]
    public void Create_SetsStatusToPendingDns()
    {
        var domain = EmailDomain.Create(1, "example.com", 10, 50, 5120);

        domain.Status.Should().Be(EmailDomainStatus.PendingDns);
        domain.DomainName.Should().Be("example.com");
        domain.MaxMailboxes.Should().Be(10);
    }

    [Fact]
    public void Activate_FromPendingDns_SetsActive()
    {
        var domain = EmailDomain.Create(1, "example.com", 10, 50, 5120);

        domain.Activate();

        domain.Status.Should().Be(EmailDomainStatus.Active);
        domain.DnsVerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddMailbox_WhenNotActive_Throws()
    {
        var domain = EmailDomain.Create(1, "example.com", 10, 50, 5120);

        var act = () => domain.AddMailbox("john", "John", 512);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddMailbox_WhenActive_Succeeds()
    {
        var domain = EmailDomain.Create(1, "example.com", 10, 50, 5120);
        domain.Activate();

        var mailbox = domain.AddMailbox("john", "John Smith", 512);

        mailbox.LocalPart.Should().Be("john");
        mailbox.Email("example.com").Should().Be("john@example.com");
        domain.Mailboxes.Should().HaveCount(1);
    }

    [Fact]
    public void AddMailbox_ExceedsLimit_Throws()
    {
        var domain = EmailDomain.Create(1, "example.com", 1, 50, 5120);
        domain.Activate();
        domain.AddMailbox("first", "First", 512);

        var act = () => domain.AddMailbox("second", "Second", 512);

        act.Should().Throw<InvalidOperationException>().WithMessage("*limit*");
    }

    [Fact]
    public void AddMailbox_DuplicateLocalPart_Throws()
    {
        var domain = EmailDomain.Create(1, "example.com", 10, 50, 5120);
        domain.Activate();
        domain.AddMailbox("john", "John", 512);

        var act = () => domain.AddMailbox("john", "John Again", 512);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
    }
}
