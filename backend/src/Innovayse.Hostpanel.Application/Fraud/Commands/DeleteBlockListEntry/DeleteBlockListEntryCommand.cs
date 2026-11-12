using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.DeleteBlockListEntry;

/// <summary>Removes an entry from the block list.</summary>
/// <param name="Id">The entry's id; set from the route.</param>
public sealed record DeleteBlockListEntryCommand(int Id) : IDemoRestricted;
