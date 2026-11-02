using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>Tests for <c>CardPaymentLedgerExtensions</c>: what the ledger answers for a gateway reference.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CardPaymentLedgerExtensionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// An empty or blank reference names no payment: the answer is empty without any query. The proof is
    /// a context whose scope is already disposed, which would throw on any read.
    /// </summary>
    /// <param name="reference">The unusable reference.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public async Task RecordedInvoiceIds_WithABlankReference_AnswersEmptyWithoutAQuery(string reference)
    {
        IHostpanelDbContext context;
        using (var scope = factory.Services.CreateScope())
        {
            context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        }

        var recorded = await context.RecordedInvoiceIdsAsync(reference, CancellationToken.None);
        var isRecorded = await context.IsRecordedAsync("bank-gateway", reference, CancellationToken.None);

        recorded.Should().BeEmpty();
        isRecorded.Should().BeFalse();
    }

    /// <summary>
    /// A payment is recorded under the key the unique index uses, the method and the reference together:
    /// the same reference under another method is another payment and is not recorded.
    /// </summary>
    [Fact]
    public async Task IsRecorded_MatchesTheMethodAndTheReferenceTogether()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var reference = $"ref-{Guid.NewGuid():N}";
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.Transactions.Add(Transaction.Create(
            clientId, DateTimeOffset.UtcNow, "Payment", reference, null, "bank-gateway", 10m, 0m, 0m, false, "USD",
            TransactionSource.GatewayPayment));
        await context.SaveChangesAsync(CancellationToken.None);

        var sameKey = await context.IsRecordedAsync("bank-gateway", reference, CancellationToken.None);
        var otherMethod = await context.IsRecordedAsync("other-gateway", reference, CancellationToken.None);
        var otherReference = await context.IsRecordedAsync("bank-gateway", $"ref-{Guid.NewGuid():N}", CancellationToken.None);

        sameKey.Should().BeTrue();
        otherMethod.Should().BeFalse();
        otherReference.Should().BeFalse();
    }
}
