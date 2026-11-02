using System.Xml.Linq;

namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk.Models;

/// <summary>The outcome of one XML API operation.</summary>
/// <param name="IsSuccess">Whether Plesk answered the operation with status <c>ok</c>.</param>
/// <param name="ErrorCode">
/// Plesk's error code when it refused the operation; null on success and when the reply could not
/// be read at all (transport failure, error page, unparseable body).
/// </param>
/// <param name="Result">The operation's <c>result</c> element on success; otherwise null.</param>
public sealed record PleskReply(bool IsSuccess, int? ErrorCode, XElement? Result)
{
    /// <summary>The reply for an answer that could not be read.</summary>
    public static PleskReply Unreadable { get; } = new(false, null, null);
}
