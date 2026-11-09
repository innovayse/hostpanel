using Innovayse.Hostpanel.Application.Billing.Common.Enums;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>One log row before it becomes a DTO; both sources project into this shape so they can be concatenated and paged in the database.</summary>
internal sealed class GatewayLogRow
{
    /// <summary>Gets or sets the entry key.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC time of the interaction.</summary>
    public DateTimeOffset Date { get; set; }

    /// <summary>Gets or sets the gateway name.</summary>
    public string Gateway { get; set; } = string.Empty;

    /// <summary>Gets or sets the debug text.</summary>
    public string DebugData { get; set; } = string.Empty;

    /// <summary>Gets or sets the outcome.</summary>
    public GatewayLogResult Result { get; set; }

    /// <summary>Gets or sets the related invoice id.</summary>
    public int? InvoiceId { get; set; }
}
