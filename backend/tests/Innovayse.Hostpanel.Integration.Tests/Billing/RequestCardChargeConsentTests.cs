using Innovayse.Hostpanel.Application.Billing.Commands.RequestCardChargeConsent;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="RequestCardChargeConsentCommand"/> through the real <see cref="IMessageBus"/>: the agreement a
/// client gave at checkout is recorded as pending against the gateway intent, once per intent, with where it
/// came from.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RequestCardChargeConsentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The agreement is stored as pending with the current wording version and the request's origin.</summary>
    [Fact]
    public async Task HandleAsync_NewIntent_StoresPendingAgreementAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var intent = NewIntentId();

        var result = await InvokeAsync(new RequestCardChargeConsentCommand(
            clientId, intent, CardConsentSource.PaymentCheckbox, new CardConsentOrigin("203.0.113.9", "TestBrowser/1.0")));

        result.IsSuccess.Should().BeTrue();
        var row = (await RowsAsync(intent)).Should().ContainSingle().Subject;
        row.ClientId.Should().Be(clientId);
        row.Status.Should().Be(CardConsentStatus.Pending);
        row.Source.Should().Be(CardConsentSource.PaymentCheckbox);
        row.IpAddress.Should().Be("203.0.113.9");
        row.UserAgent.Should().Be("TestBrowser/1.0");
        row.TextVersion.Should().Be(Domain.Billing.CardChargeConsent.CurrentTextVersion);
    }

    /// <summary>Asking again for the same intent (a retry) leaves one row.</summary>
    [Fact]
    public async Task HandleAsync_SameIntentTwice_StoresOneRowAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var intent = NewIntentId();
        var cmd = new RequestCardChargeConsentCommand(clientId, intent, CardConsentSource.PaymentCheckbox, new CardConsentOrigin(null, null));

        await InvokeAsync(cmd);
        var again = await InvokeAsync(cmd);

        again.IsSuccess.Should().BeTrue();
        (await RowsAsync(intent)).Should().ContainSingle();
    }

    /// <summary>Parallel requests for one intent still leave one row.</summary>
    [Fact]
    public async Task HandleAsync_ParallelRequests_StoreOneRowAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var intent = NewIntentId();
        var cmd = new RequestCardChargeConsentCommand(clientId, intent, CardConsentSource.PaymentCheckbox, new CardConsentOrigin(null, null));

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => InvokeAsync(cmd))));

        results.Should().OnlyContain(r => r.IsSuccess);
        (await RowsAsync(intent)).Should().ContainSingle();
    }

    /// <summary>A browser string longer than the column is cut, not refused.</summary>
    [Fact]
    public async Task HandleAsync_VeryLongUserAgent_IsTruncatedAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var intent = NewIntentId();

        await InvokeAsync(new RequestCardChargeConsentCommand(
            clientId, intent, CardConsentSource.PaymentCheckbox, new CardConsentOrigin(null, new string('x', 5000))));

        (await RowsAsync(intent)).Single().UserAgent!.Length.Should().Be(512);
    }

    /// <summary>An intent id unique to one test.</summary>
    /// <returns>A new <c>pi_</c> id.</returns>
    private static string NewIntentId() => $"pi_{Guid.NewGuid():N}";

    /// <summary>Reads the agreements recorded for an intent.</summary>
    /// <param name="intent">The gateway intent id.</param>
    /// <returns>The rows.</returns>
    private async Task<List<Domain.Billing.CardChargeConsent>> RowsAsync(string intent)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.CardChargeConsents.AsNoTracking().Where(c => c.GatewayIntentId == intent).ToListAsync();
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="cmd">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(RequestCardChargeConsentCommand cmd)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(cmd);
    }
}
