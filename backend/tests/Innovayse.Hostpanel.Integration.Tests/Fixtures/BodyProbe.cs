using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.API.Plugins;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Test-host-only base of the controllers that stand in for a plugin door on a real Kestrel socket: the same route shape
/// as the production doors (<c>{id}/{**path}</c>), the same reading of the request (<see cref="PluginHttpInputReader"/>
/// and <see cref="PluginBodyReader"/>), and the same 413 code, with the body reported instead of handed to a plugin. The
/// derived classes differ only in which of the production request guards they carry. Registered only by
/// <see cref="KestrelBodyHost"/>; they do not exist in the production host.
/// </summary>
public abstract class BodyProbe : ControllerBase
{
    /// <summary>The configured body cap.</summary>
    private readonly IOptions<PluginApiOptions> _options;

    /// <summary>Initializes a new instance of the <see cref="BodyProbe"/> class.</summary>
    /// <param name="options">The configured body cap.</param>
    protected BodyProbe(IOptions<PluginApiOptions> options)
    {
        _options = options;
    }

    /// <summary>Reads the body the way a plugin call does and reports what arrived.</summary>
    /// <param name="id">The plugin id route value; unused, it only gives the route its production shape.</param>
    /// <param name="path">The path below the plugin's prefix.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The body's length, whether every byte is the one the test sender generates and the content type; 413 over the cap.</returns>
    [HttpPost("{id}/{**path}")]
    public async Task<IActionResult> ReadAsync(string id, string? path, CancellationToken ct)
    {
        var body = await PluginBodyReader.ReadAsync(PluginHttpInputReader.Read(Request, path), _options.Value.MaxBodyBytes, ct);
        if (body is not { } bytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { code = HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE });
        }

        return Ok(new
        {
            length = bytes.Length,
            intact = bytes.Span.IndexOfAnyExcept((byte)'x') < 0,
            contentType = Request.ContentType,
        });
    }

    /// <summary>A route the door refuses before reading the body, as it does for an undeclared route or a caller it turns away.</summary>
    /// <param name="path">The path below the refusing prefix.</param>
    /// <returns>404, with the body unread.</returns>
    [HttpPost("refuse/{**path}")]
    public IActionResult Refuse(string? path)
    {
        return NotFound();
    }
}
