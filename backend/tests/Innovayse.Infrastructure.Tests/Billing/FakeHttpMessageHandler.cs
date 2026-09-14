namespace Innovayse.Infrastructure.Tests.Billing;

using System.Net;
using System.Text;

/// <summary>Scripted HTTP handler that records every request and replays queued responses.</summary>
/// <remarks>
/// The same idea as the Inecobank suite's handler: the test queues bodies, the client under test
/// sees them in order, and the test reads back what was asked. Nothing here touches the network.
/// </remarks>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    /// <summary>The responses still to replay, in order.</summary>
    private readonly Queue<(HttpStatusCode Status, string Body)> responses = new();

    /// <summary>Gets the URLs the client requested, in order.</summary>
    public List<string> Requests { get; } = [];

    /// <summary>Queues the next JSON response body, returned with HTTP 200.</summary>
    /// <param name="json">The response body to replay.</param>
    public void Enqueue(string json) => responses.Enqueue((HttpStatusCode.OK, json));

    /// <summary>Queues the next response with an explicit, possibly non-success, status code.</summary>
    /// <param name="status">The HTTP status code to return.</param>
    /// <param name="body">The response body to replay.</param>
    public void EnqueueStatus(HttpStatusCode status, string body) => responses.Enqueue((status, body));

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.ToString());
        var (status, body) = responses.Dequeue();
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}
