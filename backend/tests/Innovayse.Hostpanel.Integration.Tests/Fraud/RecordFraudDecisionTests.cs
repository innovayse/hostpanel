using FluentValidation;
using Innovayse.Hostpanel.Application.Fraud.Commands.RecordFraudDecision;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Fraud;

/// <summary>Recording a decision against Postgres: the log row it writes and the validator's refusals. The use case has no route of its own.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RecordFraudDecisionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The decision, its facts and its reasons are stored, with the moment stamped on save.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WritesTheDecisionToTheLog()
    {
        var id = await FraudTestSeeds.RecordAsync(factory, new RecordFraudDecisionCommand(
            FraudCheckKind.Order, FraudDecision.Review, 5, 9, "203.0.113.50", "Buyer@Example.com", 64,
            [FraudReasonCode.DisposableEmail, FraudReasonCode.CountryMismatch]));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await db.FraudLogEntries.AsNoTracking().SingleAsync(e => e.Id == id);
        entry.Kind.Should().Be(FraudCheckKind.Order);
        entry.Decision.Should().Be(FraudDecision.Review);
        entry.ClientId.Should().Be(5);
        entry.OrderId.Should().Be(9);
        entry.IpAddress.Should().Be("203.0.113.50");
        entry.Email.Should().Be("buyer@example.com");
        entry.Score.Should().Be(64);
        entry.Reasons.Should().Equal(FraudReasonCode.DisposableEmail, FraudReasonCode.CountryMismatch);
        entry.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>A score outside 0 to 100 is refused on the bus, and nothing is written.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARefusedScoreWritesNothing()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.FraudLogEntries.CountAsync();

        var act = () => bus.InvokeAsync<Result<int>>(
            new RecordFraudDecisionCommand(FraudCheckKind.Order, FraudDecision.Approved, null, null, null, null, 101, []));

        await act.Should().ThrowAsync<ValidationException>();
        (await db.FraudLogEntries.CountAsync()).Should().Be(before);
    }
}
