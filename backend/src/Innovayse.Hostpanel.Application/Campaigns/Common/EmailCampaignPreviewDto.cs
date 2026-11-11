namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>A campaign mail rendered the way a recipient would receive it, with a sample unsubscribe link.</summary>
/// <param name="Subject">The rendered subject line.</param>
/// <param name="Html">The rendered HTML body, the unsubscribe footer included.</param>
public sealed record EmailCampaignPreviewDto(string Subject, string Html);
