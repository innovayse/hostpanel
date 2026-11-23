using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>GatewayPaymentReversedAdminHandler</c>: a gateway payment reported as no longer paid after the invoice was
/// settled through it mails the configured staff address, says nothing was changed, and seeds the template on first use.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GatewayPaymentReversedAdminHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address configured on the test host.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>The alert sends one staff mail naming the invoice and the module, and the template row exists.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentReversed_MailsStaffOnceAndSeedsTheTemplate()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await PublishAndWaitAsync(host, new GatewayPaymentReversedEvent(818181, 7, "bank-gateway") { OccurredAt = TestMoments.Seeded });

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("#818181");
        mail.Body.Should().Contain("bank-gateway").And.Contain("not changed");
        (await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == StaffAlertTemplatesSeeder.GatewayPaymentReversedSlug))
            .Should().BeTrue();
    }

    /// <summary>With neither address configured nothing is sent (the handler logs an error naming the setting instead).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentReversed_WithNoAddress_SendsNothing()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);

        await PublishAndWaitAsync(host, new GatewayPaymentReversedEvent(1, 1, "bank-gateway") { OccurredAt = TestMoments.Seeded });

        sender.Sent.Should().BeEmpty();
    }

    /// <summary>
    /// Every language of the seeded staff template prints the values of the model the handler sends (no placeholder is left
    /// unfilled).
    /// </summary>
    /// <param name="language">The language under test.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    [InlineData("de")]
    [InlineData("es")]
    public async Task SeededTemplate_InEveryLanguage_PrintsTheModelAsync(string language)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await StaffAlertTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        var renderer = new TemplateRenderer(new FakeEmailBrandingProvider());

        var rendered = await RenderAsync(context, renderer, language);

        rendered.Should().Contain("616161").And.Contain("770077").And.Contain("bank-gateway");
        rendered.Should().NotContain("{{").And.NotContain("{%");

        // The ru, hy, de and es versions are really translated, not a copy of the English text.
        if (language == "ru")
        {
            rendered.Should().MatchRegex("[Ѐ-ӿ]");
        }

        if (language == "hy")
        {
            rendered.Should().MatchRegex("[԰-֏]");
        }

        if (language == "de")
        {
            rendered.Should().Contain("Rechnung");
        }

        if (language == "es")
        {
            rendered.Should().Contain("factura");
        }
    }

    /// <summary>Publishes the event on the host's bus and waits until the bus has handled it and everything it sent.</summary>
    /// <param name="host">The host the handler runs in.</param>
    /// <param name="evt">The event to publish.</param>
    /// <returns>A task that completes once the handling, including the staff mail, is done.</returns>
    private static async Task PublishAndWaitAsync(WebApplicationFactory<Program> host, GatewayPaymentReversedEvent evt)
    {
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(bus => bus.PublishAsync(evt).AsTask()));
    }

    /// <summary>Renders the stored template's subject and body for the model the handler sends and joins them.</summary>
    /// <param name="context">The data context holding the seeded templates.</param>
    /// <param name="renderer">The renderer under test.</param>
    /// <param name="language">The language version.</param>
    /// <returns>The rendered subject followed by the rendered body.</returns>
    private static async Task<string> RenderAsync(IHostpanelDbContext context, TemplateRenderer renderer, string language)
    {
        var template = await context.EmailTemplates.AsNoTracking()
            .FirstAsync(t => t.Slug == StaffAlertTemplatesSeeder.GatewayPaymentReversedSlug && t.Language == language, CancellationToken.None);
        var model = new GatewayPaymentReversedAdminTemplateModel(
            new InvoiceRefTemplateData(616161), new ClientRefTemplateData(770077), "bank-gateway");
        var subject = await renderer.RenderTextAsync(template.Subject, model, CancellationToken.None);
        var body = await renderer.RenderHtmlAsync(template.Body, model, CancellationToken.None);
        return $"{subject}\n{body}";
    }
}
