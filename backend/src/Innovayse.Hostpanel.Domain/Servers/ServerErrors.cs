namespace Innovayse.Hostpanel.Domain.Servers;

/// <summary>
/// Refusal codes the <see cref="Server"/> aggregate throws as
/// <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string values from
/// these constants so the resource coverage test still requires a sentence for each one.
/// </remarks>
public static class ServerErrors
{
    /// <summary>A server without SSL cannot accept a self signed certificate (400): <see cref="Server.Create"/> and <see cref="Server.Reconfigure"/>.</summary>
    public const string SelfSignedRequiresSsl = "SERVER_SELF_SIGNED_REQUIRES_SSL";
}
