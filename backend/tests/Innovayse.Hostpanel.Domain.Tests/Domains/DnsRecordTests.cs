using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Domains;

/// <summary>Unit tests for <see cref="DnsRecord"/>.</summary>
public sealed class DnsRecordTests
{
    /// <summary>CreateDetached builds a record carrying every field and bound to no domain.</summary>
    [Fact]
    public void CreateDetached_CarriesFieldsWithoutADomain()
    {
        var record = DnsRecord.CreateDetached(DnsRecordType.A, "@", "1.2.3.4", 3600, 10);

        record.DomainId.Should().Be(0);
        record.Type.Should().Be(DnsRecordType.A);
        record.Host.Should().Be("@");
        record.Value.Should().Be("1.2.3.4");
        record.Ttl.Should().Be(3600);
        record.Priority.Should().Be(10);
    }

    /// <summary>ToDetached copies the record into one bound to no domain, so the copy keeps how the record stood.</summary>
    [Fact]
    public void ToDetached_CopiesTheRecordWithoutADomain()
    {
        var now = new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
        var domain = RegisteredDomain.Register(1, "example.com", now, now.AddYears(1), true, false, "USD");
        domain.AddDnsRecord(DnsRecordType.MX, "mail", "mx.example.test", 300, 5);
        var original = domain.DnsRecords.Single();

        var copy = original.ToDetached();

        copy.Should().NotBeSameAs(original);
        copy.DomainId.Should().Be(0);
        copy.Type.Should().Be(DnsRecordType.MX);
        copy.Host.Should().Be("mail");
        copy.Value.Should().Be("mx.example.test");
        copy.Ttl.Should().Be(300);
        copy.Priority.Should().Be(5);
    }
}
