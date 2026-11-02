using Microsoft.AspNetCore.Http.Features;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The web server's per request body limit, as a feature a test can read back. A real server exposes it; an in-memory
/// test server does not, so a test that is about the limit supplies it.
/// </summary>
/// <param name="maxRequestBodySize">The limit the server starts with, in bytes.</param>
/// <param name="isReadOnly">Whether the server has already started reading the body, after which the limit cannot change.</param>
public sealed class FakeMaxRequestBodySizeFeature(long? maxRequestBodySize, bool isReadOnly = false) : IHttpMaxRequestBodySizeFeature
{
    /// <inheritdoc/>
    public bool IsReadOnly { get; } = isReadOnly;

    /// <inheritdoc/>
    public long? MaxRequestBodySize { get; set; } = maxRequestBodySize;
}
