namespace Innovayse.Application.Billing.Currencies.Commands.SetBaseCurrency;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Handles <see cref="SetBaseCurrencyCommand"/>.</summary>
/// <param name="currencies">The configured currencies.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class SetBaseCurrencyHandler(ICurrencyRepository currencies, IUnitOfWork uow)
{
    /// <summary>Promotes the currency and re-expresses the others' rates against it.</summary>
    /// <remarks>
    /// Every stored rate says how much of the old base one unit is worth. If the new base was
    /// worth <c>n</c> old-base units, a currency worth <c>r</c> old-base units is worth
    /// <c>r / n</c> new-base units — and the old base itself, worth 1, becomes <c>1 / n</c>. That
    /// <c>n</c> is read before <c>MakeBase</c> resets the new base's own rate to 1, and the old
    /// base is demoted before its rate is written, because the domain refuses any rate but 1 on
    /// a base.
    /// </remarks>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the code is not a configured currency.</exception>
    public async Task HandleAsync(SetBaseCurrencyCommand cmd, CancellationToken ct)
    {
        var all = await currencies.ListAsync(enabledOnly: false, ct);
        var code = cmd.Code.ToUpperInvariant();

        var newBase = all.FirstOrDefault(c => c.Code == code)
            ?? throw new InvalidOperationException($"'{code}' is not a configured currency.");

        if (newBase.IsBase)
        {
            return;
        }

        var newBaseInOldBase = newBase.RateToBase;
        newBase.MakeBase();

        foreach (var other in all.Where(c => c.Code != code))
        {
            if (other.IsBase)
            {
                other.UnmakeBase();
            }

            other.UpdateRate(other.RateToBase / newBaseInOldBase);
        }

        await uow.SaveChangesAsync(ct);
    }
}
