namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>The key one localised view document is cached under in <see cref="PluginViewDocuments"/>.</summary>
/// <param name="Path">The package-relative path of the document, as the manifest writes it.</param>
/// <param name="Language">
/// The lower-cased two-letter supported language it was localised in, the same value
/// <see cref="PluginUiTexts.Language"/> resolved its labels in.
/// </param>
internal sealed record LocalisedViewKey(string Path, string Language);
