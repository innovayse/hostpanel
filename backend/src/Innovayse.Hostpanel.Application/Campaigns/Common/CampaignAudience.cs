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
/// query through the bus. A client's last sign-in is not stored in a table of this product: it belongs to the identity store, so
/// the segment on it is answered from the identity provider's accounts, and only where the deployment owns its accounts.
/// </remarks>
public sealed class CampaignAudience
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the clients' addresses and languages are read through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Resolves the identity accounts that know when each client last signed in.</summary>
    private readonly IIdentityProvider _identity;

    /// <summary>Initializes the service.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the clients' addresses and languages are read through.</param>
    /// <param name="identity">Resolves the identity accounts that know when each client last signed in.</param>
    public CampaignAudience(IHostpanelDbContext context, IMessageBus bus, IIdentityProvider identity)
    {
        _context = context;
        _bus = bus;
        _identity = identity;
    }

    /// <summary>Lists the clients the segments reach, each with the one address its mail goes to.</summary>
    /// <param name="segments">The segments, already checked by the domain or the validator.</param>
    /// <param name="now">The moment relative expiry rules are measured from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reachable clients with their address and language, in ascending client id order; empty for no segments.</returns>
    public async Task<IReadOnlyList<ClientBriefDto>> ReachAsync(
        IReadOnlyList<CampaignSegmentDto> segments, DateTimeOffset now, CancellationToken ct)
    {
        var clientIds = await MatchingClientIdsAsync(segments, now, ct);

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

    /// <summary>Finds the ids of the opted-in active clients any of the segments matches.</summary>
    /// <param name="segments">The segments to match.</param>
    /// <param name="now">The moment relative expiry and sign-in rules are measured from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The distinct client ids in ascending order; empty for no segments.</returns>
    private async Task<IReadOnlyList<int>> MatchingClientIdsAsync(
        IReadOnlyList<CampaignSegmentDto> segments, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var reachable = _context.Clients.AsNoTracking().Where(c => c.Status == ClientStatus.Active && c.MarketingOptIn);
        var matched = new HashSet<int>();

        var queries = segments
            .Where(segment => segment.FilterType != CampaignFilterType.NotSignedInForDays)
            .Select(segment => Match(reachable, segment, now).Select(c => c.Id))
            .ToList();
        if (queries.Count > 0)
        {
            // Union removes the duplicates a client matched by several segments would otherwise bring.
            matched.UnionWith(await queries.Aggregate((all, next) => all.Union(next)).ToListAsync(ct));
        }

        foreach (var segment in segments.Where(segment => segment.FilterType == CampaignFilterType.NotSignedInForDays))
        {
            matched.UnionWith(await NotSignedInClientIdsAsync(reachable, segment, now, ct));
        }

        return matched.OrderBy(id => id).ToList();
    }

    /// <summary>
    /// Finds the clients whose last sign-in is the given number of days old or older. The time comes from the identity
    /// provider, in batches, because it is not a column of any table this service reads; a client that never signed in has
    /// been away since its record was created. A client created after the cutoff is dropped in the database first, so only the
    /// clients that have existed for the days are looked up.
    /// </summary>
    /// <param name="reachable">The active clients who opted in.</param>
    /// <param name="segment">The sign-in segment.</param>
    /// <param name="now">The moment the days are counted back from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids of the matching clients; a client whose account the identity provider no longer knows is left out.</returns>
    private async Task<IReadOnlyList<int>> NotSignedInClientIdsAsync(
        IQueryable<Client> reachable, CampaignSegmentDto segment, DateTimeOffset now, CancellationToken ct)
    {
        var days = int.Parse(EmailCampaignSegment.Normalize(segment.FilterType, segment.FilterValue)!, CultureInfo.InvariantCulture);
        var cutoff = now.AddDays(-days);

        // A client created after the cutoff has been away for less than the days asked whatever its last sign-in was, so
        // the database drops it before the identity provider is asked about anyone.
        var candidates = await reachable
            .Where(c => c.CreatedAt <= cutoff)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.UserId, c.CreatedAt })
            .ToListAsync(ct);

        var matching = new List<int>();
        foreach (var chunk in candidates.Chunk(JobBatch.Size))
        {
            var accounts = await _identity.GetAccountsBySubjectsAsync(chunk.Select(c => c.UserId), ct);
            foreach (var candidate in chunk)
            {
                if (accounts.TryGetValue(candidate.UserId, out var account) && (account.LastLoginAt ?? candidate.CreatedAt) <= cutoff)
                {
                    matching.Add(candidate.Id);
                }
            }
        }

        return matching;
    }

    /// <summary>Narrows the reachable clients to those one segment picks, in the database.</summary>
    /// <param name="reachable">The active clients who opted in.</param>
    /// <param name="segment">The segment to apply; not a sign-in segment, which <see cref="NotSignedInClientIdsAsync"/> answers.</param>
    /// <param name="now">The moment relative expiry rules are measured from.</param>
    /// <returns>The clients the segment matches.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The filter type is not one the database answers.</exception>
    private IQueryable<Client> Match(IQueryable<Client> reachable, CampaignSegmentDto segment, DateTimeOffset now)
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

            case CampaignFilterType.ClientGroup:
                var groupId = int.Parse(EmailCampaignSegment.Normalize(segment.FilterType, segment.FilterValue)!, CultureInfo.InvariantCulture);
                return reachable.Where(c => c.ClientGroupId == groupId);

            default:
                throw new ArgumentOutOfRangeException(nameof(segment), segment.FilterType, "Unknown campaign filter type.");
        }
    }
}
