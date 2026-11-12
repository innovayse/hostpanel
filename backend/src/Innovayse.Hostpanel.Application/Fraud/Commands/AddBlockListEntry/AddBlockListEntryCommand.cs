using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;

namespace Innovayse.Hostpanel.Application.Fraud.Commands.AddBlockListEntry;

/// <summary>Adds an IP address, IP range, e-mail domain or country to the block list.</summary>
/// <param name="Type">What <paramref name="Value"/> names.</param>
/// <param name="Value">The value; it is checked against the type and stored in its canonical spelling.</param>
/// <param name="Note">Why it is blocked, for staff; optional.</param>
public sealed record AddBlockListEntryCommand(BlockListType Type, string Value, string? Note) : IDemoRestricted;
