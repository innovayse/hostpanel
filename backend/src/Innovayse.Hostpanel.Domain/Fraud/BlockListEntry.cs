using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Fraud;

/// <summary>One IP address, IP range, e-mail domain or country the operator blocks. One value is listed once per type.</summary>
public sealed class BlockListEntry : BaseEntity, IAggregateRoot
{
    /// <summary>Longest note the column holds.</summary>
    public const int NoteMaxLength = 500;

    /// <summary>Gets what <see cref="Value"/> names.</summary>
    public BlockListType Type { get; private set; }

    /// <summary>Gets the value in its stored spelling, see <see cref="BlockListValue"/>.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Gets why the value is blocked, for staff.</summary>
    public string? Note { get; private set; }

    /// <summary>Gets when the entry was added (UTC); stamped on save.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private BlockListEntry() { }

    /// <summary>Creates an entry from a value as typed.</summary>
    /// <param name="type">What the value names.</param>
    /// <param name="value">The value as typed; it is checked and normalised.</param>
    /// <param name="note">Why it is blocked, or <see langword="null"/>.</param>
    /// <returns>A new entry.</returns>
    /// <exception cref="DomainRuleException">The value does not fit the type.</exception>
    public static BlockListEntry Create(BlockListType type, string value, string? note)
    {
        if (!BlockListValue.TryNormalise(type, value, out var normalised))
        {
            throw new DomainRuleException(
                FraudErrors.BlockListValueInvalid,
                $"'{value}' is not a valid {type} block list value.");
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        return new BlockListEntry { Type = type, Value = normalised, Note = trimmed };
    }
}
