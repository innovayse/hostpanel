namespace Innovayse.Application.Admin.Queries.GetPublicSettings;

/// <summary>
/// Query that returns the storefront-facing subset of system settings.
/// Takes no parameters: the set of exposed keys is fixed by the handler, not chosen
/// by the caller, so an unauthenticated request cannot ask for anything else.
/// </summary>
public record GetPublicSettingsQuery;
