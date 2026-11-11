using Innovayse.Hostpanel.Application.Campaigns.Common;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.CreateEmailCampaign;

/// <summary>Command to write a new email campaign as a draft. The author is the caller, read from the token.</summary>
/// <param name="Subject">The subject line of the email.</param>
/// <param name="Body">The HTML body of the email.</param>
/// <param name="Segments">The rules that pick the recipients; may be empty while the draft is being written.</param>
public sealed record CreateEmailCampaignCommand(string Subject, string Body, IReadOnlyList<CampaignSegmentDto> Segments);
