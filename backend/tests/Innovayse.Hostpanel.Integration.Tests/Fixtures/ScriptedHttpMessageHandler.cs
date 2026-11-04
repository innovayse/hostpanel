using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An HTTP handler that answers each request from a script the test supplies and records every request it saw. It
/// fakes the remote server only, never our code.
/// </summary>
/// <param name="script">Builds the response for a request; may throw to simulate a network failure.</param>
public sealed class ScriptedHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> script) : HttpMessageHandler
{
    /// <summary>Every request address seen, in order.</summary>
    public List<Uri> Requests { get; } = [];

    /// <summary>The body text of every request seen, in order; empty for a request without a body.</summary>
    public List<string> Bodies { get; } = [];

    /// <summary>Creates a handler that answers every request with a fixed status and JSON text.</summary>
    /// <param name="json">The JSON body.</param>
    /// <param name="status">The status code.</param>
    /// <returns>The handler.</returns>
    public static ScriptedHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new ScriptedHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        }));
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return await script(request, cancellationToken);
    }
}
