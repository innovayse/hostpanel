using Innovayse.Hostpanel.Application.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// Pins <see cref="INumberSequence"/>: callers asking at the same moment, each on its own
/// connection, are all handed a different number.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class NumberSequenceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Many concurrent callers receive distinct, increasing-per-caller numbers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConcurrentCallersReceiveDistinctNumbers()
    {
        // Arrange
        const int Callers = 40;

        // Act
        var numbers = await Task.WhenAll(Enumerable.Range(0, Callers).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<INumberSequence>()
                .NextAsync(NumberKind.Order, CancellationToken.None);
        }));

        // Assert
        numbers.Should().OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(n => n > 0);
    }

    /// <summary>A later call never receives a number an earlier one already did.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SuccessiveCallsAdvance()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var sequence = scope.ServiceProvider.GetRequiredService<INumberSequence>();

        // Act
        var first = await sequence.NextAsync(NumberKind.Order, CancellationToken.None);
        var second = await sequence.NextAsync(NumberKind.Order, CancellationToken.None);

        // Assert
        second.Should().BeGreaterThan(first);
    }

    /// <summary>Many concurrent callers of the credit note counter receive distinct numbers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ConcurrentCreditNoteCallersReceiveDistinctNumbers()
    {
        const int Callers = 40;

        var numbers = await Task.WhenAll(Enumerable.Range(0, Callers).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<INumberSequence>()
                .NextAsync(NumberKind.CreditNote, CancellationToken.None);
        }));

        numbers.Should().OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(n => n > 0);
    }

    /// <summary>
    /// A value that was taken and not used is not handed out again. This is the property of a database sequence the credit note
    /// numbering relies on, and the reason the numbering cannot promise there are no gaps when a caller stops after taking one.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ATakenCreditNoteNumberIsNeverHandedOutAgain()
    {
        using var scope = factory.Services.CreateScope();
        var sequence = scope.ServiceProvider.GetRequiredService<INumberSequence>();
        var abandoned = await sequence.NextAsync(NumberKind.CreditNote, CancellationToken.None);

        var next = await sequence.NextAsync(NumberKind.CreditNote, CancellationToken.None);

        next.Should().BeGreaterThan(abandoned);
    }
}
