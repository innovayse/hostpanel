using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for <see cref="EmailCampaignTemplateSeeder"/>: the campaign frame exists in every language, the seed is
/// idempotent, and the frame prints the admin's HTML as written next to a link that unsubscribes the recipient.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class EmailCampaignTemplateSeederTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The languages the frame is written in.</summary>
    private static readonly string[] Languages = ["en", "ru", "hy"];

    /// <summary>Seeding twice leaves one version of the frame per language.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeededAsync_RunTwice_StoresEveryLanguageOnce()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await EmailCampaignTemplateSeeder.EnsureSeededAsync(context, CancellationToken.None);
        await EmailCampaignTemplateSeeder.EnsureSeededAsync(context, CancellationToken.None);

        var languages = await context.EmailTemplates.AsNoTracking()
            .Where(t => t.Slug == EmailCampaignTemplateSeeder.Slug)
            .Select(t => t.Language)
            .ToListAsync(CancellationToken.None);
        languages.Should().BeEquivalentTo(Languages);
    }

    /// <summary>Every version prints the body unescaped, the subject as written, and the unsubscribe link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EveryLanguage_RendersTheBodyRawAndTheUnsubscribeLink()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await EmailCampaignTemplateSeeder.EnsureSeededAsync(context, CancellationToken.None);
        var renderer = scope.ServiceProvider.GetRequiredService<TemplateRenderer>();
        var model = new EmailCampaignTemplateModel("Fish & chips", "<p>Fresh <b>fish</b></p>", "https://portal.example/unsubscribe?token=abc");
        var templates = await context.EmailTemplates.AsNoTracking()
            .Where(t => t.Slug == EmailCampaignTemplateSeeder.Slug)
            .ToListAsync(CancellationToken.None);

        foreach (var template in templates)
        {
            var subject = await renderer.RenderTextAsync(template.Subject, model, CancellationToken.None);
            var html = await renderer.RenderHtmlAsync(template.Body, model, CancellationToken.None);

            subject.Should().Be("Fish & chips", template.Language);
            html.Should().Contain("<p>Fresh <b>fish</b></p>", template.Language)
                .And.Contain("href=\"https://portal.example/unsubscribe?token=abc\"", template.Language);
        }
    }
}
