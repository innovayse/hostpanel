using System.Xml.Linq;
using Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.Plesk;

/// <summary>Tests for <see cref="PleskPacket"/>: how a request is wrapped and how each reply shape is read.</summary>
public sealed class PleskPacketTests
{
    /// <summary>An operation is wrapped in one packet element, unformatted.</summary>
    [Fact]
    public void Build_WrapsTheOperationInAPacket()
    {
        var body = PleskPacket.Build(new XElement("server", new XElement("get")));

        body.Should().Be("<packet><server><get /></server></packet>");
    }

    /// <summary>The call's status is the result's own, not the account status inside <c>gen_info</c>.</summary>
    [Fact]
    public void Parse_ReadsTheResultStatus_NotTheAccountStatus()
    {
        var reply = PleskPacket.Parse(
            "<packet><customer><get><result><status>error</status><errcode>1013</errcode><data><gen_info><status>0</status></gen_info></data></result></get></customer></packet>");

        reply.IsSuccess.Should().BeFalse();
        reply.ErrorCode.Should().Be(1013);
    }

    /// <summary>A successful result is handed back for the caller to read.</summary>
    [Fact]
    public void Parse_ReturnsTheResult_OnSuccess()
    {
        var reply = PleskPacket.Parse("<packet><customer><add><result><status>ok</status><id>2</id></result></add></customer></packet>");

        reply.IsSuccess.Should().BeTrue();
        reply.Result!.Element("id")!.Value.Should().Be("2");
    }

    /// <summary>A refusal raised before the operation is read (wrong key, plain HTTP) is read from <c>system</c>.</summary>
    [Fact]
    public void Parse_ReadsASystemLevelRefusal()
    {
        var reply = PleskPacket.Parse("<packet><system><status>error</status><errcode>11003</errcode><errtext>key is not found</errtext></system></packet>");

        reply.IsSuccess.Should().BeFalse();
        reply.ErrorCode.Should().Be(11003);
    }

    /// <summary>Bodies that are not a Plesk reply are unreadable, never a refusal with a code.</summary>
    /// <param name="body">The body.</param>
    [Theory]
    [InlineData("<html><body>Bad Gateway</body></html>")]
    [InlineData("not xml at all")]
    [InlineData("<packet><customer><get><result><id>2</id></result></get></customer></packet>")]
    [InlineData("<packet><customer><get><result><status>error</status></result></get></customer></packet>")]
    public void Parse_IsUnreadable_ForABodyThatIsNotAReply(string body)
    {
        var reply = PleskPacket.Parse(body);

        reply.IsSuccess.Should().BeFalse();
        reply.ErrorCode.Should().BeNull();
    }
}
