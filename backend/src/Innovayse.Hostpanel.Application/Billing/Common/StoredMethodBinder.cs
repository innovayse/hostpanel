using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// The one place a stored method is bound to the consent the client gave for it. Both ways a method is stored, a save
/// session the payer completed and a payment that saved the method it was made with, end here, so one method is
/// covered by one consent whichever way it arrived.
/// </summary>
public sealed class StoredMethodBinder
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The clock the revocation of a superseded consent is stamped from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="StoredMethodBinder"/> class.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The clock the revocation of a superseded consent is stamped from.</param>
    public StoredMethodBinder(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>
    /// Binds the method to the consent and revokes the client's earlier active consents for the same method at the same
    /// module, so the method is covered by the consent that was just completed alone. The newest evidence stays; an
    /// older consent's default flag moves over. Binding the method a consent already covers changes nothing.
    /// </summary>
    /// <remarks>Stages the changes only: the caller saves, in the save that belongs to its own use case.</remarks>
    /// <param name="consent">The active consent the method was stored under (tracked).</param>
    /// <param name="methodRef">The stored method the plugin reported.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the consent now covers the method; <see langword="false"/> when it already covers a
    /// different one, in which case nothing is changed.
    /// </returns>
    public async Task<bool> TryBindAsync(CardChargeConsent consent, string methodRef, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consent);

        if (!consent.CanAttach(methodRef))
        {
            return false;
        }

        consent.AttachMethod(methodRef);
        await RevokeOlderConsentsOfTheMethodAsync(consent, ct);
        return true;
    }

    /// <summary>
    /// Revokes the client's earlier active consents for the same method at the same module; the default flag of an older
    /// one moves to <paramref name="consent"/>.
    /// </summary>
    /// <param name="consent">The consent that now covers the method.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the older consents are marked; the caller saves.</returns>
    private async Task RevokeOlderConsentsOfTheMethodAsync(CardChargeConsent consent, CancellationToken ct)
    {
        var methodRef = consent.MethodRef;
        var older = await _context.CardChargeConsents
            .Where(c => c.ClientId == consent.ClientId && c.GatewayModuleId == consent.GatewayModuleId
                && c.MethodRef == methodRef && c.RevokedAt == null && c.GrantedAt < consent.GrantedAt)
            .ToListAsync(ct);
        if (older.Count == 0)
        {
            return;
        }

        var wasDefault = older.Any(c => c.IsDefault);
        foreach (var other in older)
        {
            other.Revoke(_clock.UtcNow);
        }

        if (wasDefault)
        {
            consent.SetDefault(older);
        }
    }
}
