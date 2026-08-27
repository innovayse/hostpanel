namespace Innovayse.Infrastructure.Auth;

using Innovayse.Application.Auth.Interfaces;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Reads <c>Auth:Mode</c> once and answers from that ever after.
///
/// <para>
/// Registered as a singleton: configuration does not change while the process runs, and
/// every caller asking the same question should get the same answer computed the same
/// way, rather than each holding its own copy of the comparison. The comparison itself is
/// case-insensitive — the bug this interface exists to fix was one call site (the old
/// <c>LocalAuthController.IsLocalMode</c>) using a plain, case-sensitive <c>==</c> while
/// two others compared case-insensitively, so a configured value of <c>Local</c> or
/// <c>SSO</c> made one part of the process disagree with the rest about which mode it
/// was in.
/// </para>
/// </summary>
/// <param name="configuration">Application configuration.</param>
public sealed class ConfigurationAuthModeProvider(IConfiguration configuration) : IAuthModeProvider
{
    /// <inheritdoc/>
    public bool IsLocalMode { get; } = AuthMode.IsLocal(configuration["Auth:Mode"]);
}
