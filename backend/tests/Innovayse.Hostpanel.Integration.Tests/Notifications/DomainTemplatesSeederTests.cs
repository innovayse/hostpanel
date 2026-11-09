using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for <see cref="DomainTemplatesSeeder"/>: the seven domain mails exist in every
/// language, the seed is idempotent and keeps an operator's edit, and every seeded copy renders the
/// values of the model its handler sends.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainTemplatesSeederTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The languages every seeded mail is written in.</summary>
    private static readonly string[] Languages = ["en", "ru", "hy"];

    /// <summary>The seven slugs the domain handlers send.</summary>
    private static readonly string[] Slugs =
    [
        DomainTemplatesSeeder.RenewedSlug,
        DomainTemplatesSeeder.ExpiringSlug,
        DomainTemplatesSeeder.RegistrationFailedSlug,
        DomainTemplatesSeeder.RegistrationFailedAdminSlug,
        DomainTemplatesSeeder.RegisteredSlug,
        DomainTemplatesSeeder.ExpiredSlug,
        DomainTemplatesSeeder.TransferredSlug,
    ];

    /// <summary>Seeding twice leaves exactly one version of every slug in every language.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeededAsync_RunTwice_StoresEveryLanguageOfEverySlugOnceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await DomainTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        await DomainTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        var stored = await context.EmailTemplates.AsNoTracking()
            .Where(t => Slugs.Contains(t.Slug))
            .Select(t => new { t.Slug, t.Language })
            .ToListAsync(CancellationToken.None);
        stored.Should().HaveCount(Slugs.Length * Languages.Length);
        foreach (var slug in Slugs)
        {
            stored.Where(t => t.Slug == slug).Select(t => t.Language).Should().BeEquivalentTo(Languages);
        }
    }

    /// <summary>A version an operator already stored keeps its copy when the seed runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EnsureSeededAsync_StoredVersion_IsNotOverwrittenAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await DomainTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        var stored = await context.EmailTemplates
            .FirstAsync(t => t.Slug == DomainTemplatesSeeder.RenewedSlug && t.Language == "en", CancellationToken.None);
        stored.Update("Edited by staff", stored.Body, stored.Description);
        await context.SaveChangesAsync(CancellationToken.None);

        await DomainTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);

        var reloaded = await context.EmailTemplates.AsNoTracking()
            .FirstAsync(t => t.Slug == DomainTemplatesSeeder.RenewedSlug && t.Language == "en", CancellationToken.None);
        reloaded.Subject.Should().Be("Edited by staff");
    }

    /// <summary>Every language of every seeded mail prints the nested values of the model its handler sends.</summary>
    /// <param name="language">The language under test.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task SeededCopy_RenderedWithTheHandlersModel_PrintsTheDomainAndDatesAsync(string language)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await DomainTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        var renderer = new TemplateRenderer(new FakeEmailBrandingProvider());
        var domain = new DomainNameTemplateData("seeded.test");

        var renewed = await RenderAsync(context, renderer, DomainTemplatesSeeder.RenewedSlug, language,
            new DomainRenewedTemplateModel(new RenewedDomainTemplateData(1, "seeded.test", "2027-03-04")));
        var expiring = await RenderAsync(context, renderer, DomainTemplatesSeeder.ExpiringSlug, language,
            new DomainExpiringTemplateModel(new ExpiringDomainTemplateData(1, "seeded.test", "2026-12-01", 14)));
        var failed = await RenderAsync(context, renderer, DomainTemplatesSeeder.RegistrationFailedSlug, language,
            new DomainRegistrationFailedTemplateModel(domain, new InvoiceRefTemplateData(77)));
        var failedAdmin = await RenderAsync(context, renderer, DomainTemplatesSeeder.RegistrationFailedAdminSlug, language,
            new DomainRegistrationFailedAdminTemplateModel(domain, new ClientRefTemplateData(5), new InvoiceRefTemplateData(77), "registrar said no"));
        var registered = await RenderAsync(context, renderer, DomainTemplatesSeeder.RegisteredSlug, language,
            new DomainRegisteredTemplateModel(new RegisteredDomainTemplateData(1, "seeded.test", "Registrar", "2027-05-06")));
        var expired = await RenderAsync(context, renderer, DomainTemplatesSeeder.ExpiredSlug, language,
            new DomainExpiredTemplateModel(new ExpiredDomainTemplateData(1, "seeded.test", null)));
        var transferred = await RenderAsync(context, renderer, DomainTemplatesSeeder.TransferredSlug, language,
            new DomainTransferredTemplateModel(new RegisteredDomainTemplateData(1, "seeded.test", "Registrar", "2027-07-08")));

        renewed.Should().Contain("seeded.test").And.Contain("2027-03-04");
        expiring.Should().Contain("seeded.test").And.Contain("2026-12-01").And.Contain("14");
        failed.Should().Contain("seeded.test").And.Contain("77");
        failedAdmin.Should().Contain("seeded.test").And.Contain("77").And.Contain("registrar said no");
        registered.Should().Contain("seeded.test").And.Contain("2027-05-06");
        expired.Should().Contain("seeded.test");
        transferred.Should().Contain("seeded.test").And.Contain("2027-07-08");
    }

    /// <summary>Renders a stored template's subject and body with a model and joins them for assertions.</summary>
    /// <param name="context">The data context holding the seeded templates.</param>
    /// <param name="renderer">The renderer under test.</param>
    /// <param name="slug">The template slug.</param>
    /// <param name="language">The language version.</param>
    /// <param name="model">The model the handler sends.</param>
    /// <returns>The rendered subject followed by the rendered body.</returns>
    private static async Task<string> RenderAsync(
        IHostpanelDbContext context, TemplateRenderer renderer, string slug, string language, EmailTemplateModel model)
    {
        var template = await context.EmailTemplates.AsNoTracking()
            .FirstAsync(t => t.Slug == slug && t.Language == language, CancellationToken.None);
        var subject = await renderer.RenderTextAsync(template.Subject, model, CancellationToken.None);
        var body = await renderer.RenderHtmlAsync(template.Body, model, CancellationToken.None);
        return $"{subject}\n{body}";
    }
}
