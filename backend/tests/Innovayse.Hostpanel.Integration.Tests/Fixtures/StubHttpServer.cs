using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A real Kestrel server on a random loopback port that replays scripted responses and records
/// every request. Use it for an external service we do not own, so an adapter test exercises the
/// real <see cref="HttpClient"/> pipeline instead of a mocked <see cref="HttpMessageHandler"/>.
/// </summary>
/// <remarks>
/// Replies are consumed in order; once the queue is empty the last reply keeps being served, so a
/// test that makes more calls than it scripted fails on content, not on a queue underflow.
/// </remarks>
public sealed class StubHttpServer : IAsyncDisposable
{
    /// <summary>The running web application.</summary>
    private readonly WebApplication app;

    /// <summary>The replies still to serve, in order.</summary>
    private readonly ConcurrentQueue<StubReply> replies = new();

    /// <summary>Every request received, in arrival order.</summary>
    private readonly ConcurrentQueue<RecordedCall> requests = new();

    /// <summary>The reply served when the queue is empty: the last one dequeued.</summary>
    private StubReply lastReply = new(HttpStatusCode.NotFound, string.Empty);

    /// <summary>Initializes a new instance of the <see cref="StubHttpServer"/> class.</summary>
    /// <param name="app">The started application.</param>
    /// <param name="baseAddress">The address the server listens on.</param>
    private StubHttpServer(WebApplication app, Uri baseAddress)
    {
        this.app = app;
        BaseAddress = baseAddress;
    }

    /// <summary>Gets the address the server listens on, with a trailing slash.</summary>
    public Uri BaseAddress { get; }

    /// <summary>Gets the requests received so far, in arrival order.</summary>
    public IReadOnlyList<RecordedCall> Requests => [.. requests];

    /// <summary>Starts a server on a random free loopback port.</summary>
    /// <returns>The running server.</returns>
    public static async Task<StubHttpServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var app = builder.Build();
        StubHttpServer? server = null;
        app.Run(context => server!.ServeAsync(context));
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        server = new StubHttpServer(app, new Uri(address.TrimEnd('/') + "/"));
        return server;
    }

    /// <summary>Queues a JSON reply, served with HTTP 200.</summary>
    /// <param name="json">The response body.</param>
    public void Enqueue(string json)
    {
        replies.Enqueue(new StubReply(HttpStatusCode.OK, json));
    }

    /// <summary>Queues a reply with an explicit status.</summary>
    /// <param name="status">The HTTP status code.</param>
    /// <param name="body">The response body.</param>
    /// <param name="contentType">The response media type.</param>
    public void Enqueue(HttpStatusCode status, string body, string contentType = "application/json")
    {
        replies.Enqueue(new StubReply(status, body, contentType));
    }

    /// <summary>Queues a fully specified reply.</summary>
    /// <param name="reply">The reply to serve.</param>
    public void Enqueue(StubReply reply)
    {
        replies.Enqueue(reply);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    /// <summary>Records the request and answers with the next scripted reply.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the reply is written.</returns>
    private async Task ServeAsync(Microsoft.AspNetCore.Http.HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        requests.Enqueue(new RecordedCall(
            context.Request.Method,
            context.Request.Path + context.Request.QueryString.Value,
            body,
            context.Request.Headers.Authorization.FirstOrDefault(),
            context.Request.Headers["Idempotency-Key"].FirstOrDefault()));

        if (replies.TryDequeue(out var next))
        {
            lastReply = next;
        }

        if (lastReply.Delay is { } delay)
        {
            try
            {
                await Task.Delay(delay, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // The caller gave up (cancelled or timed out), which is what a held request is for.
                return;
            }
        }

        context.Response.StatusCode = (int)lastReply.Status;
        context.Response.ContentType = lastReply.ContentType;
        foreach (var (name, value) in lastReply.Headers ?? new Dictionary<string, string>())
        {
            context.Response.Headers[name] = value;
        }

        await context.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(lastReply.Body));
    }
}
