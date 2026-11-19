namespace Innovayse.Hostpanel.Application.Resources;

/// <summary>
/// Marker type naming the resource set that holds every sentence this API sends to a person.
/// Injected as <c>IStringLocalizer&lt;ApplicationValidationMessages&gt;</c>; it is never instantiated.
/// </summary>
/// <remarks>
/// <para>
/// The sentence a refusal carries is written here, in <c>ApplicationValidationMessages.resx</c> and its
/// per-language siblings, and not in the frontend, so one place answers for all three languages
/// (en/ru/hy/de/es). The response body still carries the machine-readable
/// <c>code</c> beside the sentence so a page can branch without matching on prose.
/// </para>
/// <para>
/// <b>The type's full name is the resource base name.</b>
/// <c>ResourceManagerStringLocalizerFactory</c> builds it from the assembly's root namespace and
/// the type name, so <c>Innovayse.Hostpanel.Application.Resources.ApplicationValidationMessages</c> has to keep matching
/// the manifest name MSBuild gives <c>Resources/ApplicationValidationMessages.resx</c>. Move or rename either
/// half and the localizer silently starts answering with the key names instead of sentences:
/// there is no exception, which is why <c>ValidationMessagesLocalizationTests</c> asserts a real lookup.
/// </para>
/// <para>
/// <b>A missing key resolves to the key itself.</b> That is deliberate: an untranslated message
/// surfaces as <c>SomeKeyName</c> on the screen rather than quietly as English, so it is visible
/// in a screenshot. A key present in <c>ApplicationValidationMessages.resx</c> but absent from the <c>ru</c> or
/// <c>hy</c> file falls back to English through the normal <c>ResourceManager</c> chain, which is
/// how the still-untranslated admin-facing messages behave.
/// </para>
/// </remarks>
public sealed class ApplicationValidationMessages
{
    /// <summary>
    /// Prevents the marker from being constructed. It exists to name a resource set to the
    /// generic <c>IStringLocalizer&lt;T&gt;</c>, never as an object.
    /// </summary>
    private ApplicationValidationMessages()
    {
    }
}
