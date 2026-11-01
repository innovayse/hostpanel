using Innovayse.SDK.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// What the controller read from the HTTP request of a plugin route, without any web framework type: the use case
/// matches it against the manifest and turns it into a plugin call.
/// </summary>
/// <param name="Method">The HTTP method of the request.</param>
/// <param name="Path">The path below the plugin's own prefix, without a leading slash, for example <c>invoices/12</c>.</param>
/// <param name="Query">The query string values by name; a repeated name keeps every value in order.</param>
/// <param name="Body">The request body stream, read by the use case only after the caller is authorised and never past the configured cap.</param>
/// <param name="ContentType">The <c>Content-Type</c> header, or <see langword="null"/> when there is none.</param>
/// <param name="ContentLength">The declared <c>Content-Length</c>, or <see langword="null"/> when the body is chunked or absent.</param>
public sealed record PluginHttpInput(
    PluginRouteMethod Method,
    string Path,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Query,
    Stream Body,
    string? ContentType,
    long? ContentLength);
