namespace Innovayse.Application.Services.Common;

/// <summary>
/// Marks a command or query that names a service id the <b>calling client must own</b>, and whose
/// handler is therefore required to settle ownership through <see cref="IServiceOwnership"/>
/// before it does anything else.
/// </summary>
/// <remarks>
/// <para>
/// This is a marker, not a mechanism: implementing it grants nothing and enforces nothing at
/// runtime. What it buys is that the requirement is written on the message type itself rather
/// than living only in the memory of whoever wrote the handler, so a test can ask the question.
/// Two do — <c>MyServicesRoutesAreOwnershipScopedTests</c> in <c>Innovayse.Integration.Tests</c>
/// checks that every <c>MyServicesController</c> action taking a service id off the route
/// dispatches a message carrying this marker, and <c>CallerScopedServiceMessageTests</c> in
/// <c>Innovayse.Application.Tests</c> checks that every marked message has a handler that takes
/// <see cref="IServiceOwnership"/>. Together they fail on a <b>new</b> unguarded route, which is
/// the failure mode that produced the five this marker was introduced to close.
/// </para>
/// <para>
/// A Wolverine middleware keyed on this marker would enforce it rather than merely describe it,
/// and is the better end state. It is not what this does, because registering one means editing
/// <c>Program.cs</c>'s <c>UseWolverine</c> block — the composition root — and the two tests above
/// catch the same omission at build time without it. The marker is deliberately shaped so that
/// swapping to middleware later is a registration and no change to any message.
/// </para>
/// <para>
/// Only <b>client-facing</b> messages carry this. The shared use cases an admin route also
/// dispatches — <c>GetCPanelSsoUrlQuery</c>, <c>ChangePasswordCommand</c> and their siblings —
/// deliberately do not, because an administrator acting on any client's service is legitimate and
/// a marker on the shared message would refuse them.
/// </para>
/// </remarks>
public interface ICallerScopedServiceMessage
{
    /// <summary>
    /// Primary key of the client service this message acts on, which must belong to the caller.
    /// </summary>
    int ServiceId { get; }
}
