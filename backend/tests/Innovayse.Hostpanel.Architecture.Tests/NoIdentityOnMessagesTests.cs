namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// Identity never travels in a body: the handler reads it from <c>ICurrentUserService</c>. A command or
/// query carrying <c>UserId</c> or <c>TenantId</c> invites the controller to copy it from the client.
/// </summary>
public sealed class NoIdentityOnMessagesTests
{
    /// <summary>The property names that smuggle the caller's identity into a message.</summary>
    private static readonly string[] ForbiddenNames = ["UserId", "TenantId", "Subject", "OrganizationId"];

    /// <summary>
    /// Messages whose <c>Subject</c> is a human-written subject line or title (a quote, an email
    /// template, a support ticket), not an account identifier. Each entry is a type name and the
    /// reason it is allowed.
    /// </summary>
    private static readonly Dictionary<string, string> AllowedSubjectLines = new()
    {
        ["CreateQuoteCommand"] = "Subject is the quote title typed by the staff member.",
        ["UpdateQuoteCommand"] = "Subject is the quote title typed by the staff member.",
        ["CreateEmailTemplateCommand"] = "Subject is the email subject line of the template.",
        ["UpdateEmailTemplateCommand"] = "Subject is the email subject line of the template.",
        ["AdminCreateTicketCommand"] = "Subject is the ticket title typed by the staff member.",
        ["CreateTicketCommand"] = "Subject is the ticket title typed by the client.",
        ["CreateEmailCampaignCommand"] = "Subject is the subject line of the campaign email.",
        ["UpdateEmailCampaignCommand"] = "Subject is the subject line of the campaign email.",
    };

    /// <summary>No command or query record exposes a <c>UserId</c>, <c>TenantId</c>, <c>Subject</c> or <c>OrganizationId</c> property.</summary>
    [Fact]
    public void CommandsAndQueries_CarryNoCallerIdentity()
    {
        var offenders = Layers.AuthoredTypesOf(Layers.Application)
            .Where(type => type.IsClass
                && (type.Name.EndsWith("Command", StringComparison.Ordinal)
                    || type.Name.EndsWith("Query", StringComparison.Ordinal)))
            .SelectMany(type => type.GetProperties()
                .Where(property => ForbiddenNames.Contains(property.Name)
                    && !(property.Name == "Subject" && AllowedSubjectLines.ContainsKey(type.Name)))
                .Select(property => $"{type.FullName}.{property.Name}"))
            .ToList();

        offenders.Should().BeEmpty("the handler resolves identity from ICurrentUserService, not from the message");
    }

    /// <summary>
    /// The allow-list is not stale: each entry is still a command or query with a <c>Subject</c>
    /// property, so a renamed or removed message cannot leave an entry behind.
    /// </summary>
    [Fact]
    public void AllowedSubjectLines_ContainOnlyMessagesThatStillHaveASubject()
    {
        var withSubject = Layers.AuthoredTypesOf(Layers.Application)
            .Where(type => type.IsClass
                && (type.Name.EndsWith("Command", StringComparison.Ordinal)
                    || type.Name.EndsWith("Query", StringComparison.Ordinal)))
            .Where(type => type.GetProperty("Subject") is not null)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        AllowedSubjectLines.Keys.Except(withSubject).Should().BeEmpty("delete an allow-list entry when its message goes or loses its Subject");
    }
}
