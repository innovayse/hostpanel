using Innovayse.Hostpanel.Domain.Support;

namespace Innovayse.Hostpanel.Domain.Tests.Support;

/// <summary>Unit tests for the invariants of <see cref="TicketReply.Create"/>.</summary>
public sealed class TicketReplyTests
{
    /// <summary>A null, empty or whitespace author is refused.</summary>
    /// <param name="author">The blank author name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithABlankAuthor_Throws(string? author)
    {
        var act = () => TicketReply.Create("Hello", author!, isStaffReply: true);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A blank message is refused.</summary>
    [Fact]
    public void Create_WithABlankMessage_Throws()
    {
        var act = () => TicketReply.Create(" ", "Agent", isStaffReply: true);

        act.Should().Throw<ArgumentException>();
    }
}
