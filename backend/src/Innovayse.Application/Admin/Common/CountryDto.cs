namespace Innovayse.Application.Admin.Common;

/// <summary>
/// DTO representing a country with its ISO 3166-1 alpha-2 code and English name.
/// </summary>
/// <param name="Code">ISO 3166-1 alpha-2 two-letter country code (e.g. "US").</param>
/// <param name="Name">English name of the country (e.g. "United States").</param>
public record CountryDto(string Code, string Name);
