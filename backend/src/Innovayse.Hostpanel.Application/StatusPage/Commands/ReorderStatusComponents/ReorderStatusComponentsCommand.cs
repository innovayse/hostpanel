namespace Innovayse.Hostpanel.Application.StatusPage.Commands.ReorderStatusComponents;

/// <summary>Puts the components of one status page group in a new order.</summary>
/// <param name="GroupId">
/// The group whose components are reordered. A value type, so it is left non-nullable: a missing <c>int</c> in the body
/// binds to <c>0</c> and the validator refuses it.
/// </param>
/// <param name="ComponentIds">Every component id of the group, once each, in the order the components should appear.</param>
public sealed record ReorderStatusComponentsCommand(int GroupId, IReadOnlyList<int> ComponentIds);
