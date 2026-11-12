using Innovayse.Hostpanel.Domain.Fraud;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.Fraud;

/// <summary>Unit tests for the <see cref="BlockListEntry"/> aggregate.</summary>
public sealed class BlockListEntryTests
{
    /// <summary>Create stores the normalised value and a trimmed note.</summary>
    [Fact]
    public void Create_StoresTheNormalisedValueAndTrimmedNote()
    {
        var entry = BlockListEntry.Create(BlockListType.EmailDomain, "@Mailinator.com", "  disposable  ");

        entry.Type.Should().Be(BlockListType.EmailDomain);
        entry.Value.Should().Be("mailinator.com");
        entry.Note.Should().Be("disposable");
    }

    /// <summary>A blank note is stored as no note.</summary>
    [Fact]
    public void Create_BlankNote_IsStoredAsNull()
    {
        BlockListEntry.Create(BlockListType.Country, "ru", "   ").Note.Should().BeNull();
    }

    /// <summary>A value that does not fit the type is refused with <c>FRAUD_BLOCKLIST_VALUE_INVALID</c>.</summary>
    [Fact]
    public void Create_InvalidValue_ThrowsWithBlockListValueInvalid()
    {
        var act = () => BlockListEntry.Create(BlockListType.Cidr, "203.0.113.9", null);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(FraudErrors.BlockListValueInvalid);
    }
}
