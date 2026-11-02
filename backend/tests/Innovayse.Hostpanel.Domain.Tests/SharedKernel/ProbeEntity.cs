using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Tests.SharedKernel;

/// <summary>Minimal entity subclass for testing <see cref="BaseEntity"/> behaviour.</summary>
internal sealed class ProbeEntity : BaseEntity
{
    /// <summary>
    /// Sets <see cref="BaseEntity.Id"/> through the protected setter, for the test's arrangement.
    /// </summary>
    /// <param name="id">The id to assign.</param>
    public ProbeEntity(int id)
    {
        Id = id;
    }

    /// <summary>Triggers a domain event.</summary>
    public void DoSomething()
    {
        AddDomainEvent(new TestDomainEvent(Id));
    }

    /// <summary>Raises an event through the deferred overload, so a test can see when it is built.</summary>
    /// <param name="eventFactory">Builds the event; the test counts its calls.</param>
    public void DoSomethingDeferred(Func<IDomainEvent> eventFactory)
    {
        AddDomainEvent(eventFactory);
    }
}
