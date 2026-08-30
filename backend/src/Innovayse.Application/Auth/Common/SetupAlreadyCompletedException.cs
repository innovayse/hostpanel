namespace Innovayse.Application.Auth.Common;

/// <summary>
/// Thrown when first-run setup is attempted on an installation that already has an Admin.
/// </summary>
/// <remarks>
/// Shaped like <see cref="UserProvisioningNotAllowedException"/> and the other typed refusals:
/// a <see cref="Code"/> the caller may branch on and a <see cref="MessageKey"/> that
/// <c>ExceptionMiddleware</c> resolves from
/// <c>Innovayse.Application/Resources/ValidationMessages*.resx</c>. It replaces a
/// <c>Conflict(new { error = "Setup already completed." })</c> written into the controller,
/// which answered every caller in English whatever they asked for.
/// <para>
/// It is answered <b>409</b> rather than 403: the request was well-formed and the caller is
/// authenticated — the state it wanted to move the installation into is one the installation
/// has already left. That distinction matters to the admin SPA, which shows the bootstrap
/// screen only while the role is unclaimed and needs to know it lost a race rather than that
/// its credential was wrong.
/// </para>
/// </remarks>
public sealed class SetupAlreadyCompletedException() : Exception("Setup has already been completed.")
{
    /// <summary>
    /// Machine-readable code sent to the caller as the <c>code</c> field of the error body.
    /// SCREAMING_SNAKE, like every other error code on this platform. It crosses the wire, so
    /// it must not be reworded.
    /// </summary>
    public const string Code = "SETUP_ALREADY_COMPLETED";

    /// <summary>
    /// Key of this refusal's sentence in
    /// <c>Innovayse.Application/Resources/ValidationMessages.resx</c> and its <c>.ru</c> /
    /// <c>.hy</c> siblings.
    /// </summary>
    public const string MessageKey = "SetupAlreadyCompleted";
}
