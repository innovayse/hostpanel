namespace Innovayse.Application.Admin.Plugins.Commands.InstallPlugin;

/// <summary>
/// Installs a plugin from a ZIP archive uploaded by the admin.
/// </summary>
/// <param name="ZipBytes">Raw bytes of the uploaded ZIP file.</param>
public record InstallPluginCommand(byte[] ZipBytes);
