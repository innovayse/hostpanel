namespace Innovayse.Domain.Tests.Common;

using Innovayse.Domain.Common;

/// <summary>Tests for <see cref="AggregateRoot"/> base class.</summary>
public class AggregateRootTests
{
    /// <summary>Aggregate raises domain event added via AddDomainEvent.</summary>
    [Fact]
    public void AddDomainEvent_ShouldAppendEventToList()
    {
        // Arrange
        var aggregate = new TestAggregate(1);

        // Act
        aggregate.DoSomething();

        // Assert
        Assert.Single(aggregate.DomainEvents);
        Assert.IsType<TestEvent>(aggregate.DomainEvents[0]);
    }

    /// <summary>ClearDomainEvents empties the list.</summary>
    [Fact]
    public void ClearDomainEvents_ShouldEmptyTheList()
    {
        var aggregate = new TestAggregate(1);
        aggregate.DoSomething();

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    /// <summary>Minimal aggregate for testing.</summary>
    private sealed class TestAggregate(int id) : AggregateRoot(id)
    {
        /// <summary>Triggers a domain event.</summary>
        public void DoSomething() => AddDomainEvent(new TestEvent(Id));
    }

    /// <summary>Test domain event.</summary>
    private sealed record TestEvent(int AggregateId) : IDomainEvent;
}
