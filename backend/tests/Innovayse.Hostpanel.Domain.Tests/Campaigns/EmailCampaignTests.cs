using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Campaigns;

/// <summary>Unit tests for the <see cref="EmailCampaign"/> aggregate's behaviour and invariants.</summary>
public sealed class EmailCampaignTests
{
    /// <summary>The fixed moment every method in these tests is handed as "now".</summary>
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>One valid segment that needs no value.</summary>
    private static readonly IReadOnlyList<CampaignSegmentDefinition> AllActive = [new(CampaignFilterType.AllActive, null)];

    /// <summary>Builds a draft with one segment.</summary>
    /// <returns>A new draft campaign.</returns>
    private static EmailCampaign NewDraft() => EmailCampaign.Create("Autumn offer", "<p>Hello</p>", "admin-1", AllActive);

    /// <summary>Builds a campaign that is scheduled for an hour from now.</summary>
    /// <returns>A scheduled campaign.</returns>
    private static EmailCampaign NewScheduled()
    {
        var campaign = NewDraft();
        campaign.Schedule(Now.AddHours(1), Now);
        return campaign;
    }

    /// <summary>Builds a campaign that is being sent.</summary>
    /// <returns>A campaign in the sending state.</returns>
    private static EmailCampaign NewSending()
    {
        var campaign = NewScheduled();
        campaign.StartSending();
        return campaign;
    }

    /// <summary>Create stores the content, starts as a draft and keeps the segments.</summary>
    [Fact]
    public void Create_StoresFields_AndStartsAsDraft()
    {
        var campaign = EmailCampaign.Create("  Autumn offer ", "<p>Hello</p>", " admin-1 ", AllActive);

        Assert.Equal("Autumn offer", campaign.Subject);
        Assert.Equal("<p>Hello</p>", campaign.Body);
        Assert.Equal("admin-1", campaign.CreatedBy);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Null(campaign.ScheduledAt);
        Assert.Null(campaign.SentAt);
        Assert.Empty(campaign.Recipients);
        Assert.Equal(CampaignFilterType.AllActive, Assert.Single(campaign.Segments).FilterType);
    }

    /// <summary>A draft may be written before any segment is chosen.</summary>
    [Fact]
    public void Create_WithoutSegments_IsAllowed()
    {
        var campaign = EmailCampaign.Create("Subject", "Body", "admin-1", []);

        Assert.Empty(campaign.Segments);
    }

    /// <summary>A blank subject, body or creator is refused.</summary>
    /// <param name="subject">The subject line.</param>
    /// <param name="body">The HTML body.</param>
    /// <param name="createdBy">The creator subject.</param>
    [Theory]
    [InlineData("", "Body", "admin-1")]
    [InlineData("   ", "Body", "admin-1")]
    [InlineData("Subject", "", "admin-1")]
    [InlineData("Subject", "  ", "admin-1")]
    [InlineData("Subject", "Body", "")]
    [InlineData("Subject", "Body", "  ")]
    public void Create_WithBlankField_Throws(string subject, string body, string createdBy)
    {
        Assert.Throws<ArgumentException>(() => EmailCampaign.Create(subject, body, createdBy, AllActive));
    }

    /// <summary>A subject, body or creator longer than its column is refused.</summary>
    [Fact]
    public void Create_WithTooLongField_Throws()
    {
        Assert.Throws<ArgumentException>(() => EmailCampaign.Create(new string('s', EmailCampaign.SubjectMaxLength + 1), "Body", "admin-1", AllActive));
        Assert.Throws<ArgumentException>(() => EmailCampaign.Create("Subject", new string('b', EmailCampaign.BodyMaxLength + 1), "admin-1", AllActive));
        Assert.Throws<ArgumentException>(() => EmailCampaign.Create("Subject", "Body", new string('a', EmailCampaign.CreatedByMaxLength + 1), AllActive));
    }

