namespace Innovayse.Hostpanel.Shared.Extensions;

/// <summary>
/// The one answer to "did the client walk away?", asked by the exception middleware, the action
/// timing filter and the logger that demotes expected failures, so the three can never disagree.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Whether the exception is a cancellation that happened because the client aborted the request
    /// (a reload, a navigation, a closed tab). A cancellation for any other reason, such as a
    /// timeout of the server's own or a host that is stopping, is not one, because the request
    /// token is not cancelled then.
    /// </summary>
    /// <param name="context">The current request, or <c>null</c> when the code runs outside one.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <returns><c>true</c> when the exception is a cancellation and the request was aborted.</returns>
    public static bool IsClientAbort(this HttpContext? context, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is OperationCanceledException
            && context is not null
            && context.RequestAborted.IsCancellationRequested;
    }
}
