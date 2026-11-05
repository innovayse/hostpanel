namespace Innovayse.Hostpanel.Application.Migration.Importers.Records;

/// <summary>One client service as the source plugin describes it.</summary>
/// <param name="ExternalId">The service's own id in the source; together with the client it is the duplicate check. Null from a source that sends none, and the importer then matches by domain or username.</param>
/// <param name="ClientEmail">Email of the owning client in the source; the importer finds the local client by it and skips the record when none matches.</param>
/// <param name="ProductName">The product's name in the source; the importer matches the local product by it.</param>
/// <param name="BillingCycle">The source's billing cycle word (monthly, annually and so on), mapped to a local cycle.</param>
/// <param name="Status">The source's status word for the record, mapped to a local status by the importer's source-value table.</param>
/// <param name="Domain">The domain name the line or service is for; null or empty when none.</param>
/// <param name="Username">Hosting account user name on the server; null when none.</param>
/// <param name="NextDueDate">When the next renewal invoice falls due.</param>
/// <param name="TerminatedAt">When the service was terminated; null while it is not.</param>
/// <param name="SubscriptionId">The gateway subscription reference; null when the source has none.</param>
/// <param name="AdminNotes">Staff-only notes, carried over as they are.</param>
/// <param name="RecurringAmount">Amount billed each renewal, in the client's currency.</param>
/// <param name="FirstPaymentAmount">Amount billed on the first invoice, in the client's currency.</param>
/// <param name="PaymentMethod">The source's gateway name for how it is paid; mapped to a local gateway where one matches.</param>
/// <param name="RegisteredAt">When the service was registered in the source; anchors its billing period. Null when the source has none.</param>
internal sealed record ServiceRecord(
    string ClientEmail, string ProductName, string BillingCycle, string Status,
    string? Domain, string? Username, DateTimeOffset? NextDueDate,
    DateTimeOffset? TerminatedAt, string? SubscriptionId, string? AdminNotes,
    decimal RecurringAmount, decimal FirstPaymentAmount, string? PaymentMethod,
    DateTimeOffset? RegisteredAt,
    string? ExternalId = null);
