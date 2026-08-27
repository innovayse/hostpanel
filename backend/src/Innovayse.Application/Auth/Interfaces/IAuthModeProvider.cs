namespace Innovayse.Application.Auth.Interfaces;

/// <summary>
/// Whether this deployment owns its own people, decided once and shared.
///
/// <para>
/// Five places used to read <c>Auth:Mode</c> independently, and they were not all written
/// the same way: two compared case-insensitively, one used a plain, case-sensitive
/// <c>==</c>. A configured value of <c>Local</c> or <c>SSO</c> — any casing other than the
/// exact lowercase every comparison happened to expect — made parts of one running process
/// disagree about which mode they were in. This interface is the one place left to ask; the
/// comparison itself is an Infrastructure detail this layer never reads directly.
/// </para>
/// </summary>
public interface IAuthModeProvider
{
    /// <summary>True when this deployment owns its own people (ASP.NET Identity); false when an SSO does.</summary>
    bool IsLocalMode { get; }
}
