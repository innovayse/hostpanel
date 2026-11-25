using Innovayse.Hostpanel.Domain.Admin;

namespace Innovayse.Hostpanel.Domain.Tests.Admin;

/// <summary>Unit tests for the <see cref="ActivityRead"/> entity.</summary>
public sealed class ActivityReadTests
{
    /// <summary>Create keeps the admin, the event key and the moment it was read.</summary>
    [Fact]
    public void Create_KeepsWhoReadWhatAndWhen()
    {
        var readAt = new DateTimeOffset(2026, 11, 25, 9, 30, 0, TimeSpan.Zero);

        var read = ActivityRead.Create("admin-subject", "InvoiceOverdue:33:639000000000000000", readAt);

        read.UserId.Should().Be("admin-subject");
        read.EventKey.Should().Be("InvoiceOverdue:33:639000000000000000");
        read.ReadAt.Should().Be(readAt);
    }
}
