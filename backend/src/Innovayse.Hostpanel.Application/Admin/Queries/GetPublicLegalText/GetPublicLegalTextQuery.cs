namespace Innovayse.Hostpanel.Application.Admin.Queries.GetPublicLegalText;

/// <summary>
/// Query that returns the operator's legal text of one storefront page, in every language it is written in.
/// The page names which keys are read; the caller never names a key, so an unauthenticated request cannot ask
/// for any other setting.
/// </summary>
/// <param name="Page">The page slug: <c>terms</c>, <c>privacy</c>, <c>refund</c>, <c>acceptable_use</c> or <c>cookies</c>.</param>
public sealed record GetPublicLegalTextQuery(string Page);
