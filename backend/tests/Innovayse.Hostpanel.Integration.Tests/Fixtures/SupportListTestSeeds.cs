using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;
using Innovayse.Hostpanel.Domain.Support.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builders for the announcement, network issue and knowledge base list tests: rows seeded straight into the database
/// with the text, state and moment a test needs.
/// </summary>
public static class SupportListTestSeeds
{
    /// <summary>Seeds one announcement.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="title">The title.</param>
    /// <param name="content">The body.</param>
    /// <param name="isPublished">Whether the announcement is published.</param>
    /// <returns>The new announcement id.</returns>
    public static async Task<int> SeedAnnouncementAsync(
        WebApplicationFactory<Program> host, string title, string content = "Body.", bool isPublished = true)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var announcement = Announcement.Create(title, content, isPublished);
        context.Announcements.Add(announcement);
        await context.SaveChangesAsync(CancellationToken.None);
        return announcement.Id;
    }

    /// <summary>Seeds one network issue in the status given.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="title">The title.</param>
    /// <param name="server">The affected server, or <see langword="null"/>.</param>
    /// <param name="description">The description.</param>
    /// <param name="status">The lifecycle status the issue is moved to after it is created.</param>
    /// <param name="type">The issue type.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The new network issue id.</returns>
    public static async Task<int> SeedNetworkIssueAsync(
        WebApplicationFactory<Program> host,
        string title,
        string? server = null,
        string description = "Something is down.",
        NetworkIssueStatus status = NetworkIssueStatus.Reported,
        NetworkIssueType type = NetworkIssueType.Server,
        NetworkIssuePriority priority = NetworkIssuePriority.Medium)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var issue = NetworkIssue.Create(title, type, server, priority, TestMoments.Seeded, description);
        issue.Update(title, type, server, priority, status, TestMoments.Seeded, null, description);
        context.NetworkIssues.Add(issue);
        await context.SaveChangesAsync(CancellationToken.None);
        return issue.Id;
    }
}
