using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Demo;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The demo reset also forgets what visitors did with cards: the saved methods and charges of the demo payment plugin
/// and the card intents of the demo gateway, so the demo comes back as seeded.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DemoCardStateResetTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A saved method, a charge and an opened intent are gone after a reset.</summary>
    [Fact]
    public async Task Reset_ForgetsSavedMethodsChargesAndIntentsAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        using var scope = host.Services.CreateScope();
        var credentials = scope.ServiceProvider.GetRequiredService<DemoStoredCredentialState>();
        var gateway = scope.ServiceProvider.GetRequiredService<ICardPaymentGateway>();
        var saved = credentials.CompleteSession(credentials.OpenSession("cust-reset"))!;
        credentials.Record("cust-reset", "key-reset", new StoredChargeResult(Innovayse.SDK.Plugins.Enums.StoredChargeOutcome.Succeeded, "txn", 1000, "USD", null));
        var intent = await gateway.CreatePaymentIntentAsync(new CardPaymentIntentInstruction(10m, "USD", 1, 2, 3, "key"), CancellationToken.None);
        credentials.Methods("cust-reset").Should().ContainSingle(m => m.MethodRef == saved.MethodRef);

        await scope.ServiceProvider.GetRequiredService<IDemoDataManager>().ResetAsync(CancellationToken.None);

        credentials.Methods("cust-reset").Should().BeEmpty();
        credentials.ChargeCount.Should().Be(0);
        credentials.Find("cust-reset", "key-reset").Should().BeNull();
        (await gateway.VerifyPaymentIntentAsync(new CardPaymentVerifyInstruction(intent.ClientSecret!), CancellationToken.None)).Succeeded.Should().BeFalse();
    }
}
