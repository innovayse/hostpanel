namespace Innovayse.Hostpanel.Infrastructure.Integrations.Plesk;

/// <summary>The Plesk XML API error codes the client acts on.</summary>
/// <remarks>
/// Only the "absent" codes are read as an answer. Plesk reuses 1007 ("already exists") for other
/// refusals too, a prohibited domain name among them, so the provider never trusts it and always
/// asks what the account looks like instead.
/// </remarks>
public static class PleskErrorCodes
{
    /// <summary>The object asked about does not exist.</summary>
    public const int ObjectNotFound = 1013;

    /// <summary>The owner a subscription was asked for does not exist.</summary>
    public const int OwnerNotFound = 1015;
}
