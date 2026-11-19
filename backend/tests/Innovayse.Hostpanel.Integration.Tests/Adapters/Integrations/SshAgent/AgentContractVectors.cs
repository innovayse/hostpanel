using System.Text.Json.Nodes;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Integrations.SshAgent;

/// <summary>
/// Reads the agent's golden vectors, <c>backend/agent/ssh-key-agent/tests/contract/*.json</c>, which the project links
/// into its output (<c>Contract/</c>). The agent's own tests run the same files against the real agent, so the request
/// and answer shapes HostPanel speaks are pinned from both sides by one source.
/// </summary>
internal static class AgentContractVectors
{
    /// <summary>The folder the vectors are copied to beside the test assembly.</summary>
    private const string Folder = "Contract";

    /// <summary>Loads the vectors of one operation.</summary>
    /// <param name="op">The operation, which is also the file's name, for example <c>ensure-website</c>.</param>
    /// <returns>Every case of the file, in order.</returns>
    /// <exception cref="FileNotFoundException">The vectors were not linked into the output.</exception>
    public static IReadOnlyList<AgentContractCase> Load(string op)
    {
        var path = Path.Combine(AppContext.BaseDirectory, Folder, op + ".json");
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        return document["cases"]!.AsArray()
            .Select(node => node!.AsObject())
            .Select(node => new AgentContractCase(
                node["name"]!.GetValue<string>(),
                node["request"]!.AsObject().DeepClone().AsObject(),
                node["response"]!.AsObject().DeepClone().AsObject()))
            .ToList();
    }

    /// <summary>The indexes of one operation's vectors as theory rows, so a theory runs one vector per row.</summary>
    /// <param name="op">The operation.</param>
    /// <returns>One row per case.</returns>
    public static TheoryData<int> Rows(string op)
    {
        var rows = new TheoryData<int>();
        var cases = Load(op);
        for (var index = 0; index < cases.Count; index++)
        {
            rows.Add(index);
        }

        return rows;
    }
}
