using System.Globalization;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// Resolves who a set of campaign segments reaches. This is the one place that turns a segment into recipients, so the count an
/// admin sees before sending and the recipients fixed when sending starts can never disagree.
/// </summary>
/// <remarks>
/// A client is in the audience only when it is active and has opted in to marketing email, whatever the segments say; a
/// client matched by several segments counts once. A client with no sign-in address cannot be mailed, and one address is
/// mailed once however many clients share it (compared without regard to case or surrounding spaces): the client with the
/// lowest id keeps it. The service reads clients and their services, tables of two other modules, as a read-only projection,
/// which is why it is listed in the architecture test's cross-module allow-list; addresses come from the Clients module's own
/// query through the bus.
/// </remarks>
public sealed class CampaignAudience
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the clients' addresses and languages are read through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the service.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the clients' addresses and languages are read through.</param>
    public CampaignAudience(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Lists the clients the segments reach, each with the one address its mail goes to.</summary>
    /// <param name="segments">The segments, already checked by the domain or the validator.</param>
    /// <param name="now">The moment relative expiry rules are measured from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reachable clients with their address and language, in ascending client id order; empty for no segments.</returns>
    public async Task<IReadOnlyList<ClientBriefDto>> ReachAsync(
        IReadOnlyList<CampaignSegmentDefinition> segments, DateTimeOffset now, CancellationToken ct)
    {
        var clientIds = await MatchingClientIds(segments, now).OrderBy(id => id).ToListAsync(ct);

        var reached = new List<ClientBriefDto>();
        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in clientIds.Chunk(JobBatch.Size))
        {
            var briefs = await _bus.GetClientBriefsByIdAsync(chunk, withEmail: true, ct);

            // The identity store answers in no particular order, so the chunk is put back in id order for the lowest id to win.
            foreach (var brief in briefs.Values.OrderBy(b => b.Id))
            {
                // A client whose identity has no address cannot be mailed, so it is not reached.
                if (!string.IsNullOrWhiteSpace(brief.Email) && addresses.Add(brief.Email.Trim()))
                {
                    reached.Add(brief);
                }
            }
        }

        return reached;
    }

    /// <summary>Builds the query of client ids: the union of what each segment matches among the opted-in active clients.</summary>
    /// <param name="segments">The segments to match.</param>
    /// <param name="now">The moment relative expiry rules are measured from.</param>
    /// <returns>A query of distinct client ids.</returns>
    private IQueryable<int> MatchingClientIds(IReadOnlyList<CampaignSegmentDefinition> segments, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var reachable = _context.Clients.AsNoTracking().Where(c => c.Status == ClientStatus.Active && c.MarketingOptIn);
        var matches = segments.Select(segment => Match(reachable, segment, now).Select(c => c.Id)).ToList();

        // Union removes the duplicates a client matched by several segments would otherwise bring.
        return matches.Count == 0
            ? reachable.Where(_ => false).Select(c => c.Id)
            : matches.Aggregate((all, next) => all.Union(next));
    }

    /// <summary>Narrows the reachable clients to those one segment picks.</summary>
    /// <param name="reachable">The active clients who opted in.</param>
    /// <param name="segment">The segment to apply.</param>
    /// <param name="now">The moment relative expiry rules are measured from.</param>
    /// <returns>The clients the segment matches.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The filter type is not a known one.</exception>
    private IQueryable<Client> Match(IQueryable<Client> reachable, CampaignSegmentDefinition segment, DateTimeOffset now)
    {
        var services = _context.ClientServices.AsNoTracking().Where(s => s.Status == ServiceStatus.Active);

        switch (segment.FilterType)
        {
            case CampaignFilterType.AllActive:
                return reachable;

            case CampaignFilterType.Product:
                var productId = int.Parse(EmailCampaignSegment.Normalize(segment.FilterType, segment.FilterValue)!, CultureInfo.InvariantCulture);
                return reachable.Where(c => services.Any(s => s.ClientId == c.Id && s.ProductId == productId));

            case CampaignFilterType.Country:
                var country = EmailCampaignSegment.Normalize(segment.FilterType, segment.FilterValue)!;
                return reachable.Where(c => c.Country != null && c.Country.ToUpper() == country);

            case CampaignFilterType.ServiceExpiringInDays:
                var horizon = now.AddDays(int.Parse(EmailCampaignSegment.Normalize(segment.FilterType, segment.FilterValue)!, CultureInfo.InvariantCulture));
                return reachable.Where(c => services.Any(s => s.ClientId == c.Id && s.NextRenewalAt > now && s.NextRenewalAt <= horizon));

            default:
                throw new ArgumentOutOfRangeException(nameof(segment), segment.FilterType, "Unknown campaign filter type.");
        }
    }
}
