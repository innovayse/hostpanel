namespace Innovayse.Application.Auth.Common;

using Microsoft.Extensions.Localization;

/// <summary>
/// Thrown when a flow tries to create or edit a person in a deployment whose people belong to an
/// SSO.
/// </summary>
/// <remarks>
/// <para>
/// It used to derive from <see cref="InvalidOperationException"/> and build its message in the
/// constructor as English prose, so <c>ExceptionMiddleware</c> answered it out of the
/// unclassified 400 bin as <c>INVALID_OPERATION</c> -- indistinguishable from every other 400,
/// and in English on a Russian or Armenian screen. It is now shaped like
/// <c>MyServiceNotFoundException</c>, <c>MyContactNotFoundException</c> and
/// <c>InvoiceNotFoundException</c>: a <see cref="Code"/> the caller may branch on and a
/// <see cref="MessageKey"/> the middleware resolves from
/// <c>Innovayse.Application/Resources/ValidationMessages*.resx</c>.
/// </para>
/// <para>
/// <b>Six keys, not one key with a parameter.</b> The sentence names which flow was refused, and
/// that detail is worth keeping -- "an account cannot be created here" and "a password cannot be
/// set here" send an operator to different places. But the flow name is a verb phrase, and a
/// verb phrase spliced into a sentence does not survive translation: Russian and Armenian
/// inflect it along with the rest of the clause, so a <c>{0}</c> would leave an English fragment
/// sitting inside a Russian sentence and read as a bug in both languages. Each operation
/// therefore carries its own key, and each key is one complete sentence per language, written by
/// somebody who can see all of it.
/// </para>
/// <para>
/// The <see cref="Code"/> stays single for the opposite reason: what the caller can do about any
/// of the six is identical -- the change belongs in the SSO -- so a code per flow would give the
/// frontend six branches that all render the sentence the API already sent.
/// </para>
/// <para>
/// It derives from <see cref="Exception"/> rather than <see cref="InvalidOperationException"/>,
/// as the other typed refusals do. Nothing catches it by base type: <c>UpdateClientHandler</c>
/// catches it by name around the language write alone, and that is deliberately unchanged.
/// </para>
/// </remarks>
/// <param name="operation">Which flow was refused. Selects the sentence and is kept for the log.</param>
public sealed class UserProvisioningNotAllowedException(UserProvisioningOperation operation)
    : Exception(PublicMessageFor(operation))
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// SCREAMING_SNAKE, the casing every error code on this platform uses. It crosses the wire,
    /// so it must not be reworded.
    /// </summary>
    public const string Code = "USER_PROVISIONING_NOT_ALLOWED";

    /// <summary>Which account-writing flow was refused. Logged server-side; selects the sentence.</summary>
    public UserProvisioningOperation Operation { get; } = operation;

    /// <summary>
    /// Key of this refusal's sentence in
    /// <c>Innovayse.Application/Resources/ValidationMessages.resx</c> and its <c>.ru</c> /
    /// <c>.hy</c> siblings.
    /// </summary>
    /// <remarks>
    /// An instance member rather than a constant, because the key depends on
    /// <see cref="Operation"/>. <c>ExceptionMiddleware</c> reads it off the caught instance the
    /// same way it reads the constant on every other typed refusal.
    /// </remarks>
    public string MessageKey => MessageKeyFor(Operation);

    /// <summary>
    /// The English sentence for one flow. This is what <see cref="Exception.Message"/> carries,
    /// so a log line and a test read prose rather than a key; what the caller is shown is looked
    /// up under <see cref="MessageKey"/>, because the portal ships en/ru/hy.
    /// </summary>
    /// <param name="operation">The flow that was refused.</param>
    /// <returns>The English sentence.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for a member nobody has worded yet.</exception>
    public static string PublicMessageFor(UserProvisioningOperation operation) => operation switch
    {
        UserProvisioningOperation.CreateAccount =>
            "This deployment's accounts belong to the sign-on service, so an account cannot be created here. Add the person in the sign-on service first.",
        UserProvisioningOperation.ChangeName =>
            "This deployment's accounts belong to the sign-on service, so a person's name cannot be changed here. Change it in the sign-on service.",
        UserProvisioningOperation.ChangeEmail =>
            "This deployment's accounts belong to the sign-on service, so a sign-in address cannot be changed here. Change it in the sign-on service.",
        UserProvisioningOperation.DeleteAccount =>
            "This deployment's accounts belong to the sign-on service, so an account cannot be deleted here. Delete it in the sign-on service.",
        UserProvisioningOperation.SetPassword =>
            "This deployment's accounts belong to the sign-on service, so a password cannot be set here. Passwords are managed in the sign-on service.",
        UserProvisioningOperation.IssuePasswordReset =>
            "This deployment's accounts belong to the sign-on service, so a password reset cannot be issued here. Request it from the sign-on service.",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    /// <summary>
    /// Resolves the resource key for one flow.
    /// </summary>
    /// <param name="operation">The flow that was refused.</param>
    /// <returns>The key in <c>ValidationMessages.resx</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for a member nobody has keyed yet.</exception>
    /// <remarks>
    /// Spelled out per member rather than derived from <c>operation.ToString()</c>. A key built
    /// by reflection breaks silently when a member is renamed --
    /// <see cref="IStringLocalizer"/> answers a missing key with the key text, so the screen
    /// would read <c>UserProvisioningNotAllowedChangeName</c> and nothing would fail.
    /// </remarks>
    public static string MessageKeyFor(UserProvisioningOperation operation) => operation switch
    {
        UserProvisioningOperation.CreateAccount => "UserProvisioningNotAllowedCreateAccount",
        UserProvisioningOperation.ChangeName => "UserProvisioningNotAllowedChangeName",
        UserProvisioningOperation.ChangeEmail => "UserProvisioningNotAllowedChangeEmail",
        UserProvisioningOperation.DeleteAccount => "UserProvisioningNotAllowedDeleteAccount",
        UserProvisioningOperation.SetPassword => "UserProvisioningNotAllowedSetPassword",
        UserProvisioningOperation.IssuePasswordReset => "UserProvisioningNotAllowedIssuePasswordReset",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };
}
