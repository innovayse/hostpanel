using Innovayse.Hostpanel.Infrastructure.Storage.Settings;
using Microsoft.AspNetCore.Hosting;

namespace Innovayse.Hostpanel.Infrastructure.Storage.Extensions;

/// <summary>Extension methods that resolve where <see cref="BrandingOptions"/> puts uploads on disk.</summary>
public static class BrandingOptionsExtensions
{
    /// <summary>The absolute directory every branding upload is written under.</summary>
    /// <param name="options">The branding options; <see cref="BrandingOptions.RelativePath"/> is resolved against the web root.</param>
    /// <param name="env">Host environment, for locating the web root.</param>
    /// <returns>The uploads root, resolved against the web root.</returns>
    public static string UploadsRoot(this BrandingOptions options, IWebHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(env);

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        return Path.GetFullPath(Path.Combine(webRoot, options.RelativePath));
    }
}
