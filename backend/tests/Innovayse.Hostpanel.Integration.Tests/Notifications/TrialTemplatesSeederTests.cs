using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// The seeded free-trial emails exist in every language and print every value of the model their handlers send, with no
/// placeholder left unfilled; the translations are really translated and seeding again adds nothing.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TrialTemplatesSeederTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The trial every rendering uses.</summary>
    private static readonly TrialTemplateData Trial = new(42, "Starter Hosting", "2026-12-05", "https://portal.example/client/services/42");

    /// <summary>Renders the stored template of a slug and language: its subject followed by its body.</summary>
    /// <param name="slug">The template slug.</param>
    /// <param name="language">The language version.</param>
    /// <returns>The rendered mail.</returns>
    private async Task<string> RenderAsync(string slug, string language)
    {
        EmailTemplateModel model = slug switch
        {
            TrialTemplatesSeeder.EndingSlug => new TrialEndingTemplateModel(Trial, 9),
            TrialTemplatesSeeder.ConvertedSlug => new TrialConvertedTemplateModel(Trial, "2026-12-08"),
            _ => new TrialEndedTemplateModel(Trial),
        };

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await TrialTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        var renderer = new TemplateRenderer(new FakeEmailBrandingProvider());
        var template = await context.EmailTemplates.AsNoTracking()
            .FirstAsync(t => t.Slug == slug && t.Language == language, CancellationToken.None);
        var subject = await renderer.RenderTextAsync(template.Subject, model, CancellationToken.None);
        var body = await renderer.RenderHtmlAsync(template.Body, model, CancellationToken.None);
        return $"{subject}\n{body}";
    }

    /// <summary>Every mail prints the product, the end date and the link, plus the values of its own slug, with nothing left unfilled.</summary>
    /// <param name="slug">The template under test.</param>
    /// <param name="language">The language under test.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(TrialTemplatesSeeder.EndingSlug, "en")]
    [InlineData(TrialTemplatesSeeder.EndingSlug, "ru")]
    [InlineData(TrialTemplatesSeeder.EndingSlug, "hy")]
    [InlineData(TrialTemplatesSeeder.EndingSlug, "de")]
    [InlineData(TrialTemplatesSeeder.EndingSlug, "es")]
    [InlineData(TrialTemplatesSeeder.ConvertedSlug, "en")]
    [InlineData(TrialTemplatesSeeder.ConvertedSlug, "ru")]
    [InlineData(TrialTemplatesSeeder.ConvertedSlug, "hy")]
    [InlineData(TrialTemplatesSeeder.ConvertedSlug, "de")]
    [InlineData(TrialTemplatesSeeder.ConvertedSlug, "es")]
    [InlineData(TrialTemplatesSeeder.EndedSlug, "en")]
    [InlineData(TrialTemplatesSeeder.EndedSlug, "ru")]
    [InlineData(TrialTemplatesSeeder.EndedSlug, "hy")]
    [InlineData(TrialTemplatesSeeder.EndedSlug, "de")]
    [InlineData(TrialTemplatesSeeder.EndedSlug, "es")]
    public async Task PrintsEveryValueOfItsModel(string slug, string language)
    {
        var text = await RenderAsync(slug, language);

        text.Should().Contain("Starter Hosting").And.Contain("2026-12-05").And.Contain("https://portal.example/client/services/42");
        if (slug == TrialTemplatesSeeder.EndingSlug)
        {
            var phrase = language switch
            {
                "en" => "ends in 9 days",
                "ru" => "через 9 дн.",
                "hy" => "9 օրից",
                "de" => "in 9 Tagen",
                _ => "en 9 días",
            };
            text.Should().Contain(phrase);
        }

        if (slug == TrialTemplatesSeeder.ConvertedSlug)
        {
            text.Should().Contain("2026-12-08");
        }

        text.Should().NotContain("{{").And.NotContain("{%");
    }

    /// <summary>The Russian and Armenian versions are written in their own scripts and the German and Spanish ones differ from English.</summary>
    /// <param name="slug">The template under test.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(TrialTemplatesSeeder.EndingSlug)]
    [InlineData(TrialTemplatesSeeder.ConvertedSlug)]
    [InlineData(TrialTemplatesSeeder.EndedSlug)]
    public async Task TheTranslationsAreReallyTranslated(string slug)
    {
        (await RenderAsync(slug, "ru")).Should().MatchRegex("[Ѐ-ӿ]");
        (await RenderAsync(slug, "hy")).Should().MatchRegex("[԰-֏]");
        (await RenderAsync(slug, "de")).Should().NotBe(await RenderAsync(slug, "en"));
        (await RenderAsync(slug, "es")).Should().NotBe(await RenderAsync(slug, "en"));
    }

    /// <summary>Seeding again adds nothing twice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SeedingAgain_AddsNothing()
    {
        string[] slugs = [TrialTemplatesSeeder.EndingSlug, TrialTemplatesSeeder.ConvertedSlug, TrialTemplatesSeeder.EndedSlug];
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await TrialTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        await TrialTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        foreach (var slug in slugs)
        {
            (await context.EmailTemplates.AsNoTracking().CountAsync(t => t.Slug == slug)).Should().Be(5);
        }
    }
}
