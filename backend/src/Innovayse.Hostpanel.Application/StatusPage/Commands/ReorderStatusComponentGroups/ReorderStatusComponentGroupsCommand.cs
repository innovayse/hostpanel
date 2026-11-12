namespace Innovayse.Hostpanel.Application.StatusPage.Commands.ReorderStatusComponentGroups;

/// <summary>Puts the status page groups in a new order.</summary>
/// <param name="GroupIds">Every group id, once each, in the order the groups should appear.</param>
public sealed record ReorderStatusComponentGroupsCommand(IReadOnlyList<int> GroupIds);
