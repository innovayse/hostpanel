using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.ListEmailCampaigns;

/// <summary>Query to retrieve a page of email campaigns for the admin panel, newest first, with optional filters.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (1 to 100); a size outside that range is refused, not clamped.</param>
/// <param name="Search">Optional text; only campaigns whose subject contains it (case-insensitive) are returned. Blank returns all.</param>
/// <param name="Status">Optional lifecycle status; null returns every status.</param>
public sealed record ListEmailCampaignsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    CampaignStatus? Status = null) : IListQuery;
