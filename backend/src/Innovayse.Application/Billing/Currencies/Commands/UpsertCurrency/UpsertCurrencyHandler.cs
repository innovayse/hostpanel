namespace Innovayse.Application.Billing.Currencies.Commands.UpsertCurrency;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Handles <see cref="UpsertCurrencyCommand"/>.</summary>
/// <param name="currencies">The configured currencies.</param>
/// <param name="uow">Unit of work for persistence.</param>
public sealed class UpsertCurrencyHandler(ICurrencyRepository currencies, IUnitOfWork uow)
{
    /// <summary>Creates the currency, or rewrites the stored one's display, rate and switch.</summary>
    /// <param name="cmd">The command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the base is asked to be switched off.</exception>
    public async Task HandleAsync(UpsertCurrencyCommand cmd, CancellationToken ct)
    {
        var row = await currencies.FindAsync(cmd.Code, ct);

        if (row is null)
        {
            row = Currency.Create(
                cmd.Code, cmd.Numeric, cmd.Prefix, cmd.Suffix, cmd.Decimals, cmd.RateToBase, isBase: false);
            currencies.Add(row);
        }
        else
        {
            row.UpdateDisplay(cmd.Prefix, cmd.Suffix, cmd.Decimals);

            // The base is worth one of itself by definition; the form's figure for it is noise,
            // and the domain would refuse it.
            if (!row.IsBase)
            {
                row.UpdateRate(cmd.RateToBase);
            }
        }

        ApplySwitch(row, cmd.IsEnabled);

        await uow.SaveChangesAsync(ct);
    }

    /// <summary>Turns the currency on or off as asked; the domain refuses to turn off the base.</summary>
    /// <param name="currency">The row.</param>
    /// <param name="enabled">The switch position asked for.</param>
    /// <exception cref="InvalidOperationException">Thrown when the base is asked to be switched off.</exception>
    private static void ApplySwitch(Currency currency, bool enabled)
    {
        if (enabled)
        {
            currency.Enable();
        }
        else
        {
            currency.Disable();
        }
    }
}
