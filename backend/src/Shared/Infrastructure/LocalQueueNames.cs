namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// Names the local queue the conventional routing gives a message type. The routing is configured with this
/// namer and <see cref="DurableQueuePolicy"/> matches queues with it, so the two always agree on which queue
/// carries which message without reading Wolverine's internal assignment table.
/// </summary>
public static class LocalQueueNames
{
    /// <summary>The queue name for <paramref name="messageType"/>: its full name in lower case, nested types joined with a dot.</summary>
    /// <param name="messageType">The handled message type.</param>
    /// <returns>A name unique per message type and valid in a <c>local://</c> address.</returns>
    public static string For(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return (messageType.FullName ?? messageType.Name).Replace('+', '.').ToLowerInvariant();
    }
}
