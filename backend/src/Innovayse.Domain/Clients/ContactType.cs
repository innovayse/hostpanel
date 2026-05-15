namespace Innovayse.Domain.Clients;

/// <summary>Classification of an additional contact on a client account.</summary>
public enum ContactType
{
    /// <summary>Receives billing and invoice emails.</summary>
    Billing,

    /// <summary>Receives technical and service notifications.</summary>
    Technical,

    /// <summary>General purpose contact.</summary>
    General
}
