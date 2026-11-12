namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>Everything the signals may look at for one request, gathered by the caller so a signal needs no I/O.</summary>
/// <param name="IpAddress">The caller's IP address, or <see langword="null"/> when it is not known.</param>
/// <param name="Email">The e-mail address, or <see langword="null"/> when it is not known.</param>
/// <param name="BillingCountry">The billing country (ISO 3166-1 alpha-2), or <see langword="null"/> when none is on record.</param>
/// <param name="IpCountry">The country of the IP address, or <see langword="null"/> when no scoring service could say.</param>
/// <param name="BlockList">The operator's block list.</param>
/// <param name="OtherAccountsOnIp">How many other e-mail addresses were seen from this IP address recently.</param>
/// <param name="OrdersInWindow">How many orders came from this IP address within the velocity window.</param>
/// <param name="MaxAccountsPerIp">How many accounts one IP address may carry.</param>
/// <param name="VelocityMaxOrders">How many orders one IP address may place within the velocity window.</param>
public sealed record FraudSignalContext(
    string? IpAddress,
    string? Email,
    string? BillingCountry,
    string? IpCountry,
    IReadOnlyList<BlockListEntry> BlockList,
    int OtherAccountsOnIp,
    int OrdersInWindow,
    int MaxAccountsPerIp,
    int VelocityMaxOrders)
{
    /// <summary>Gets the domain part of the e-mail address in lower case, or <see langword="null"/> when there is none.</summary>
    public string? EmailDomain
    {
        get
        {
            var at = Email?.LastIndexOf('@') ?? -1;
            return at < 0 || at == Email!.Length - 1 ? null : Email[(at + 1)..].Trim().ToLowerInvariant();
        }
    }
}
