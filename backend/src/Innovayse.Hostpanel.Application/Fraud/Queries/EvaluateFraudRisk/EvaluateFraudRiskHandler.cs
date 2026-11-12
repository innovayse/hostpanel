using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Signals;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Fraud.Queries.EvaluateFraudRisk;

/// <summary>
/// Gathers the facts of a registration or order (the block list, what the fraud log remembers of the caller's IP address, and what
/// the external scoring service says when it is set up), lets the domain score them and decide, and answers with the verdict. It
/// writes nothing: the caller logs the decision once it knows the order it belongs to.
/// </summary>
public sealed class EvaluateFraudRiskHandler
{
    /// <summary>How far back other e-mail addresses seen from the same IP address count as accounts on it.</summary>
    private static readonly TimeSpan AccountLookback = TimeSpan.FromDays(30);

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The operator's thresholds and limits.</summary>
    private readonly FraudSettingsReader _settings;

    /// <summary>The external scoring service, which adds to the local signals when it is set up.</summary>
    private readonly IFraudScoringProvider _provider;

    /// <summary>The caller's IP address; the query does not state it, and must not.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>The clock the lookback and the velocity window are measured from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="settings">The operator's thresholds and limits.</param>
    /// <param name="provider">The external scoring service, which adds to the local signals when it is set up.</param>
    /// <param name="caller">The caller's IP address; the query does not state it, and must not.</param>
    /// <param name="clock">The clock the lookback and the velocity window are measured from.</param>
    public EvaluateFraudRiskHandler(
        IHostpanelDbContext context,
        FraudSettingsReader settings,
        IFraudScoringProvider provider,
        ICurrentUserService caller,
        IClock clock)
    {
        _context = context;
        _settings = settings;
        _provider = provider;
        _caller = caller;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">What is being checked.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the verdict. Never refuses.</returns>
    public async Task<Result<FraudVerdictDto>> HandleAsync(EvaluateFraudRiskQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = await _settings.ReadAsync(ct);
        if (!settings.Enabled)
        {
            return Result.Success(new FraudVerdictDto(false, FraudDecision.Approved, 0, []));
        }

        var ip = string.IsNullOrWhiteSpace(_caller.IpAddress) ? null : _caller.IpAddress.Trim();
        var email = string.IsNullOrWhiteSpace(query.Email) ? null : query.Email.Trim().ToLowerInvariant();
        var country = string.IsNullOrWhiteSpace(query.BillingCountry) ? null : query.BillingCountry.Trim().ToUpperInvariant();

        var blockList = await _context.BlockListEntries.AsNoTracking().ToListAsync(ct);
        var (otherAccounts, ordersInWindow) = ip is null ? (0, 0) : await CountRecentAsync(ip, email, settings, ct);

        // The external service is asked only when there is an address to ask about; its failure is its adapter's to log, and
        // the local score decides alone.
        var external = ip is null ? null : await _provider.ScoreAsync(new FraudProviderContext(ip, email, country), ct);

        var local = FraudRiskScorer.Assess(new FraudSignalContext(
            ip, email, country, external?.IpCountry, blockList, otherAccounts, ordersInWindow, settings.MaxAccountsPerIp, settings.VelocityMaxOrders));
        var assessment = external is null ? local : local.CombineWith(new FraudAssessment(external.Score, external.Reasons));

        var decision = FraudDecisionPolicy.Decide(assessment.Score, settings.ManualReviewRiskScore, settings.AutoRejectRiskScore);
        return Result.Success(new FraudVerdictDto(true, decision, assessment.Score, assessment.Reasons));
    }

    /// <summary>Counts what the fraud log remembers of one IP address.</summary>
    /// <param name="ip">The caller's IP address.</param>
    /// <param name="email">The e-mail address being checked, so it is not counted as another account.</param>
    /// <param name="settings">The velocity window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The other e-mail addresses seen from the address within the lookback, and the orders it placed within the velocity window.</returns>
    private async Task<(int OtherAccounts, int OrdersInWindow)> CountRecentAsync(
        string ip, string? email, FraudSettingsDto settings, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var lookbackStart = now - AccountLookback;
        var windowStart = now - TimeSpan.FromMinutes(settings.VelocityWindowMinutes);

        var fromIp = _context.FraudLogEntries.AsNoTracking().Where(l => l.IpAddress == ip);

        var otherAccounts = await fromIp
            .Where(l => l.Email != null && l.Email != email && l.CreatedAt >= lookbackStart)
            .Select(l => l.Email)
            .Distinct()
            .CountAsync(ct);

        // Only the automatic decisions count as orders: a staff decision on a held order is not another order from the address.
        var ordersInWindow = await fromIp
            .Where(l => l.Kind == FraudCheckKind.Order
                && (l.Decision == FraudDecision.Approved || l.Decision == FraudDecision.Review || l.Decision == FraudDecision.Rejected)
                && l.CreatedAt >= windowStart)
            .CountAsync(ct);

        return (otherAccounts, ordersInWindow);
    }
}