    /// <summary>A segment whose value does not fit its filter is refused when the campaign is created.</summary>
    [Fact]
    public void Create_WithInvalidSegment_ThrowsSegmentValueInvalid()
    {
        var ex = Assert.Throws<DomainRuleException>(() =>
            EmailCampaign.Create("Subject", "Body", "admin-1", [new(CampaignFilterType.Product, null)]));

        Assert.Equal(CampaignErrors.SegmentValueInvalid, ex.Code);
    }

    /// <summary>Update replaces the content and the whole set of segments.</summary>
    [Fact]
    public void Update_ReplacesContentAndSegments()
    {
        var campaign = NewDraft();

        campaign.Update("New subject", "<p>New</p>", [new(CampaignFilterType.Country, "am"), new(CampaignFilterType.Product, "7")]);

        Assert.Equal("New subject", campaign.Subject);
        Assert.Equal("<p>New</p>", campaign.Body);
        Assert.Equal(2, campaign.Segments.Count);
        Assert.DoesNotContain(campaign.Segments, s => s.FilterType == CampaignFilterType.AllActive);
    }

    /// <summary>An invalid segment leaves the draft unchanged, segments included.</summary>
    [Fact]
    public void Update_WithInvalidSegment_LeavesTheDraftUnchanged()
    {
        var campaign = NewDraft();

        Assert.Throws<DomainRuleException>(() => campaign.Update("New subject", "New body", [new(CampaignFilterType.Country, "Armenia")]));

        Assert.Equal("Autumn offer", campaign.Subject);
        Assert.Equal(CampaignFilterType.AllActive, Assert.Single(campaign.Segments).FilterType);
    }

    /// <summary>A blank subject is refused on update.</summary>
    [Fact]
    public void Update_WithBlankSubject_Throws()
    {
        var campaign = NewDraft();

        Assert.Throws<ArgumentException>(() => campaign.Update(" ", "Body", AllActive));
    }

    /// <summary>Only a draft can be edited.</summary>
    [Fact]
    public void Update_WhenNotDraft_ThrowsNotDraft()
    {
        var campaign = NewScheduled();

        var ex = Assert.Throws<DomainRuleException>(() => campaign.Update("Subject", "Body", AllActive));

        Assert.Equal(CampaignErrors.NotDraft, ex.Code);
    }

    /// <summary>Scheduling moves a draft to Scheduled and records the moment.</summary>
    [Fact]
    public void Schedule_MovesDraftToScheduled()
    {
        var campaign = NewDraft();
        var at = Now.AddDays(1);

        campaign.Schedule(at, Now);

        Assert.Equal(CampaignStatus.Scheduled, campaign.Status);
        Assert.Equal(at, campaign.ScheduledAt);
    }

    /// <summary>Scheduling without a moment sends now: the campaign is due at the caller's current moment.</summary>
    [Fact]
    public void Schedule_WithoutMoment_IsDueNow()
    {
        var campaign = NewDraft();

        campaign.Schedule(null, Now);

        Assert.Equal(CampaignStatus.Scheduled, campaign.Status);
        Assert.Equal(Now, campaign.ScheduledAt);
    }

    /// <summary>A campaign without a segment cannot be scheduled.</summary>
    [Fact]
    public void Schedule_WithoutSegment_ThrowsNoSegment()
    {
        var campaign = EmailCampaign.Create("Subject", "Body", "admin-1", []);

        var ex = Assert.Throws<DomainRuleException>(() => campaign.Schedule(Now.AddHours(1), Now));

        Assert.Equal(CampaignErrors.NoSegment, ex.Code);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
    }

    /// <summary>A moment that is now or earlier is refused.</summary>
    /// <param name="minutesFromNow">The offset of the send moment from now, in minutes.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Schedule_AtOrBeforeNow_ThrowsScheduleInPast(int minutesFromNow)
    {
        var campaign = NewDraft();

        var ex = Assert.Throws<DomainRuleException>(() => campaign.Schedule(Now.AddMinutes(minutesFromNow), Now));

        Assert.Equal(CampaignErrors.ScheduleInPast, ex.Code);
        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Null(campaign.ScheduledAt);
    }

