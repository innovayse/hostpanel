using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Rendering;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Tests for <c>PluginChangeEndedAdminHandler</c>: a plugin change the host applied in the background mails the configured
/// staff address with how it ended (loaded and running, or failed with the reason and the restored version, or uninstalled),
/// sends it once however often the event is delivered, and seeds the template on first use.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginChangeEndedAdminHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address configured on the test host.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>A change that loaded mails staff that the new version runs, and the template row exists.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangeApplied_MailsStaffThatTheNewVersionRunsAndSeedsTheTemplateAsync()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        await PublishAndWaitAsync(host, Applied("applied-plugin"));

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("applied-plugin").And.Contain("loaded and running");
        mail.Body.Should().Contain("2.0.0").And.Contain("loaded and running").And.NotContain("could not be loaded");
        (await context.EmailTemplates.AsNoTracking().AnyAsync(t => t.Slug == StaffAlertTemplatesSeeder.PluginChangeEndedSlug))
            .Should().BeTrue();
    }

    /// <summary>A failed change mails staff the reason and the version that was restored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangeFailed_MailsStaffTheReasonAndTheRestoredVersionAsync()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

        await PublishAndWaitAsync(host, Failed("failed-plugin", restoredVersion: "1.0.0"));

        var mail = sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which;
        mail.Subject.Should().Contain("failed-plugin").And.Contain("change failed");
        mail.Body.Should().Contain("entry point missing").And.Contain("1.0.0").And.Contain("restored");
    }

    /// <summary>A failed new install says the plugin is not installed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FreshInstallFailed_MailsStaffThatThePluginIsNotInstalledAsync()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

        await PublishAndWaitAsync(host, Failed("fresh-failed-plugin", restoredVersion: null, fromVersion: null));

        sender.Sent.Should().ContainSingle(m => m.To == AdminEmail).Which.Body.Should().Contain("not installed");
    }

    /// <summary>The same event delivered twice sends one mail, as a start that could not mark the change as reported would.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheSameEventTwice_MailsStaffOnceAsync()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);
        var evt = Applied("twice-plugin");

        await PublishAndWaitAsync(host, evt);
        await PublishAndWaitAsync(host, evt);

        sender.Sent.Should().ContainSingle(m => m.To == AdminEmail);
    }

    /// <summary>With neither address configured nothing is sent (the handler logs an error naming the setting instead).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangeEnded_WithNoAddress_SendsNothingAsync()
    {
        var sender = new FakeEmailSender();
        var host = EventHandlerTestHost.WithFakeRelay(factory, sender);

        await PublishAndWaitAsync(host, Applied("no-address-plugin"));

        sender.Sent.Should().BeEmpty();
    }

    /// <summary>Every language of the seeded template prints the values of the model the handler sends, for both outcomes.</summary>
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

        var applied = await RenderAsync(context, renderer, language, new PluginChangeEndedAdminTemplateModel("model-plugin", "1.0.0", "2.0.0", true, null, null));
        var failed = await RenderAsync(context, renderer, language, new PluginChangeEndedAdminTemplateModel("model-plugin", "1.0.0", "2.0.0", false, "broken-entry", "1.0.0"));

        applied.Should().Contain("model-plugin").And.Contain("2.0.0").And.Contain("1.0.0");
        failed.Should().Contain("model-plugin").And.Contain("broken-entry");
        (applied + failed).Should().NotContain("{{").And.NotContain("{%");
        applied.Should().NotContain("broken-entry");

        // The ru, hy, de and es versions are really translated, not a copy of the English text.
        if (language == "ru")
        {
            applied.Should().MatchRegex("[Ѐ-ӿ]");
        }

        if (language == "hy")
        {
            applied.Should().MatchRegex("[԰-֏]");
        }

        if (language == "de")
        {
            failed.Should().Contain("fehlgeschlagen");
        }

        if (language == "es")
        {
            failed.Should().Contain("fallido");
        }
    }

    /// <summary>Builds the event of a change that loaded.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The event.</returns>
    private static PluginChangeEndedEvent Applied(string pluginId)
    {
        return new PluginChangeEndedEvent(pluginId, "1.0.0", "2.0.0", true, null, null, TestMoments.Seeded);
    }

    /// <summary>Builds the event of a change that could not be loaded.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="restoredVersion">The version that was restored; <see langword="null"/> for a plugin left uninstalled.</param>
    /// <param name="fromVersion">The version that ran before; <see langword="null"/> for a new install.</param>
    /// <returns>The event.</returns>
    private static PluginChangeEndedEvent Failed(string pluginId, string? restoredVersion, string? fromVersion = "1.0.0")
    {
        return new PluginChangeEndedEvent(pluginId, fromVersion, "2.0.0", false, "entry point missing", restoredVersion, TestMoments.Seeded);
    }

    /// <summary>Publishes the event on the host's bus and waits until the bus has handled it and everything it sent.</summary>
    /// <param name="host">The host the handler runs in.</param>
    /// <param name="evt">The event to publish.</param>
    /// <returns>A task that completes once the handling, including the staff mail, is done.</returns>
    private static async Task PublishAndWaitAsync(WebApplicationFactory<Program> host, PluginChangeEndedEvent evt)
    {
        await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(bus => bus.PublishAsync(evt).AsTask()));
    }

    /// <summary>Renders the stored template's subject and body for a model and joins them.</summary>
    /// <param name="context">The data context holding the seeded templates.</param>
    /// <param name="renderer">The renderer under test.</param>
    /// <param name="language">The language version.</param>
    /// <param name="model">The model the handler would send.</param>
    /// <returns>The rendered subject followed by the rendered body.</returns>
    private static async Task<string> RenderAsync(
        IHostpanelDbContext context, TemplateRenderer renderer, string language, PluginChangeEndedAdminTemplateModel model)
    {
        var template = await context.EmailTemplates.AsNoTracking()
            .FirstAsync(t => t.Slug == StaffAlertTemplatesSeeder.PluginChangeEndedSlug && t.Language == language, CancellationToken.None);
        var subject = await renderer.RenderTextAsync(template.Subject, model, CancellationToken.None);
        var body = await renderer.RenderHtmlAsync(template.Body, model, CancellationToken.None);
        return $"{subject}\n{body}";
    }
}
