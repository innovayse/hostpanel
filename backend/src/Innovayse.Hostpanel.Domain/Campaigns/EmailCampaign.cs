using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Campaigns;

/// <summary>
/// A marketing email an admin writes once and sends to a chosen group of clients. Only clients whose
/// <c>MarketingOptIn</c> consent flag is true are ever recipients. Stored in the <c>EmailCampaigns</c> table.
/// </summary>
public sealed class EmailCampaign : BaseEntity, IAggregateRoot
{
    /// <summary>Longest subject the stored column holds.</summary>
    public const int SubjectMaxLength = 200;

    /// <summary>Longest HTML body the stored column holds.</summary>
    public const int BodyMaxLength = 100_000;

    /// <summary>Longest creator subject the stored column holds.</summary>
    public const int CreatedByMaxLength = 450;

    /// <summary>Internal mutable segments list.</summary>
    private readonly List<EmailCampaignSegment> _segments = [];

    /// <summary>Internal mutable recipients list.</summary>
    private readonly List<EmailCampaignRecipient> _recipients = [];

    /// <summary>Gets the subject line of the email.</summary>
    public string Subject { get; private set; } = string.Empty;

    /// <summary>Gets the HTML body of the email.</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>Gets where the campaign is in its life.</summary>
    public CampaignStatus Status { get; private set; }

    /// <summary>Gets the moment sending is due to start; set only while <see cref="CampaignStatus.Scheduled"/> or later.</summary>
    public DateTimeOffset? ScheduledAt { get; private set; }

    /// <summary>Gets the moment delivery finished, or <see langword="null"/> until then.</summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Gets the identity subject of the admin who wrote the campaign.</summary>
    public string CreatedBy { get; private set; } = string.Empty;

    /// <summary>Gets the UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Gets the rules that pick the recipients; a client matched by any of them is a recipient.</summary>
    public IReadOnlyList<EmailCampaignSegment> Segments => _segments.AsReadOnly();

    /// <summary>Gets the clients the campaign is delivered to; empty until sending starts.</summary>
    public IReadOnlyList<EmailCampaignRecipient> Recipients => _recipients.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private EmailCampaign() { }

    /// <summary>Writes a new campaign as a draft.</summary>
    /// <param name="subject">The subject line.</param>
    /// <param name="body">The HTML body.</param>
    /// <param name="createdBy">The identity subject of the admin writing it.</param>
    /// <param name="segments">The rules that pick the recipients; may be empty while the draft is being written.</param>
    /// <returns>A new <see cref="EmailCampaign"/> in <see cref="CampaignStatus.Draft"/>.</returns>
    /// <exception cref="ArgumentException">The subject, body or creator is blank or longer than its column.</exception>
    /// <exception cref="DomainRuleException">A segment's value does not fit its filter.</exception>
    public static EmailCampaign Create(string subject, string body, string createdBy, IReadOnlyList<CampaignSegmentDefinition> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var trimmedCreator = createdBy?.Trim();
        if (string.IsNullOrEmpty(trimmedCreator) || trimmedCreator.Length > CreatedByMaxLength)
        {
            throw new ArgumentException($"A campaign records its creator, at most {CreatedByMaxLength} characters.", nameof(createdBy));
        }

        var campaign = new EmailCampaign
        {
            Status = CampaignStatus.Draft,
            CreatedBy = trimmedCreator,
        };
        var built = BuildSegments(segments);
        campaign.SetContent(subject, body);
        campaign._segments.AddRange(built);
        return campaign;
    }

    /// <summary>Replaces the subject, body and segments of a draft.</summary>
    /// <param name="subject">The subject line.</param>
    /// <param name="body">The HTML body.</param>
    /// <param name="segments">The complete set of segments; the current ones are replaced by it.</param>
    /// <exception cref="ArgumentException">The subject or body is blank or longer than its column.</exception>
    /// <exception cref="DomainRuleException">The campaign is not a draft, or a segment's value does not fit its filter.</exception>
    public void Update(string subject, string body, IReadOnlyList<CampaignSegmentDefinition> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        EnsureStatus(CampaignStatus.Draft, CampaignErrors.NotDraft);

        // Segments are checked first and replaced last, so a refusal anywhere leaves the draft as it was.
        var built = BuildSegments(segments);
        SetContent(subject, body);
        _segments.Clear();
        _segments.AddRange(built);
    }

