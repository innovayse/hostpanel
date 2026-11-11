namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Whether this deployment owns its own people, decided once and shared.
///
/// <para>
/// Every reader of <c>Auth:Mode</c> asks here, so one running process cannot disagree with
/// itself about which mode it is in whatever casing the configured value uses. The comparison
/// itself is an Infrastructure detail this layer never reads directly.
/// </para>
/// </summary>
public interface IAuthModeProvider
{
    /// <summary>True when this deployment owns its own people (ASP.NET Identity); false when an external sign-on provider does.</summary>
    public bool IsLocalMode { get; }
}
