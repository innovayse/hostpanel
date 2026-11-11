namespace Innovayse.Hostpanel.Application.Campaigns.Commands.ScheduleEmailCampaign;

/// <summary>Command to queue a draft campaign for sending.</summary>
/// <param name="Id">
/// The campaign identifier to schedule; wins over any id in the body. A value type, so it is left non-nullable: a missing
/// <c>int</c> in the body binds to <c>0</c> and the route overlay replaces it.
/// </param>
/// <param name="At">When sending is due to start, which must be in the future, or <c>null</c> to send now.</param>
public sealed record ScheduleEmailCampaignCommand(int Id, DateTimeOffset? At);
