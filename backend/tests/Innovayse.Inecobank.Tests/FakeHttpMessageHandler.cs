namespace Innovayse.Inecobank.Tests;

using System.Net;

/// <summary>Scripted HTTP handler that records every request and replays queued responses.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();

    /// <summary>Gets the requests the client sent, in order.</summary>
    public List<(string Url, string Body)> Requests { get; } = [];

    /// <summary>Queues the next JSON response body, returned with HTTP 200.</summary>
    /// <param name="json">The response body to replay.</param>
    public void Enqueue(string json) => _responses.Enqueue((HttpStatusCode.OK, json));

    /// <summary>Queues the next response with an explicit, possibly non-success, status code.</summary>
    /// <param name="status">The HTTP status code to return.</param>
    /// <param name="body">The response body to replay.</param>
    public void EnqueueStatus(HttpStatusCode status, string body) => _responses.Enqueue((status, body));

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri!.ToString(), body));
        var (status, responseBody) = _responses.Dequeue();
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
