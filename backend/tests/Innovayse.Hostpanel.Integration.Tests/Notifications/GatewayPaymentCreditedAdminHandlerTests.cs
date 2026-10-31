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
/// Tests for <c>GatewayPaymentCreditedAdminHandler</c>: a gateway payment its invoice could not
/// record mails the configured staff address, saying whether the amount went to the client's balance
/// or is held for an operator, and seeds the template on first use.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GatewayPaymentCreditedAdminHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address configured on the test host.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>A credited payment sends one staff mail naming the invoice, the reference and the amount, and the template row exists.</summary>
    [Fact]
    public async Task GatewayPaymentCredited_MailsStaffOnceAndSeedsTheTemplate()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await PublishAndWaitAsync(host, new GatewayPaymentCreditedEvent(616161, 7, "bank-gateway", "ref-credited-1", 4.5m, "USD", Credited: true)
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("#616161");
        mail.Body.Should().Contain("bank-gateway").And.Contain("ref-credited-1").And.Contain("4.5").And.Contain("USD");
        mail.Body.Should().Contain("added to the client").And.NotContain("held for an operator");
        (await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == StaffAlertTemplatesSeeder.GatewayPaymentCreditedSlug))
            .Should().BeTrue();
    }

    /// <summary>A payment held for an operator says so instead of claiming it was credited.</summary>
    [Fact]
    public async Task GatewayPaymentHeld_MailsStaffThatAnOperatorMustResolveIt()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

        await PublishAndWaitAsync(host, new GatewayPaymentCreditedEvent(626262, 7, "bank-gateway", "ref-held-1", 10m, "EUR", Credited: false)
        {
            OccurredAt = DateTimeOffset.UtcNow,
        });

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("#626262");
        mail.Body.Should().Contain("held for an operator").And.NotContain("added to the client");
    }

    /// <summary>With neither address configured nothing is sent (the handler logs an error naming the setting instead).</summary>
    [Fact]
    public async Task GatewayPaymentCredited_WithNoAddress_SendsNothing()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await bus.InvokeAsync(
            new GatewayPaymentCreditedEvent(1, 1, "bank-gateway", "ref-none", 1m, "USD", Credited: true) { OccurredAt = DateTimeOffset.UtcNow },
            CancellationToken.None);

        sender.Sent.Should().BeEmpty();
    }

    /// <summary>
    /// Every language of the seeded staff template prints the values of the model the handler sends
    /// (no placeholder is left unfilled), and says differently whether the amount was credited or held.
    /// </summary>
    /// <param name="language">The language under test.</param>
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task SeededTemplate_InEveryLanguage_PrintsTheModelAndTellsCreditedFromHeldAsync(string language)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await StaffAlertTemplatesSeeder.EnsureSeededAsync(context, CancellationToken.None);
        var renderer = new TemplateRenderer(new FakeEmailBrandingProvider());

        var credited = await RenderAsync(context, renderer, language, credited: true);
        var held = await RenderAsync(context, renderer, language, credited: false);

        foreach (var text in new[] { credited, held })
        {
            text.Should().Contain("616161").And.Contain("77").And.Contain("bank-gateway")
                .And.Contain("ref-seeded-1").And.Contain("12").And.Contain("USD");
            text.Should().NotContain("{{").And.NotContain("{%");
        }

        credited.Should().NotBe(held);
    }

    /// <summary>Publishes the event on the host's bus and waits until the bus has handled it and everything it sent.</summary>
    /// <param name="host">The host the handler runs in.</param>
    /// <param name="evt">The event to publish.</param>
    /// <returns>A task that completes once the handling, including the staff mail, is done.</returns>
    private static async Task PublishAndWaitAsync(WebApplicationFactory<Program> host, GatewayPaymentCreditedEvent evt)
    {
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(bus => bus.PublishAsync(evt).AsTask()));
    }

    /// <summary>Renders the stored template's subject and body for the model the handler sends and joins them.</summary>
    /// <param name="context">The data context holding the seeded templates.</param>
    /// <param name="renderer">The renderer under test.</param>
    /// <param name="language">The language version.</param>
    /// <param name="credited">Whether the model says the amount was credited to the balance.</param>
    /// <returns>The rendered subject followed by the rendered body.</returns>
    private static async Task<string> RenderAsync(
        IHostpanelDbContext context, TemplateRenderer renderer, string language, bool credited)
    {
        var template = await context.EmailTemplates.AsNoTracking()
            .FirstAsync(t => t.Slug == StaffAlertTemplatesSeeder.GatewayPaymentCreditedSlug && t.Language == language, CancellationToken.None);
        var model = new GatewayPaymentCreditedAdminTemplateModel(
            new InvoiceRefTemplateData(616161), new ClientRefTemplateData(77), "bank-gateway", "ref-seeded-1", 12m, "USD", credited);
        var subject = await renderer.RenderTextAsync(template.Subject, model, CancellationToken.None);
        var body = await renderer.RenderHtmlAsync(template.Body, model, CancellationToken.None);
        return $"{subject}\n{body}";
    }
}
