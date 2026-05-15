namespace Innovayse.API.Admin;

using Innovayse.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides reference data endpoints for countries and currencies.
/// Any authenticated user can access these read-only endpoints.
/// </summary>
[ApiController]
[Route("api/reference")]
[Authorize]
public sealed class ReferenceDataController : ControllerBase
{
    /// <summary>
    /// Returns the full list of ISO 3166-1 alpha-2 countries.
    /// </summary>
    /// <returns>List of country code and name pairs.</returns>
    [HttpGet("countries")]
    public ActionResult<IReadOnlyList<CountryDto>> GetCountries()
    {
        return Ok(CountryList.All);
    }

    /// <summary>
    /// Returns the list of supported currencies.
    /// </summary>
    /// <returns>List of currency code, name, and symbol records.</returns>
    [AllowAnonymous]
    [HttpGet("currencies")]
    public ActionResult<IReadOnlyList<CurrencyDto>> GetCurrencies()
    {
        return Ok(CurrencyList.All);
    }
}
