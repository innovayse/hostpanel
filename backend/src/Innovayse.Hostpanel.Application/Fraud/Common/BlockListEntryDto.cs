using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Application.Fraud.Common;

/// <summary>One entry of the block list.</summary>
/// <param name="Id">Entry primary key.</param>
/// <param name="Type">What <paramref name="Value"/> names.</param>
/// <param name="Value">The blocked IP address, range, e-mail domain or country, in its stored spelling.</param>
/// <param name="Note">Why it is blocked, when a note was given.</param>
/// <param name="CreatedAt">When the entry was added (UTC).</param>
public sealed record BlockListEntryDto(int Id, BlockListType Type, string Value, string? Note, DateTimeOffset CreatedAt);
