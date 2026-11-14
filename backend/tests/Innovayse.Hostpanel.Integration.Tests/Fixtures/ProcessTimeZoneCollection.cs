namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The tests that change the time zone of the whole test process. A collection that disables parallelization runs alone,
/// so no other test reads a clock or parses a date while the zone is not the one the machine has.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessTimeZoneCollection
{
    /// <summary>The name a test class puts in <see cref="CollectionAttribute"/> to join the collection.</summary>
    public const string Name = "ProcessTimeZone";
}
