namespace Innovayse.Hostpanel.Domain.Domains;

/// <summary>
/// Refusal codes the <see cref="RegisteredDomain"/> aggregate throws as
/// <see cref="Innovayse.Hostpanel.Domain.DomainRuleException"/>.
/// </summary>
/// <remarks>
/// Declared here, not in <c>Application.Common.Errors.HostpanelErrorCodes</c>, because Domain may
/// not reference Application; <c>HostpanelErrorCodes</c> re-declares the same string values from
/// these constants so <c>ResourceCoverageTests</c> (which only reflects over
/// <c>HostpanelErrorCodes</c>) still requires a sentence for each one in every language.
/// </remarks>
public static class DomainsErrors
{
    /// <summary>The domain's status does not allow cancellation (400): <see cref="RegisteredDomain.Cancel"/>.</summary>
    public const string DomainCancelNotAllowed = "DOMAIN_CANCEL_NOT_ALLOWED";

    /// <summary>The domain's status does not allow renewal (400): <see cref="RegisteredDomain.Renew"/>.</summary>
    public const string DomainRenewNotAllowed = "DOMAIN_RENEW_NOT_ALLOWED";

    /// <summary>
    /// The domain's status does not allow linking a hosting service (400):
    /// <see cref="RegisteredDomain.LinkService"/>.
    /// </summary>
    public const string DomainLinkNotAllowed = "DOMAIN_LINK_NOT_ALLOWED";

    /// <summary>
    /// The id names no DNS record on this domain (404): <see cref="RegisteredDomain.UpdateDnsRecord"/>
    /// and <see cref="RegisteredDomain.RemoveDnsRecord"/>.
    /// </summary>
    public const string DomainDnsRecordNotFound = "DOMAIN_DNS_RECORD_NOT_FOUND";

    /// <summary>
    /// The id names no email forwarding rule on this domain (404):
    /// <see cref="RegisteredDomain.UpdateForwardingRule"/> and
    /// <see cref="RegisteredDomain.RemoveForwardingRule"/>.
    /// </summary>
    public const string DomainForwardingRuleNotFound = "DOMAIN_FORWARDING_RULE_NOT_FOUND";

    /// <summary>
    /// A value given for a registry's extra domain field is refused (422): <see cref="DomainExtendedAttributes.From"/> when the
    /// values would not fit the stored form, and the check against the fields the registrar plugin declares.
    /// </summary>
    public const string DomainFieldInvalid = "DOMAIN_FIELD_INVALID";
}
