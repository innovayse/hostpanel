namespace Innovayse.Hostpanel.Domain.Migration.Enums;

/// <summary>Why a record pulled from the legacy source was skipped instead of imported.</summary>
public enum MigrationSkipReason
{
    /// <summary>The same record already exists here, so nothing was written.</summary>
    AlreadyExists = 0,

    /// <summary>The client the record belongs to does not exist here.</summary>
    ClientNotFound = 1,

    /// <summary>The account for a client could not be created by the account provider.</summary>
    AccountNotProvisioned = 2,

    /// <summary>The product group the record refers to was not imported.</summary>
    ProductGroupNotFound = 3,

    /// <summary>No product exists here to attach the service to.</summary>
    NoProducts = 4,

    /// <summary>The ticket the reply belongs to does not exist here.</summary>
    TicketNotFound = 5,

    /// <summary>The ticket the reply belongs to is closed and takes no more replies.</summary>
    TicketClosed = 6,

    /// <summary>Importing the record failed with an error; the detail is in the server log only.</summary>
    ImportFailed = 7,

    /// <summary>No product here carries the name the record refers to.</summary>
    ProductNotFound = 8,

    /// <summary>The invoice the record pays was not imported here.</summary>
    InvoiceNotFound = 9,

    /// <summary>The source record carries no date the import needs, such as an issue or due date.</summary>
    DateMissing = 10,

    /// <summary>No department exists here to place the ticket in.</summary>
    DepartmentNotFound = 11,

    /// <summary>No download category exists here to place the file in.</summary>
    DownloadCategoryNotFound = 12,

    /// <summary>The client record has no first name and no email address to take one from.</summary>
    NameMissing = 13,

    /// <summary>The record names a billing cycle this version cannot bill, such as triennial or one-time (monthly, quarterly, semi-annual, annual and biennial are billed).</summary>
    BillingCycleUnsupported = 14,

    /// <summary>Every line of the order is a domain or a service addon, which has no catalogue product here, so the order has nothing to import.</summary>
    NoProductLines = 15,
}
