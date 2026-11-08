using System.Text.Json;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm.Models;

/// <summary>The outcome of one WHM API 1 call.</summary>
/// <param name="IsSuccess">Whether WHM answered with <c>metadata.result</c> 1.</param>
/// <param name="IsRefusal">
/// Whether WHM answered and positively refused, with <c>metadata.result</c> 0. False on success
/// and when the reply could not be read at all (transport failure, error page, unparseable body).
/// </param>
/// <param name="Data">The reply's <c>data</c> element on success, cloned so it outlives the document; otherwise null.</param>
public sealed record WhmReply(bool IsSuccess, bool IsRefusal, JsonElement? Data)
{
    /// <summary>The reply for an answer that could not be read.</summary>
    public static WhmReply Unreadable { get; } = new(false, false, null);

    /// <summary>The reply for a positive refusal.</summary>
    public static WhmReply Refused { get; } = new(false, true, null);
}
