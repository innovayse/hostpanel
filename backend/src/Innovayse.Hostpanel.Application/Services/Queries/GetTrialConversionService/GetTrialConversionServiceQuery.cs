namespace Innovayse.Hostpanel.Application.Services.Queries.GetTrialConversionService;

/// <summary>
/// Reads the converted trial whose first paid period billing is to invoice: the service only while its
/// trial is awaiting payment, so billing raises the conversion invoice without the renewal sweep, which
/// leaves running trials out.
/// </summary>
/// <param name="ServiceId">The trial service.</param>
public sealed record GetTrialConversionServiceQuery(int ServiceId);
