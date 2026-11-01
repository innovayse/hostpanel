using Innovayse.Hostpanel.Shared.Models.Interfaces;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// The reply the host gives a caller for a plugin route: the plugin's JSON or empty success with the status the
/// plugin chose, or the plugin's problem in the standard error body.
/// </summary>
/// <param name="Status">The HTTP status.</param>
/// <param name="Body">The value written as JSON; <see langword="null"/> for no content and for a problem.</param>
/// <param name="ErrorCode">The namespaced code of a problem (see <see cref="PluginCallRunner"/>); <see langword="null"/> for a success.</param>
/// <param name="Detail">The finished sentence of a problem; <see langword="null"/> for a success.</param>
public sealed record PluginReplyDto(int Status, object? Body, string? ErrorCode, string? Detail) : IForwardedReply;
