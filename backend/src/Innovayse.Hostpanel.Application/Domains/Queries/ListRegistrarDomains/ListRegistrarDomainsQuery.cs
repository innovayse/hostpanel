namespace Innovayse.Hostpanel.Application.Domains.Queries.ListRegistrarDomains;

/// <summary>
/// Query to read one page of the domains a registrar plugin reports for its account, each marked with whether the panel
/// already has it, so staff can pick the ones to import.
/// </summary>
/// <param name="RegistrarModule">
/// The id of the installed registrar plugin to ask, in any letter case. Nullable so the route value can overlay it: a
/// non-nullable member would make a body or query without it a binding error before <c>with { RegistrarModule = module }</c>
/// ever ran.
/// </param>
/// <param name="Cursor">The cursor of the page wanted, exactly as the previous page returned it; null reads the first page.</param>
public sealed record ListRegistrarDomainsQuery(string? RegistrarModule, string? Cursor = null);
