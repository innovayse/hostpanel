using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Domain.Fraud;

namespace Innovayse.Hostpanel.Application.Fraud.Extensions;

/// <summary>Maps a <see cref="FraudLogEntry"/> to its DTO.</summary>
public static class FraudLogEntryExtensions
{
    /// <summary>Maps a log entry.</summary>
    /// <param name="entry">The entry to map.</param>
    /// <returns>The DTO.</returns>
    public static FraudLogEntryDto ToDto(this FraudLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new FraudLogEntryDto(
            entry.Id,
            entry.Kind,
            entry.Decision,
            entry.ClientId,
            entry.OrderId,
            entry.IpAddress,
            entry.Email,
            entry.Score,
            entry.Reasons,
            entry.CreatedAt);
    }
}
