namespace Innovayse.Hostpanel.Infrastructure.Vat;

/// <summary>The error body of the UK tax authority's API.</summary>
/// <param name="Code">The error code: <c>NOT_FOUND</c> for a number that is not registered, <c>INVALID_REQUEST</c> for one that is malformed.</param>
internal sealed record HmrcErrorWire(string? Code);
