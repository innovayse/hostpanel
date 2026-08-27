namespace Innovayse.Infrastructure.Auth;

/// <summary>
/// The single comparison behind every "which mode is this?" question in the codebase.
///
/// <para>
/// Lives in Infrastructure, not Application: interpreting a raw configuration string is a
/// detail of where configuration comes from, and the Application layer's
/// <c>IAuthModeProvider</c> asks only the yes/no question, never this one directly.
/// </para>
/// <para>
/// Exists as a static helper, rather than folding the comparison into
/// <c>ConfigurationAuthModeProvider</c> alone, because one caller — <c>Program.cs</c> —
/// runs during service registration, before the DI container is built, and cannot resolve
/// anything DI-registered. That code and the provider's implementation both call this, so
/// the comparison itself never has two copies to drift apart.
/// </para>
/// </summary>
public static class AuthMode
{
    /// <summary>The configured value that selects local (self-owned) mode.</summary>
    public const string Local = "local";

    /// <summary>
    /// True when <paramref name="configuredValue"/> names local mode, case-insensitively.
    /// </summary>
    /// <param name="configuredValue">The raw value of the <c>Auth:Mode</c> configuration key.</param>
    /// <returns>True for local mode; false for anything else, including a missing value.</returns>
    public static bool IsLocal(string? configuredValue) =>
        string.Equals(configuredValue, Local, StringComparison.OrdinalIgnoreCase);
}
