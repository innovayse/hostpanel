namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The tests that change a global billing setting the tax decision reads (the automatic tax switch, the seller's country).
/// Classes in one collection run one after another, so none of them reads a setting while another has it turned the other way.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TaxSettingsCollection
{
    /// <summary>The name a test class puts in <see cref="CollectionAttribute"/> to join the collection.</summary>
    public const string Name = "TaxSettings";
}
