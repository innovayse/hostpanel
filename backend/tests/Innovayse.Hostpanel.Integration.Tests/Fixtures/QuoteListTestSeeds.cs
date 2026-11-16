using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builders for the quote list tests: quotes seeded straight into the database with a chosen subject, stage, expiry and
/// creation moment, so a test can tell the stages and the valid from the expired apart.
/// </summary>
public static class QuoteListTestSeeds
{
    /// <summary>
    /// An expiry date ahead of any real clock. The shared host judges a quote's validity against its own system clock, so
    /// a quote meant to be valid is given a date that clock does not reach while the suite runs.
    /// </summary>
    public static DateTimeOffset ExpiresAhead { get; } = TestMoments.Seeded.AddYears(100);

    /// <summary>An expiry date behind any real clock: a quote given it has expired for the shared host.</summary>
    public static DateTimeOffset ExpiredBefore { get; } = TestMoments.Seeded.AddDays(-2);

    /// <summary>Seeds one quote of one line for a client.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="clientId">The quoted client.</param>
    /// <param name="subject">The quote's subject.</param>
    /// <param name="expiryDate">When the quote stops being valid (UTC).</param>
    /// <param name="stage">The quote's stage.</param>
    /// <param name="createdAt">The moment the quote was created (UTC); now when not given.</param>
    /// <returns>The new quote id.</returns>
    public static async Task<int> SeedAsync(
        WebApplicationFactory<Program> host,
        int clientId,
        string subject,
        DateTimeOffset expiryDate,
        QuoteStage stage = QuoteStage.Draft,
        DateTimeOffset? createdAt = null)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var quote = Quote.Create(clientId, subject, expiryDate, "USD");
        quote.AddItem("Design", 10m, 1);
        quote.UpdateDetails(subject, stage, expiryDate, null);
        quote.Backdate(createdAt ?? TestMoments.Seeded);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync(CancellationToken.None);
        return quote.Id;
    }
}
