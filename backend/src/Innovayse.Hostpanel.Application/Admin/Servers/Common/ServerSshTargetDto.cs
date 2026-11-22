
namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>
/// What an SSH key operation needs to know about one server: its module, whether it is disabled,
/// and its key agent settings when any are stored. Server-side only; never sent to a browser.
/// </summary>
/// <param name="ServerId">The server.</param>
/// <param name="Module">Its control panel module.</param>
/// <param name="IsDisabled">Whether the server is disabled.</param>
/// <param name="Agent">Its SSH key agent settings, or <see langword="null"/> when none are stored.</param>
public sealed record ServerSshTargetDto(int ServerId, string Module, bool IsDisabled, ServerSshAgentEndpoint? Agent);
