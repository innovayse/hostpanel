namespace Innovayse.Application.Resources;

/// <summary>
/// Marker type naming the resource set that holds every sentence this API sends to a person.
/// Injected as <c>IStringLocalizer&lt;ValidationMessages&gt;</c>; it is never instantiated.
/// </summary>
/// <remarks>
/// <para>
/// The sentence a refusal carries is written here, in <c>ValidationMessages.resx</c> and its
/// per-language siblings, and not in the frontend. The portal ships en/ru/hy and used to keep a
/// mapping table of its own (<c>client/utils/portalErrorMessages.ts</c>) that translated a
/// handful of codes and let every other message through in English -- so a Russian or Armenian
/// customer read English for all but five failures. Moving the wording here makes one place
/// answer for all three languages, and the response body still carries the machine-readable
/// <c>code</c> beside the sentence so a page can branch without matching on prose.
/// </para>
/// <para>
/// <b>The type's full name is the resource base name.</b>
/// <c>ResourceManagerStringLocalizerFactory</c> builds it from the assembly's root namespace and
/// the type name, so <c>Innovayse.Application.Resources.ValidationMessages</c> has to keep matching
/// the manifest name MSBuild gives <c>Resources/ValidationMessages.resx</c>. Move or rename either
/// half and the localizer silently starts answering with the key names instead of sentences --
/// there is no exception, which is why <c>ValidationMessagesLocalizationTests</c> asserts a real lookup.
/// </para>
/// <para>
/// <b>A missing key resolves to the key itself.</b> That is deliberate: an untranslated message
/// surfaces as <c>SomeKeyName</c> on the screen rather than quietly as English, so it is visible
/// in a screenshot. A key present in <c>ValidationMessages.resx</c> but absent from the <c>ru</c> or
/// <c>hy</c> file falls back to English through the normal <c>ResourceManager</c> chain, which is
/// how the still-untranslated admin-facing messages behave.
/// </para>
/// </remarks>
public sealed class ValidationMessages
{
    /// <summary>
    /// Prevents the marker from being constructed. It exists to name a resource set to the
    /// generic <c>IStringLocalizer&lt;T&gt;</c>, never as an object.
    /// </summary>
    private ValidationMessages()
    {
    }
}
