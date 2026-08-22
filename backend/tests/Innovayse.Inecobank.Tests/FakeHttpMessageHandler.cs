namespace Innovayse.Inecobank.Tests;

using System.Net;

/// <summary>Scripted HTTP handler that records every request and replays queued JSON responses.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<string> _responses = new();

    /// <summary>Gets the requests the client sent, in order.</summary>
    public List<(string Url, string Body)> Requests { get; } = [];

    /// <summary>Queues the next JSON response body (returned with HTTP 200).</summary>
    /// <param name="json">The response body to replay.</param>
    public void Enqueue(string json) => _responses.Enqueue(json);

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri!.ToString(), body));
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responses.Dequeue(), System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
