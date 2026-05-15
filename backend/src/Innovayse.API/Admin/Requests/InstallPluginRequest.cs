namespace Innovayse.API.Admin.Requests;

/// <summary>
/// Multipart form data request for uploading a plugin ZIP archive.
/// </summary>
public sealed class InstallPluginRequest
{
    /// <summary>Gets or sets the uploaded plugin ZIP file.</summary>
    public required IFormFile File { get; init; }
}
