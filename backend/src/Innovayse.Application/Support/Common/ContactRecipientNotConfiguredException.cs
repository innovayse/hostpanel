namespace Innovayse.Application.Support.Common;

/// <summary>
/// Thrown when the public contact form is submitted on a deployment that never configured the
/// address enquiries are delivered to.
/// <para>
/// This is a misconfiguration, not a fault of the request, and the whole reason this type
/// exists is that the alternative already shipped: the Nuxt route this use case replaces wrapped
/// its send in <c>if (smtpHost &amp;&amp; smtpUser &amp;&amp; emailTo)</c> and answered "sent"
/// either way, so a tier that filled none of them told every visitor their message had arrived
/// while nothing was sent and nothing was logged. A missing setting has to be visible, so it is
/// answered as a refusal carrying <see cref="Code"/> rather than skipped.
/// </para>
/// <para>
/// <c>Notifications:ContactEmail</c> is named in <see cref="SettingName"/> for the server-side
/// log only. The response body carries <see cref="PublicMessage"/>, which tells the visitor the
/// form is unavailable without naming this deployment's configuration keys back at them.
/// </para>
/// </summary>
public sealed class ContactRecipientNotConfiguredException() : Exception(PublicMessage)
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// SCREAMING_SNAKE, the casing every error code on this platform uses. The frontend branches
    /// on this string, so it is part of the wire contract and must not be reworded.
    /// </summary>
    public const string Code = "CONTACT_NOT_CONFIGURED";

    /// <summary>
    /// Key of the sentence in <c>Innovayse.Application/Resources/ValidationMessages.resx</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="PublicMessage"/> is still the English text and is still what
    /// <see cref="System.Exception.Message"/> carries, so a log line and a test read the same
    /// sentence they always did. What the caller is shown is looked up under this key instead,
    /// because the portal ships in en/ru/hy and a customer reading Russian or Armenian was
    /// previously served this English constant for every failure the frontend had no entry for.
    /// </remarks>
    public const string MessageKey = "ContactNotConfigured";

    /// <summary>
    /// The configuration key an operator has to fill to fix this. Written to the log line that
    /// accompanies the refusal, never to the response body.
    /// </summary>
    public const string SettingName = "Notifications:ContactEmail";

    /// <summary>
    /// The sentence the visitor is shown. It offers the one recovery available -- reach the
    /// company another way -- because retrying the form cannot succeed until an operator acts.
    /// </summary>
    public const string PublicMessage =
        "The contact form is not available on this deployment. Please use the email address or phone number shown on this page.";
}
