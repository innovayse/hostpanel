using System.Net;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud.Signals;

/// <summary>Fires when the caller's IP address is on the block list, alone or inside a blocked CIDR range.</summary>
public sealed class BlockedIpSignal : IFraudSignal
{
    /// <summary>Points a block list hit adds; enough to reach any reject threshold.</summary>
    public const int Weight = FraudAssessment.MaxScore;

    /// <inheritdoc/>
    public FraudSignalHit? Evaluate(FraudSignalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IPAddress.TryParse(context.IpAddress, out var address))
        {
            return null;
        }

        var listed = context.BlockList.Any(entry => entry.Type switch
        {
            BlockListType.Ip => IPAddress.TryParse(entry.Value, out var blocked) && IpNetwork.Unmap(blocked).Equals(IpNetwork.Unmap(address)),
            BlockListType.Cidr => IpNetwork.TryParse(entry.Value, out var range) && range.Contains(address),
            _ => false,
        });

        return listed ? new FraudSignalHit(FraudReasonCode.BlockedIp, Weight) : null;
    }
}