    /// <summary>Queues a draft to start sending: at a future moment, or at once when no moment is given.</summary>
    /// <param name="at">When sending is due to start, or <see langword="null"/> to send now (due at <paramref name="now"/>).</param>
    /// <param name="now">The current moment, from the caller's clock.</param>
    /// <exception cref="DomainRuleException">The campaign is not a draft, has no segment, or a given <paramref name="at"/> is not after <paramref name="now"/>.</exception>
    public void Schedule(DateTimeOffset? at, DateTimeOffset now)
    {
        EnsureStatus(CampaignStatus.Draft, CampaignErrors.NotDraft);

        if (_segments.Count == 0)
        {
            throw new DomainRuleException(CampaignErrors.NoSegment, "A campaign needs at least one segment before it can be scheduled.");
        }

        if (at <= now)
        {
            throw new DomainRuleException(CampaignErrors.ScheduleInPast, "A campaign is scheduled for a moment in the future.");
        }

        ScheduledAt = at ?? now;
        Status = CampaignStatus.Scheduled;
    }

    /// <summary>Takes a scheduled campaign back to a draft so it can be edited again.</summary>
    /// <exception cref="DomainRuleException">The campaign is not scheduled.</exception>
    public void CancelSchedule()
    {
        EnsureStatus(CampaignStatus.Scheduled, CampaignErrors.NotScheduled);

        ScheduledAt = null;
        Status = CampaignStatus.Draft;
    }

    /// <summary>Begins delivery of a scheduled campaign.</summary>
    /// <exception cref="DomainRuleException">The campaign is not scheduled.</exception>
    public void StartSending()
    {
        EnsureStatus(CampaignStatus.Scheduled, CampaignErrors.NotScheduled);

        Status = CampaignStatus.Sending;
    }

    /// <summary>
    /// Fixes a client as a recipient while the campaign is being sent, with the address and language the client has now.
    /// Who is a recipient, and that an address shared by several clients is mailed once, is decided by the audience the caller
    /// reads; the table's unique (campaign, client) index is the last guard against a client fixed twice.
    /// </summary>
    /// <param name="clientId">The client to deliver to; the caller has already checked the client's marketing consent.</param>
    /// <param name="email">The client's email address now.</param>
    /// <param name="language">The client's language now, or <see langword="null"/>.</param>
    /// <returns>The new recipient.</returns>
    /// <exception cref="ArgumentException">The address is blank or longer than its column, or the language is longer than its column.</exception>
    /// <exception cref="DomainRuleException">The campaign is not being sent.</exception>
    public EmailCampaignRecipient AddRecipient(int clientId, string email, string? language)
    {
        EnsureStatus(CampaignStatus.Sending, CampaignErrors.NotSending);

        var recipient = EmailCampaignRecipient.Create(clientId, email, language);
        _recipients.Add(recipient);
        return recipient;
    }

    /// <summary>Records that delivery has finished.</summary>
    /// <param name="now">The moment delivery finished, from the caller's clock.</param>
    /// <exception cref="DomainRuleException">The campaign is not being sent.</exception>
    public void MarkSent(DateTimeOffset now)
    {
        EnsureStatus(CampaignStatus.Sending, CampaignErrors.NotSending);

        Status = CampaignStatus.Sent;
        SentAt = now;
    }

    /// <summary>Refuses to delete a campaign that has left the draft state, because it is queued or its mail has gone out.</summary>
    /// <exception cref="DomainRuleException">The campaign is not a draft.</exception>
    public void EnsureCanDelete()
    {
        EnsureStatus(CampaignStatus.Draft, CampaignErrors.NotDraft);
    }

    /// <summary>Checks and stores the subject and body.</summary>
    /// <param name="subject">The subject line.</param>
    /// <param name="body">The HTML body.</param>
    /// <exception cref="ArgumentException">The subject or body is blank or longer than its column.</exception>
    private void SetContent(string subject, string body)
    {
        var trimmedSubject = subject?.Trim();
        if (string.IsNullOrEmpty(trimmedSubject) || trimmedSubject.Length > SubjectMaxLength)
        {
            throw new ArgumentException($"A campaign needs a subject of at most {SubjectMaxLength} characters.", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(body) || body.Length > BodyMaxLength)
        {
            throw new ArgumentException($"A campaign needs a body of at most {BodyMaxLength} characters.", nameof(body));
        }

        Subject = trimmedSubject;
        Body = body;
    }

    /// <summary>Checks every segment definition and builds the segments, touching no campaign state.</summary>
    /// <param name="segments">The complete set of segment definitions.</param>
    /// <returns>The segments to store.</returns>
    /// <exception cref="DomainRuleException">A segment's value does not fit its filter.</exception>
    private static List<EmailCampaignSegment> BuildSegments(IReadOnlyList<CampaignSegmentDefinition> segments)
    {
        return segments.Select(EmailCampaignSegment.Create).ToList();
    }

    /// <summary>Refuses an operation the campaign's current status does not allow.</summary>
    /// <param name="required">The status the operation needs.</param>
    /// <param name="code">The refusal code to throw when the status is another one.</param>
    /// <exception cref="DomainRuleException">The campaign is not in the required status.</exception>
    private void EnsureStatus(CampaignStatus required, string code)
    {
        if (Status != required)
        {
            throw new DomainRuleException(code, $"This needs a campaign in status {required}; current status is {Status}.");
        }
    }
}
