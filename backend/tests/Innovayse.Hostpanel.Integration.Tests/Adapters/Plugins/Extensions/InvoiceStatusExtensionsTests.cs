using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins.Extensions;

/// <summary>
/// <see cref="InvoiceStatusExtensions"/>: every client-visible invoice status maps to the SDK status of the same name (the
/// numbers differ, so a cast would be wrong), while a draft, which the host never shares, and an unknown value fail loudly.
/// </summary>
public sealed class InvoiceStatusExtensionsTests
{
    /// <summary>The invoice statuses a plugin may see: every defined value except a draft.</summary>
    private static readonly InvoiceStatus[] ClientVisibleStatuses =
        Enum.GetValues<InvoiceStatus>().Where(s => s != InvoiceStatus.Draft).ToArray();

    /// <summary>Every client-visible invoice status maps to the plugin status with the same name.</summary>
    [Fact]
    public void InvoiceStatuses_MapByName()
    {
        foreach (var status in ClientVisibleStatuses)
        {
            status.ToPlugin().ToString().Should().Be(status.ToString());
        }
    }

    /// <summary>No two client-visible invoice statuses map to the same plugin status.</summary>
    [Fact]
    public void InvoiceMapping_IsInjective()
    {
        ClientVisibleStatuses.Select(s => s.ToPlugin()).Distinct().Should().HaveCount(ClientVisibleStatuses.Length);
    }

    /// <summary>A draft has no arm and throws: drafts are filtered out before mapping, so one reaching it is refused.</summary>
    [Fact]
    public void DraftInvoiceStatus_Throws()
    {
        var act = () => InvoiceStatus.Draft.ToPlugin();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>An undefined invoice status throws instead of being passed on.</summary>
    [Fact]
    public void UndefinedInvoiceStatus_Throws()
    {
        var act = () => ((InvoiceStatus)999).ToPlugin();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
