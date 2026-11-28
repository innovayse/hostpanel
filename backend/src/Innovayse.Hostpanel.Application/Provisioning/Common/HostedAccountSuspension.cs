namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>Input of a hosting account suspension.</summary>
/// <param name="Account">The account to suspend.</param>
/// <param name="Reason">
/// Why it is suspended, passed on to the panel; <see langword="null"/> when none was given.
/// </param>
public sealed record HostedAccountSuspension(HostedAccountTarget Account, string? Reason = null);
