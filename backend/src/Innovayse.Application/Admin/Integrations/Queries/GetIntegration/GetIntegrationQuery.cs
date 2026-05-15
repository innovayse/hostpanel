namespace Innovayse.Application.Admin.Integrations.Queries.GetIntegration;

/// <summary>Query that returns the full masked configuration for one integration.</summary>
/// <param name="Slug">URL-safe integration identifier, e.g. "stripe".</param>
public record GetIntegrationQuery(string Slug);
