namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>The registrar plugin ids the tests configure TLDs and domains with; no plugin by these ids needs to exist.</summary>
public static class TestRegistrarModules
{
    /// <summary>The first registrar a routing test configures.</summary>
    public const string First = "registrar-one";

    /// <summary>The second registrar a routing test configures.</summary>
    public const string Second = "registrar-two";
}
