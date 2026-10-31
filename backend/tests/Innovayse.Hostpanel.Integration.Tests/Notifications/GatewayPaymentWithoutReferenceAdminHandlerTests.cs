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
/// Tests for <c>GatewayPaymentWithoutReferenceAdminHandler</c>: a gateway payment reported as paid
/// with no reference mails the configured staff address, and seeds the template on first use.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GatewayPaymentWithoutReferenceAdminHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address configured on the test host.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>The alert sends one staff mail naming the invoice, the module and the amount, and the template row exists.</summary>
    [Fact]
    public async Task PaymentWithoutReference_MailsStaffOnceAndSeedsTheTemplate()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await PublishAndWaitAsync(host, new GatewayPaymentWithoutReferenceEvent(717171, 7, "bank-gateway", 12.5m, "USD")
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("#717171");
        mail.Body.Should().Contain("bank-gateway").And.Contain("12.5").And.Contain("USD").And.Contain("no payment reference");
        (await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == StaffAlertTemplatesSeeder.GatewayPaymentWithoutReferenceSlug))
            .Should().BeTrue();
    }

    /// <summary>An alert for a gateway that reported no amount still mails staff, without printing an empty amount.</summary>
    [Fact]
    public async Task PaymentWithoutReferenceAndAmount_MailsStaffWithoutAnAmount()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

        await PublishAndWaitAsync(host, new GatewayPaymentWithoutReferenceEvent(727272, 7, "bank-gateway", null, "USD")
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("#727272");
        mail.Body.Should().NotContain("USD");
    }

    /// <summary>With neither address configured nothing is sent (the handler logs an error naming the setting instead).</summary>
    [Fact]
    public async Task PaymentWithoutReference_WithNoAddress_SendsNothing()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.InvokeAsync(
            new GatewayPaymentWithoutReferenceEvent(1, 1, "bank-gateway", 1m, "USD") { OccurredAt = DateTimeOffset.UtcNow },
            CancellationToken.None);

        sender.Sent.Should().BeEmpty();
    }

    /// <summary>
    /// Every language of the seeded staff template prints the values of the model the handler sends
    /// (no placeholder is left unfilled), and leaves the amount out when the gateway reported none.
    /// </summary>
    /// <param name="language">The language under test.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task SeededTemplate_InEveryLanguage_PrintsTheModelAsync(string language)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await StaffAlertTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        var renderer = new TemplateRenderer(new FakeEmailBrandingProvider());

        var withAmount = await RenderAsync(context, renderer, language, 1234.56m);
        var withoutAmount = await RenderAsync(context, renderer, language, null);

        withAmount.Should().Contain("616161").And.Contain("770077").And.Contain("bank-gateway")
            .And.Contain("1234.56").And.Contain("USD");
        withoutAmount.Should().Contain("616161").And.Contain("770077").And.Contain("bank-gateway")
            .And.NotContain("1234.56").And.NotContain("USD");
        withAmount.Should().NotContain("{{").And.NotContain("{%");
        withoutAmount.Should().NotContain("{{").And.NotContain("{%");

        // The ru and hy versions are really translated, not a copy of the English text.
        if (language == "ru")
        {
            withAmount.Should().MatchRegex("[Ѐ-ӿ]");
        }

        if (language == "hy")
        {
            withAmount.Should().MatchRegex("[԰-֏]");
        }
    }

    /// <summary>Publishes the event on the host's bus and waits until the bus has handled it and everything it sent.</summary>
    /// <param name="host">The host the handler runs in.</param>
    /// <param name="evt">The event to publish.</param>
    /// <returns>A task that completes once the handling, including the staff mail, is done.</returns>
    private static async Task PublishAndWaitAsync(WebApplicationFactory<Program> host, GatewayPaymentWithoutReferenceEvent evt)
    {
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(bus => bus.PublishAsync(evt).AsTask()));
    }

    /// <summary>Renders the stored template's subject and body for the model the handler sends and joins them.</summary>
    /// <param name="context">The data context holding the seeded templates.</param>
    /// <param name="renderer">The renderer under test.</param>
    /// <param name="language">The language version.</param>
    /// <param name="amount">The amount the gateway reported, or <see langword="null"/> when it reported none.</param>
    /// <returns>The rendered subject followed by the rendered body.</returns>
    private static async Task<string> RenderAsync(
        IHostpanelDbContext context, TemplateRenderer renderer, string language, decimal? amount)
    {
        var template = await context.EmailTemplates.AsNoTracking()
            .FirstAsync(t => t.Slug == StaffAlertTemplatesSeeder.GatewayPaymentWithoutReferenceSlug && t.Language == language, CancellationToken.None);
        var model = new GatewayPaymentWithoutReferenceAdminTemplateModel(
            new InvoiceRefTemplateData(616161), new ClientRefTemplateData(770077), "bank-gateway", amount, "USD");
        var subject = await renderer.RenderTextAsync(template.Subject, model, CancellationToken.None);
        var body = await renderer.RenderHtmlAsync(template.Body, model, CancellationToken.None);
        return $"{subject}\n{body}";
    }
}
