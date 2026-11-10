using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins.Extensions;

/// <summary><see cref="PluginHostInvoiceDtoExtensions"/>: a client-visible invoice reaches a plugin with every value unchanged.</summary>
public sealed class PluginHostInvoiceDtoExtensionsTests
{
    /// <summary>Every column is copied and the status is translated by name.</summary>
    [Fact]
    public void ToPlugin_CopiesEveryValue()
    {
        var issued = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var due = issued.AddDays(14);
        var paid = issued.AddDays(3);
        var dto = new PluginHostInvoiceDto(9, InvoiceStatus.Paid, 120.50m, "USD", issued, due, paid);

        var result = dto.ToPlugin();

        result.Should().Be(new PluginClientInvoice(9, PluginInvoiceStatus.Paid, 120.50m, "USD", issued, due, paid));
    }

    /// <summary>An invoice that is not paid keeps its payment moment as null.</summary>
    [Fact]
    public void ToPlugin_UnpaidKeepsNullPaidAt()
    {
        var dto = new PluginHostInvoiceDto(1, InvoiceStatus.Unpaid, 1m, "EUR", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);

        var result = dto.ToPlugin();

        result.PaidAt.Should().BeNull();
    }

    /// <summary>A draft invoice throws: the host filters drafts out before mapping, so one reaching it is refused.</summary>
    [Fact]
    public void ToPlugin_Draft_Throws()
    {
        var dto = new PluginHostInvoiceDto(1, InvoiceStatus.Draft, 1m, "EUR", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null);

        var act = () => dto.ToPlugin();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
