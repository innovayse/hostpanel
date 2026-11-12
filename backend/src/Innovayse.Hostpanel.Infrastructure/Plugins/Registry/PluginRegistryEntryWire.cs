namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// The registry's JSON for one plugin in a listing, or the summary of a plugin page. Every member is checked before it
/// becomes an application type. The registry names the id <c>pluginId</c> and the author <c>publisherName</c>; the
/// shorter <c>id</c> and <c>author</c> are read too, for a registry that uses them.
/// </summary>
/// <param name="PluginId">The plugin id.</param>
/// <param name="Id">The plugin id, in the short form.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">A short description.</param>
/// <param name="PublisherName">The publisher's name.</param>
/// <param name="Author">The publisher's name, in the short form.</param>
/// <param name="IconUrl">The icon address.</param>
/// <param name="Category">The category name.</param>
/// <param name="LatestVersion">The newest version; <see langword="null"/> when nothing is published.</param>
/// <param name="SdkVersion">The SDK version the newest version targets, when the registry states it.</param>
/// <param name="Verified">Whether the registry vouches for the publisher.</param>
/// <param name="Pricing">The pricing name.</param>
/// <param name="Distribution">The distribution name (<c>Download</c> or <c>HostOnly</c>); absent from a registry that has no restricted plugins.</param>
/// <param name="PublisherKind">The publisher kind name (<c>Personal</c> or <c>Organization</c>); absent from an older registry.</param>
/// <param name="Tags">The plugin's tags; absent from an older registry.</param>
/// <param name="Downloads">Downloads of every version added up; absent from an older registry.</param>
/// <param name="LatestChangelog">The release notes of the newest version, Markdown; absent from an older registry.</param>
/// <param name="Rating">The reviews summary; <see langword="null"/> when the plugin has no reviews, and absent from an older registry.</param>
internal sealed record PluginRegistryEntryWire(
    string? PluginId,
    string? Id,
    string? Name,
    string? Description,
    string? PublisherName,
    string? Author,
    string? IconUrl,
    string? Category,
    string? LatestVersion,
    string? SdkVersion,
    bool Verified,
    string? Pricing,
    string? Distribution = null,
    string? PublisherKind = null,
    List<string?>? Tags = null,
    long? Downloads = null,
    string? LatestChangelog = null,
    PluginRegistryRatingWire? Rating = null);
