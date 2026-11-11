using Innovayse.Hostpanel.Application.Campaigns.Common;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.UpdateEmailCampaign;

/// <summary>Command to replace the subject, body and segments of a draft campaign.</summary>
/// <param name="Id">
/// The campaign identifier to update; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay replaces it.
/// </param>
/// <param name="Subject">The subject line of the email.</param>
/// <param name="Body">The HTML body of the email.</param>
/// <param name="Segments">The complete set of segments; the current ones of the campaign are replaced by it.</param>
public sealed record UpdateEmailCampaignCommand(int Id, string Subject, string Body, IReadOnlyList<CampaignSegmentDto> Segments);
