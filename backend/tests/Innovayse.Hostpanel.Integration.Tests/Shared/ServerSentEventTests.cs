using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary><see cref="ServerSentEvent"/>: the framing of an event and of the heartbeat comment.</summary>
public sealed class ServerSentEventTests
{
    /// <summary>A plain event is a data line and a blank line.</summary>
    [Fact]
    public void Format_PlainEvent_IsOneDataLine()
    {
        ServerSentEvent.Format(new StreamedEvent("hello")).Should().Be("data: hello\n\n");
    }

    /// <summary>A named event with an id writes the event line, then the id line, then the data.</summary>
    [Fact]
    public void Format_NamedEventWithId_WritesEventIdAndDataInOrder()
    {
        ServerSentEvent.Format(new StreamedEvent("{}", "progress", "42")).Should().Be("event: progress\nid: 42\ndata: {}\n\n");
    }

    /// <summary>Every line break form inside the data starts a new data line, so the data cannot end the event early.</summary>
    /// <param name="data">The event data.</param>
    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    [InlineData("a\rb")]
    public void Format_LineBreaksInData_BecomeSeparateDataLines(string data)
    {
        ServerSentEvent.Format(new StreamedEvent(data)).Should().Be("data: a\ndata: b\n\n");
    }

    /// <summary>A blank line inside the data is kept as an empty data line, not as the end of the event.</summary>
    [Fact]
    public void Format_BlankLineInData_StaysInsideTheEvent()
    {
        ServerSentEvent.Format(new StreamedEvent("a\n\nb")).Should().Be("data: a\ndata: \ndata: b\n\n");
    }

    /// <summary>An event with no text is still an event: one empty data line.</summary>
    [Fact]
    public void Format_EmptyData_IsOneEmptyDataLine()
    {
        ServerSentEvent.Format(new StreamedEvent(string.Empty)).Should().Be("data: \n\n");
    }

    /// <summary>The heartbeat is a comment, which a client ignores.</summary>
    [Fact]
    public void Heartbeat_IsAComment()
    {
        ServerSentEvent.Heartbeat.Should().StartWith(":").And.EndWith("\n\n");
    }

    /// <summary>A carriage return, a line feed or a NUL in the name is refused, so a plugin cannot inject a field or an event.</summary>
    /// <param name="name">The forbidden name.</param>
    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    [InlineData("a\0b")]
    [InlineData("tick\n\ndata: injected")]
    public void Format_ForbiddenCharacterInName_IsRefused(string name)
    {
        var act = () => ServerSentEvent.Format(new StreamedEvent("x", name));

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A carriage return, a line feed or a NUL in the id is refused.</summary>
    /// <param name="id">The forbidden id.</param>
    [Theory]
    [InlineData("1\n2")]
    [InlineData("1\r2")]
    [InlineData("1\0")]
    public void Format_ForbiddenCharacterInId_IsRefused(string id)
    {
        var act = () => ServerSentEvent.Format(new StreamedEvent("x", "tick", id));

        act.Should().Throw<ArgumentException>();
    }
}
