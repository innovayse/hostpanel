using Innovayse.Hostpanel.Application.Clients.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>Asks the service that holds the register of VAT numbers whether a number is registered.</summary>
/// <remarks>
/// One port for every register: a handler names the country and the number and never learns which service
/// answered. Implementations live in Infrastructure, one adapter per service.
/// </remarks>
public interface IVatNumberChecker
{
    /// <summary>Checks one VAT number.</summary>
    /// <param name="lookup">The country and the number to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The service's answer: valid with the name and address it returned when it returns them, invalid, or unavailable when
    /// the service or the member state behind it could not be reached or gave no verdict. It never throws for a service
    /// that is down and never reports such a failure as invalid.
    /// </returns>
    public Task<VatNumberCheck> CheckAsync(VatNumberLookup lookup, CancellationToken ct);
}
