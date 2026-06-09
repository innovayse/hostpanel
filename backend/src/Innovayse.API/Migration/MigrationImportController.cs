namespace Innovayse.API.Migration;

using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

/// <summary>
/// Public migration endpoints (no auth required).
/// </summary>
[ApiController]
[Route("api/migrations")]
public sealed class MigrationImportController : ControllerBase
{
    /// <summary>
    /// Returns the migration plugin as a downloadable ZIP archive.
    /// </summary>
    [HttpGet("plugin/download")]
    public IActionResult DownloadPlugin()
    {
        // AppContext.BaseDirectory = …/bin/Debug/net9.0 (5 levels up = solution root)
        var solutionRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        var pluginDir = Path.GetFullPath(
            Path.Combine(solutionRoot, "plugins", "innovayse_migration"));

        if (!Directory.Exists(pluginDir))
            return NotFound(new { error = "Plugin files not found." });

        var ms = new MemoryStream();
        ZipFile.CreateFromDirectory(pluginDir, ms, CompressionLevel.Optimal, includeBaseDirectory: false);
        ms.Position = 0;

        return File(ms, "application/zip", "innovayse-migration.zip");
    }
}
