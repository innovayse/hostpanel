using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.Tests.SharedKernel;

/// <summary>Tests for <see cref="BaseEntity"/>: identity equality and the domain-event list.</summary>
public sealed class BaseEntityTests
{
    /// <summary>Aggregate raises domain event added via AddDomainEvent.</summary>
    [Fact]
    public void AddDomainEvent_ShouldAppendEventToList()
    {
        // Arrange
        var probe = new ProbeEntity(1);

        // Act
        probe.DoSomething();

        // Assert
        probe.DomainEvents.Should().ContainSingle();
        probe.DomainEvents.Single().Should().BeOfType<TestDomainEvent>();
    }

    /// <summary>ClearDomainEvents empties the list.</summary>
    [Fact]
    public void ClearDomainEvents_ShouldEmptyTheList()
    {
        // Arrange
        var probe = new ProbeEntity(1);
        probe.DoSomething();

        // Act
        probe.ClearDomainEvents();

        // Assert
        probe.DomainEvents.Should().BeEmpty();
    }

    /// <summary>
    /// HasDomainEvents is false on a fresh entity, true once an event is raised and false again after a clear.
    /// </summary>
    [Fact]
    public void HasDomainEvents_FollowsRaiseAndClear()
    {
        var probe = new ProbeEntity(1);
        probe.HasDomainEvents.Should().BeFalse();

        probe.DoSomething();
        probe.HasDomainEvents.Should().BeTrue();

        probe.ClearDomainEvents();
        probe.HasDomainEvents.Should().BeFalse();
    }

    /// <summary>
    /// HasDomainEvents reports a deferred event without building it, so asking before the insert never
    /// builds an event that would carry id 0.
    /// </summary>
    [Fact]
    public void HasDomainEvents_DoesNotInvokeDeferredFactory()
    {
        var probe = new ProbeEntity(0);
        var builds = 0;
        probe.DoSomethingDeferred(() =>
        {
            builds++;
            return new TestDomainEvent(probe.Id);
        });

        probe.HasDomainEvents.Should().BeTrue();
        builds.Should().Be(0);
    }

    /// <summary>Two entities of one type with the same non-zero id are equal; different types never are.</summary>
    [Fact]
    public void Equals_IsByTypeAndId()
    {
        var a = new ProbeEntity(7);
        var b = new ProbeEntity(7);
        var c = new OtherProbeEntity(7);

        a.Should().Be(b);
        a.Should().NotBe(c);
        (a == b).Should().BeTrue();
    }

    /// <summary>
    /// Unsaved entities (id 0) are equal only to themselves, so a list of new contacts must not collapse to one.
    /// </summary>
    [Fact]
    public void Equals_UnsavedEntitiesAreDistinct()
    {
        var a = new ProbeEntity(0);
        var b = new ProbeEntity(0);

        a.Should().NotBe(b);
        a.Should().Be(a);
    }
}
