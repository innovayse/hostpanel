using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>One scripted answer a <see cref="StubHttpServer"/> replays.</summary>
/// <param name="Status">The HTTP status code to answer with.</param>
/// <param name="Body">The response body.</param>
/// <param name="ContentType">The response media type.</param>
/// <param name="Headers">Extra response headers to set, or <see langword="null"/> for none.</param>
/// <param name="Delay">How long to hold the request before answering, or <see langword="null"/> to answer at once.</param>
public sealed record StubReply(
    HttpStatusCode Status,
    string Body,
    string ContentType = "application/json",
    IReadOnlyDictionary<string, string>? Headers = null,
    TimeSpan? Delay = null);
