namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;

/// <summary>
/// WHM could not be asked, or its answer was lost, and nothing positive is known about what
/// happened on the server, so the caller may try again later.
/// </summary>
/// <remarks>
/// Thrown instead of a refusal. A refusal is an answer: the account was not made. When the answer
/// is missing and the follow up lookup is missing too, the account may or may not exist, and
/// reporting a refusal would tell the caller it is safe to treat the order as failed for good.
/// </remarks>
public sealed class WhmUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WhmUnavailableException"/> class.</summary>
    /// <param name="message">What could not be established; never WHM's own text or a request detail.</param>
    public WhmUnavailableException(string message)
        : base(message)
    {
    }
}
