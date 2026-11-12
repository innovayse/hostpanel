using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Models.Interfaces;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The reply the host gives a caller for a plugin route: the plugin's JSON or empty success with the status the
/// plugin chose, the plugin's problem in the standard error body, the events of a streamed reply, or a raw body with its
/// own media type (a callback's acknowledgement, or the HTML page of a gateway's checkout).
/// </summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">The value written as JSON; <see langword="null"/> for no content and for a problem.</param>
/// <param name="ErrorCode">The namespaced code of a problem (see <see cref="PluginCallRunner"/>); <see langword="null"/> for a success.</param>
/// <param name="Detail">The finished sentence of a problem; <see langword="null"/> for a success.</param>
/// <param name="Stream">The events of a streamed reply; <see langword="null"/> for every other reply.</param>
/// <param name="RawBody">The bytes of a raw reply, written as they are; <see langword="null"/> for every other reply.</param>
/// <param name="ContentType">The media type of <paramref name="RawBody"/>; <see langword="null"/> for every other reply.</param>
/// <param name="PageOrigins">
/// The origins the page may load from when the raw reply is an HTML page, so a gateway can serve its checkout page;
/// <see langword="null"/> for a raw reply that is not a page and for every other reply.
/// </param>
public sealed record PluginReplyDto(
    int Status,
    object? Body,
    string? ErrorCode,
    string? Detail,
    StreamedReply? Stream = null,
    ReadOnlyMemory<byte>? RawBody = null,
    string? ContentType = null,
    IReadOnlyList<string>? PageOrigins = null)
    : IForwardedReply;
