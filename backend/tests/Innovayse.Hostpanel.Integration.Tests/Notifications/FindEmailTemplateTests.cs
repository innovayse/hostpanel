using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Queries.FindEmailTemplate;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>FindEmailTemplate</c> use case, driven through the bus the way the mail pipeline does: the
/// recipient's language wins, the default language stands in, and a slug nobody wrote is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FindEmailTemplateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Stores a template version for a slug.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="slug">The template slug.</param>
    /// <param name="language">The version's language.</param>
    /// <returns>A task that completes once the version is stored.</returns>
    private static async Task StoreAsync(IServiceProvider services, string slug, string language)
    {
        var context = services.GetRequiredService<IHostpanelDbContext>();
        context.EmailTemplates.Add(EmailTemplate.Create(slug, language, $"Subject {language}", $"Body {language}", null));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Asks the bus for a template.</summary>
    /// <param name="services">The host's service provider.</param>
    /// <param name="slug">The template slug.</param>
    /// <param name="language">The recipient's language.</param>
    /// <returns>The query's outcome.</returns>
    private static Task<Result<EmailTemplateDto>> FindAsync(IServiceProvider services, string slug, string? language) =>
        services.GetRequiredService<IMessageBus>().InvokeAsync<Result<EmailTemplateDto>>(new FindEmailTemplateQuery(slug, language), CancellationToken.None);

    /// <summary>The version in the recipient's language is returned, whatever case the language is given in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Find_RecipientsLanguageWritten_ReturnsThatVersion()
    {
        using var scope = factory.Services.CreateScope();
        var slug = $"find-{Guid.NewGuid():N}";
        await StoreAsync(scope.ServiceProvider, slug, "en");
        await StoreAsync(scope.ServiceProvider, slug, "ru");

        var result = await FindAsync(scope.ServiceProvider, slug, "RU");

        result.Value!.Language.Should().Be("ru");
        result.Value.Subject.Should().Be("Subject ru");
    }

    /// <summary>A language nobody translated falls back to the default language's version.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Find_LanguageNotWritten_FallsBackToTheDefault()
    {
        using var scope = factory.Services.CreateScope();
        var slug = $"find-{Guid.NewGuid():N}";
        await StoreAsync(scope.ServiceProvider, slug, "en");

        var armenian = await FindAsync(scope.ServiceProvider, slug, "hy");
        var unknown = await FindAsync(scope.ServiceProvider, slug, null);

        armenian.Value!.Language.Should().Be("en");
        unknown.Value!.Language.Should().Be("en");
    }

    /// <summary>A slug with no version in either language is refused with <c>EMAIL_TEMPLATE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Find_UnknownSlug_RefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();

        var result = await FindAsync(scope.ServiceProvider, $"missing-{Guid.NewGuid():N}", "en");

        result.ErrorCode.Should().Be(HostpanelErrorCodes.EMAIL_TEMPLATE_NOT_FOUND);
    }
}