    /// <summary>Only a draft can be scheduled.</summary>
    [Fact]
    public void Schedule_WhenNotDraft_ThrowsNotDraft()
    {
        var campaign = NewScheduled();

        var ex = Assert.Throws<DomainRuleException>(() => campaign.Schedule(Now.AddDays(2), Now));

        Assert.Equal(CampaignErrors.NotDraft, ex.Code);
    }

    /// <summary>Cancelling the schedule returns the campaign to a draft with no moment.</summary>
    [Fact]
    public void CancelSchedule_ReturnsToDraft_AndClearsTheMoment()
    {
        var campaign = NewScheduled();

        campaign.CancelSchedule();

        Assert.Equal(CampaignStatus.Draft, campaign.Status);
        Assert.Null(campaign.ScheduledAt);
    }

    /// <summary>Only a scheduled campaign has a schedule to cancel.</summary>
    [Fact]
    public void CancelSchedule_WhenNotScheduled_ThrowsNotScheduled()
    {
        var ex = Assert.Throws<DomainRuleException>(() => NewDraft().CancelSchedule());

        Assert.Equal(CampaignErrors.NotScheduled, ex.Code);
    }

    /// <summary>Starting moves a scheduled campaign to Sending.</summary>
    [Fact]
    public void StartSending_MovesScheduledToSending()
    {
        var campaign = NewScheduled();

        campaign.StartSending();

        Assert.Equal(CampaignStatus.Sending, campaign.Status);
    }

    /// <summary>Only a scheduled campaign can start sending.</summary>
    [Fact]
    public void StartSending_WhenNotScheduled_ThrowsNotScheduled()
    {
        var ex = Assert.Throws<DomainRuleException>(() => NewDraft().StartSending());

        Assert.Equal(CampaignErrors.NotScheduled, ex.Code);
    }

    /// <summary>MarkSent finishes a sending campaign and records the moment.</summary>
    [Fact]
    public void MarkSent_MovesSendingToSent_AndRecordsTheMoment()
    {
        var campaign = NewSending();
        var done = Now.AddHours(2);

        campaign.MarkSent(done);

        Assert.Equal(CampaignStatus.Sent, campaign.Status);
        Assert.Equal(done, campaign.SentAt);
    }

    /// <summary>Only a campaign being sent can be finished.</summary>
    [Fact]
    public void MarkSent_WhenNotSending_ThrowsNotSending()
    {
        var ex = Assert.Throws<DomainRuleException>(() => NewScheduled().MarkSent(Now));

        Assert.Equal(CampaignErrors.NotSending, ex.Code);
    }

    /// <summary>A draft may be deleted.</summary>
    [Fact]
    public void EnsureCanDelete_ForDraft_DoesNotThrow()
    {
        NewDraft().EnsureCanDelete();
    }

    /// <summary>A campaign that has left the draft state may not be deleted.</summary>
    [Fact]
    public void EnsureCanDelete_WhenNotDraft_ThrowsNotDraft()
    {
        var ex = Assert.Throws<DomainRuleException>(() => NewSending().EnsureCanDelete());

        Assert.Equal(CampaignErrors.NotDraft, ex.Code);
    }

    /// <summary>AddRecipient fixes the client with the address and language it has now.</summary>
    [Fact]
    public void AddRecipient_WhileSending_AddsAPendingRecipient()
    {
        var campaign = NewSending();

        var recipient = campaign.AddRecipient(5, "client@example.com", "hy");

        Assert.Same(recipient, Assert.Single(campaign.Recipients));
        Assert.Equal(5, recipient.ClientId);
        Assert.Equal("client@example.com", recipient.Email);
        Assert.Equal("hy", recipient.Language);
        Assert.Equal(CampaignRecipientStatus.Pending, recipient.Status);
        Assert.Equal(0, recipient.Attempts);
    }

    /// <summary>Recipients are fixed only once sending has started.</summary>
    [Fact]
    public void AddRecipient_WhenNotSending_ThrowsNotSending()
    {
        var ex = Assert.Throws<DomainRuleException>(() => NewScheduled().AddRecipient(5, "client@example.com", null));

        Assert.Equal(CampaignErrors.NotSending, ex.Code);
    }
}
