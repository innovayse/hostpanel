using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Clients.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Notifications.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Notifications;

/// <summary>
/// Integration tests for the <c>SendEmail</c> use case. There is no HTTP route for this command:
/// every caller is an internal event handler, fire-and-forget, so each test dispatches
/// <see cref="SendEmailCommand"/> straight through <see cref="IMessageBus"/>, exactly as
/// the notification handler tests do; those tests exercise the handlers that call it.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SendEmailTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The typed data every test sends; its templates print <c>{{ first_name }}</c>.</summary>
    private static readonly UserInviteTemplateModel AdaInvite = new("https://panel.example/invite", "Acme Ltd", "Ada");

    /// <summary>
    /// Builds a host of its own with the SMTP relay replaced by <paramref name="emailSender"/>, so
    /// the fake this test reads back is the one the command actually wrote to.
    /// </summary>
    /// <param name="emailSender">The fake relay this host's <see cref="SendEmailCommand"/> will use.</param>
    /// <returns>The isolated host.</returns>
    private WebApplicationFactory<Program> HostWithFakeRelay(FakeEmailSender emailSender) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(emailSender)));

    /// <summary>A known, active template renders and sends, and the attempt is logged as successful.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_KnownActiveTemplate_SendsAndLogsSuccess()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = $"send-email-{Guid.NewGuid():N}";
        await NotificationsTestSeeds.EnsureTemplateAsync(factory, slug, "Hello {{first_name}}");
        var to = $"send-email-{Guid.NewGuid():N}@example.test";

        var outcome = await bus.InvokeAsync<Innovayse.Hostpanel.Shared.Models.Result>(
            new SendEmailCommand(to, null, slug, AdaInvite), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        Assert.Single(emailSender.Sent, m => m.To == to);
        var log = await context.EmailLogs.AsNoTracking().FirstAsync(l => l.To == to);
        Assert.True(log.Success);
    }

    /// <summary>A slug naming no template refuses with <c>EMAIL_TEMPLATE_NOT_FOUND</c>, and nothing is sent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_UnknownTemplateSlug_RefusesNotFound()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Innovayse.Hostpanel.Shared.Models.Result>(
            new SendEmailCommand($"nobody-{Guid.NewGuid():N}@example.test", null, $"missing-{Guid.NewGuid():N}", AdaInvite),
            CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(
            Innovayse.Hostpanel.Application.Common.Errors.HostpanelErrorCodes.EMAIL_TEMPLATE_NOT_FOUND);
        Assert.Empty(emailSender.Sent);
    }

    /// <summary>An inactive template is not sent, but the command still answers success.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_InactiveTemplate_SucceedsWithoutSending()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var slug = $"send-email-inactive-{Guid.NewGuid():N}";
        await NotificationsTestSeeds.EnsureTemplateAsync(factory, slug, "Hello {{first_name}}", isActive: false);
        var to = $"send-email-{Guid.NewGuid():N}@example.test";

        var outcome = await bus.InvokeAsync<Innovayse.Hostpanel.Shared.Models.Result>(
            new SendEmailCommand(to, null, slug, AdaInvite), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        Assert.Empty(emailSender.Sent);
    }

    /// <summary>A recipient whose language is Russian gets the Russian version of the template, not the default one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_RussianRecipient_SendsTheRussianVersion()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var slug = $"send-email-ru-{Guid.NewGuid():N}";
        await SeedVersionsAsync(scope, slug, ("en", "Hello {{first_name}}"), ("ru", "Привет, {{first_name}}"));
        var to = $"send-email-{Guid.NewGuid():N}@example.test";

        var outcome = await bus.InvokeAsync<Innovayse.Hostpanel.Shared.Models.Result>(
            new SendEmailCommand(to, "ru", slug, AdaInvite), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle(m => m.To == to)
            .Which.Subject.Should().Be("Привет, Ada");
    }

    /// <summary>A recipient whose language has no version yet gets the default language's version instead of nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_MissingTranslation_FallsBackToTheDefaultLanguage()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var slug = $"send-email-fallback-{Guid.NewGuid():N}";
        await SeedVersionsAsync(scope, slug, ("en", "Hello {{first_name}}"), ("ru", "Привет, {{first_name}}"));
        var to = $"send-email-{Guid.NewGuid():N}@example.test";

        var outcome = await bus.InvokeAsync<Innovayse.Hostpanel.Shared.Models.Result>(
            new SendEmailCommand(to, "hy", slug, AdaInvite), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle(m => m.To == to)
            .Which.Subject.Should().Be("Hello Ada");
    }

    /// <summary>
    /// A relay that refuses the message is logged as failed with the classified reason, and the
    /// command still answers success because every caller fires it and forgets it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_RelayRefuses_LogsTheClassifiedFailure()
    {
        var emailSender = new FakeEmailSender { FailWith = EmailSendFailure.AuthenticationRejected };
        var host = HostWithFakeRelay(emailSender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = $"send-email-refused-{Guid.NewGuid():N}";
        await NotificationsTestSeeds.EnsureTemplateAsync(factory, slug, "Hello {{first_name}}");
        var to = $"send-email-{Guid.NewGuid():N}@example.test";

        var outcome = await bus.InvokeAsync<Innovayse.Hostpanel.Shared.Models.Result>(
            new SendEmailCommand(to, null, slug, AdaInvite), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        var log = await context.EmailLogs.AsNoTracking().FirstAsync(l => l.To == to);
        log.Success.Should().BeFalse();
        log.Failure.Should().Be(EmailSendFailure.AuthenticationRejected);
    }

    /// <summary>Stores one active version per language of a fresh template slug.</summary>
    /// <param name="scope">The test host scope whose data context to write through.</param>
    /// <param name="slug">The template slug.</param>
    /// <param name="versions">The language code and subject of each version.</param>
    /// <returns>A task that completes once the versions are stored.</returns>
    private static async Task SeedVersionsAsync(
        IServiceScope scope, string slug, params (string Language, string Subject)[] versions)
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var (language, subject) in versions)
        {
            context.EmailTemplates.Add(EmailTemplate.Create(slug, language, subject, "Body", null));
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// A mail sent with a contact copy also reaches the contacts who switched that kind of email on,
    /// and only them: a contact who switched it off, and the recipient's own address, get nothing extra.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_WithContactCopy_AlsoMailsTheContactsWhoWantThatKind()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var slug = $"send-email-copy-{Guid.NewGuid():N}";
        await NotificationsTestSeeds.EnsureTemplateAsync(factory, slug, "Hello {{first_name}}");
        var (clientId, _) = await ClientsTestSeeds.SeedClientAsync(factory);
        var admin = await ClientsTestSeeds.AdminClientAsync(factory);
        var to = $"send-email-copy-{Guid.NewGuid():N}@example.test";
        var wanting = $"wants-{Guid.NewGuid():N}@example.test";
        var declining = $"declines-{Guid.NewGuid():N}@example.test";
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Wants", wanting, true))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Declines", declining, false))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/clients/{clientId}/contacts", ClientsTestSeeds.ContactBody("Same", to.ToUpperInvariant(), true))).EnsureSuccessStatusCode();

        var outcome = await bus.InvokeAsync<Innovayse.Hostpanel.Shared.Models.Result>(
            new SendEmailCommand(to, null, slug, AdaInvite, new ContactCopy(clientId, ContactNotification.Invoice)), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        emailSender.Sent.Select(m => m.To).Should().BeEquivalentTo(new[] { to, wanting });
        (await context.EmailLogs.AsNoTracking().CountAsync(l => l.To == wanting)).Should().Be(1);
        (await context.EmailLogs.AsNoTracking().CountAsync(l => l.To == declining)).Should().Be(0);
    }

    /// <summary>A keyed send replayed with the same key reaches the relay once and leaves one Sent row.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_KeyedReplay_SendsOnce()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        var (slug, to, key) = await KeyedScenarioAsync();
        var command = new SendEmailCommand(to, null, slug, AdaInvite, IdempotencyKey: key);

        await InvokeInNewScopeAsync(host, command);
        var replay = await InvokeInNewScopeAsync(host, command);

        replay.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle(m => m.To == to);
        var rows = await LogsForAsync(host, to);
        rows.Should().ContainSingle().Which.State.Should().Be(EmailDeliveryState.Sent);
        rows[0].IdempotencyKey.Should().Be(EmailIdempotencyKey.ForRecipient(key, to));
    }

    /// <summary>Two deliveries of the same keyed command running at once send the mail once between them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_TwoConcurrentKeyedSends_SendOnce()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        var (slug, to, key) = await KeyedScenarioAsync();
        var command = new SendEmailCommand(to, null, slug, AdaInvite, IdempotencyKey: key);

        var outcomes = await Task.WhenAll(InvokeInNewScopeAsync(host, command), InvokeInNewScopeAsync(host, command));

        outcomes.Should().OnlyContain(o => o.IsSuccess);
        emailSender.Sent.Count(m => m.To == to).Should().Be(1);
        (await LogsForAsync(host, to)).Should().ContainSingle();
    }

    /// <summary>A keyed mail the relay refused is sent again on replay, on the same row.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_FailedKeyedRowOnReplay_IsResent()
    {
        var refusing = HostWithFakeRelay(new FakeEmailSender { FailWith = EmailSendFailure.AuthenticationRejected });
        var emailSender = new FakeEmailSender();
        var accepting = HostWithFakeRelay(emailSender);
        var (slug, to, key) = await KeyedScenarioAsync();
        var command = new SendEmailCommand(to, null, slug, AdaInvite, IdempotencyKey: key);

        await InvokeInNewScopeAsync(refusing, command);
        (await LogsForAsync(accepting, to)).Should().ContainSingle().Which.State.Should().Be(EmailDeliveryState.Failed);
        await InvokeInNewScopeAsync(accepting, command);

        emailSender.Sent.Should().ContainSingle(m => m.To == to);
        var row = (await LogsForAsync(accepting, to)).Should().ContainSingle().Subject;
        row.State.Should().Be(EmailDeliveryState.Sent);
        row.Success.Should().BeTrue();
        row.Failure.Should().BeNull();
    }

    /// <summary>
    /// A row another delivery claimed inside the lease is left to it and nothing is sent; one claimed
    /// longer ago than the lease (a sender that crashed before saving its outcome) is sent again.
    /// </summary>
    /// <param name="outsideLease">Whether the existing claim is older than the configured lease.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_SendingRow_IsResentOnlyOutsideTheLease(bool outsideLease)
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        var (slug, to, key) = await KeyedScenarioAsync();
        var lease = host.Services.GetRequiredService<IOptions<NotificationOptions>>().Value.DeliveryClaimLease;
        var claimedAt = outsideLease
            ? DateTimeOffset.UtcNow - lease - TimeSpan.FromMinutes(1)
            : DateTimeOffset.UtcNow;
        using (var seedScope = host.Services.CreateScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            seedContext.EmailLogs.Add(
                EmailLog.Claim(to, "Hello Ada", "Body", EmailIdempotencyKey.ForRecipient(key, to), claimedAt));
            await seedContext.SaveChangesAsync();
        }

        var outcome = await InvokeInNewScopeAsync(
            host, new SendEmailCommand(to, null, slug, AdaInvite, IdempotencyKey: key));

        outcome.IsSuccess.Should().BeTrue();
        emailSender.Sent.Count(m => m.To == to).Should().Be(outsideLease ? 1 : 0);
        var row = (await LogsForAsync(host, to)).Should().ContainSingle().Subject;
        row.State.Should().Be(outsideLease ? EmailDeliveryState.Sent : EmailDeliveryState.Sending);
    }

    /// <summary>An unkeyed command sent twice is two mails and two log rows, exactly as before keys existed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_UnkeyedTwice_SendsTwice()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        var (slug, to, _) = await KeyedScenarioAsync();
        var command = new SendEmailCommand(to, null, slug, AdaInvite);

        await InvokeInNewScopeAsync(host, command);
        await InvokeInNewScopeAsync(host, command);

        emailSender.Sent.Count(m => m.To == to).Should().Be(2);
        var rows = await LogsForAsync(host, to);
        rows.Should().HaveCount(2).And.OnlyContain(l => l.IdempotencyKey == null && l.State == EmailDeliveryState.Sent);
    }

    /// <summary>A key that is not 64 lower-case hex characters is refused before anything is sent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Send_MalformedKey_IsRefusedByValidation()
    {
        var emailSender = new FakeEmailSender();
        var host = HostWithFakeRelay(emailSender);
        var (slug, to, key) = await KeyedScenarioAsync();

        var act = () => InvokeInNewScopeAsync(
            host, new SendEmailCommand(to, null, slug, AdaInvite, IdempotencyKey: key.ToUpperInvariant()));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        emailSender.Sent.Should().BeEmpty();
    }

    /// <summary>Seeds a fresh active template and picks a fresh recipient and message key for one keyed test.</summary>
    /// <returns>The template slug, the recipient address and the message key.</returns>
    private async Task<(string Slug, string To, string Key)> KeyedScenarioAsync()
    {
        var slug = $"send-email-keyed-{Guid.NewGuid():N}";
        await NotificationsTestSeeds.EnsureTemplateAsync(factory, slug, "Hello {{first_name}}");
        var to = $"send-email-keyed-{Guid.NewGuid():N}@example.test";
        return (slug, to, EmailIdempotencyKey.ForParts(slug, to));
    }

    /// <summary>Dispatches <paramref name="command"/> in a scope of its own, as one delivery would.</summary>
    /// <param name="host">The host whose bus to use.</param>
    /// <param name="command">The command to dispatch.</param>
    /// <returns>The handler's answer.</returns>
    private static async Task<Innovayse.Hostpanel.Shared.Models.Result> InvokeInNewScopeAsync(
        WebApplicationFactory<Program> host, SendEmailCommand command)
    {
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Innovayse.Hostpanel.Shared.Models.Result>(command, CancellationToken.None);
    }

    /// <summary>Reads every log row written for one recipient, untracked.</summary>
    /// <param name="host">The host whose database to read.</param>
    /// <param name="to">The recipient address.</param>
    /// <returns>The rows, oldest first.</returns>
    private static async Task<List<EmailLog>> LogsForAsync(WebApplicationFactory<Program> host, string to)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.EmailLogs.AsNoTracking().Where(l => l.To == to).OrderBy(l => l.Id).ToListAsync();
    }
}
