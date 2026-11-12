using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Builds the <see cref="FraudSignalContext"/> the signal tests look at, with a harmless request unless a test says otherwise.</summary>
internal static class FraudContexts
{
    /// <summary>A request that trips no signal: a known address, an ordinary e-mail, no block list, nothing seen before.</summary>
    /// <param name="ip">The caller's IP address.</param>
    /// <param name="email">The e-mail address.</param>
    /// <param name="billingCountry">The billing country.</param>
    /// <param name="ipCountry">The country of the IP address.</param>
    /// <param name="blockList">The block list.</param>
    /// <param name="otherAccounts">Other e-mail addresses seen from the IP address.</param>
    /// <param name="orders">Orders seen from the IP address within the window.</param>
    /// <param name="maxAccounts">The accounts-per-address limit.</param>
    /// <param name="maxOrders">The orders-per-window limit.</param>
    /// <returns>The context.</returns>
    internal static FraudSignalContext Request(
        string? ip = "203.0.113.9",
        string? email = "buyer@example.com",
        string? billingCountry = null,
        string? ipCountry = null,
        IReadOnlyList<BlockListEntry>? blockList = null,
        int otherAccounts = 0,
        int orders = 0,
        int maxAccounts = 3,
        int maxOrders = 3)
    {
        return new FraudSignalContext(ip, email, billingCountry, ipCountry, blockList ?? [], otherAccounts, orders, maxAccounts, maxOrders);
    }

    /// <summary>Makes a block list holding the given entries.</summary>
    /// <param name="entries">The type and value of each entry.</param>
    /// <returns>The block list.</returns>
    internal static IReadOnlyList<BlockListEntry> Listing(params (BlockListType Type, string Value)[] entries)
    {
        return entries.Select(e => BlockListEntry.Create(e.Type, e.Value, null)).ToList();
    }
}
