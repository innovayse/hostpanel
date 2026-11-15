namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IHttpClientFactory"/> whose every client talks to one <see cref="StubHttpServer"/>,
/// for adapters that take a factory and choose their own host and port.
/// </summary>
public sealed class StubHttpClientFactory : IHttpClientFactory
{
    /// <summary>The stub server's base address, with a trailing slash.</summary>
    private readonly Uri _target;

    /// <summary>The handlers handed out so far, which hold the addresses their requests named.</summary>
    private readonly List<RedirectToStubHandler> _handlers = [];

    /// <summary>Initializes a new instance of the <see cref="StubHttpClientFactory"/> class.</summary>
    /// <param name="server">The stub server every client reaches.</param>
    public StubHttpClientFactory(StubHttpServer server)
    {
        _target = server.BaseAddress;
    }

    /// <summary>The names the clients were asked for, in order; the empty string is the unnamed client.</summary>
    private readonly List<string> _clientNames = [];

    /// <summary>Gets the names the clients were asked for, in order; the empty string is the unnamed client.</summary>
    public IReadOnlyList<string> ClientNames => _clientNames;

    /// <summary>Gets the addresses requests were originally aimed at, across every client, in order.</summary>
    public IReadOnlyList<Uri> RequestedUris => [.. _handlers.SelectMany(handler => handler.RequestedUris)];

    /// <inheritdoc/>
    public HttpClient CreateClient(string name)
    {
        _clientNames.Add(name);
        var handler = new RedirectToStubHandler(_target);
        _handlers.Add(handler);
        return new HttpClient(handler);
    }
}
