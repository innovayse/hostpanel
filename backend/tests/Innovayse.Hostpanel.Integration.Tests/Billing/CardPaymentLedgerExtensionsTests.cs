using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
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
        var isRecorded = await context.IsRecordedAsync(reference, CancellationToken.None);

        recorded.Should().BeEmpty();
        isRecorded.Should().BeFalse();
    }
}
