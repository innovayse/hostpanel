using System.Text.Json.Nodes;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.SshAgent;

/// <summary>One golden vector of the agent contract: what HostPanel sends and what the agent answers to it.</summary>
/// <param name="Name">What the vector shows, for the test's display name.</param>
/// <param name="Request">The request document, version included.</param>
/// <param name="Response">The agent's answer, version included.</param>
internal sealed record AgentContractCase(string Name, JsonObject Request, JsonObject Response)
{
    /// <summary>Prints the vector's name, so a failing theory row says which vector it is.</summary>
    /// <returns>The name.</returns>
    public override string ToString()
    {
        return Name;
    }
}
