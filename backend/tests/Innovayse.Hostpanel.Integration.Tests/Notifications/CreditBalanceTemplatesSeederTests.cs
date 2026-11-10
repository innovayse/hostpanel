using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// The seeded credit balance email exists in every language and prints every value of the model its handler sends: the signed change, the
/// new balance, the currency and the link to the credit page, with no placeholder left unfilled, and the Russian and Armenian versions
/// are really translated.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreditBalanceTemplatesSeederTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The model every rendering uses: a deduction that left 1234.56.</summary>
    private static readonly CreditBalanceChangedTemplateModel Model =
        new(new CreditBalanceTemplateData(-15.5m, 1234.56m, "USD", "https://portal.example/client/credit"));

    /// <summary>Renders the stored template of a language: its subject followed by its body.</summary>
    /// <param name="language">The language version.</param>
    /// <returns>The rendered mail.</returns>
    private async Task<string> RenderAsync(string language)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await CreditBalanceTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        var renderer = new TemplateRenderer(new FakeEmailBrandingProvider());
        var template = await context.EmailTemplates.AsNoTracking()
            .FirstAsync(t => t.Slug == CreditBalanceTemplatesSeeder.ChangedSlug && t.Language == language, CancellationToken.None);
        var subject = await renderer.RenderTextAsync(template.Subject, Model, CancellationToken.None);
        var body = await renderer.RenderHtmlAsync(template.Body, Model, CancellationToken.None);
        return $"{subject}\n{body}";
    }

    /// <summary>The mail prints the change, the new balance, the currency and the link, with nothing left unfilled.</summary>
    /// <param name="language">The language under test.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task PrintsTheChangeTheBalanceTheCurrencyAndTheLink(string language)
    {
        var text = await RenderAsync(language);

        text.Should().Contain("15.5").And.Contain("1234.56").And.Contain("USD").And.Contain("https://portal.example/client/credit");
        text.Should().NotContain("{{").And.NotContain("{%");
    }

    /// <summary>The Russian and Armenian versions are written in their own scripts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheTranslationsAreReallyTranslated()
    {
        (await RenderAsync("ru")).Should().MatchRegex("[Ѐ-ӿ]");
        (await RenderAsync("hy")).Should().MatchRegex("[԰-֏]");
    }

    /// <summary>Seeding again leaves an edit the operator made alone and adds nothing twice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SeedingAgain_KeepsTheOperatorsEditAndAddsNothing()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await CreditBalanceTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        var before = await context.EmailTemplates.AsNoTracking().CountAsync(t => t.Slug == CreditBalanceTemplatesSeeder.ChangedSlug);

        await CreditBalanceTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        before.Should().Be(3);
        (await context.EmailTemplates.AsNoTracking().CountAsync(t => t.Slug == CreditBalanceTemplatesSeeder.ChangedSlug)).Should().Be(before);
    }
}
