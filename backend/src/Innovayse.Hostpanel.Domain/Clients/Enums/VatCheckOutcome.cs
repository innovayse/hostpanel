namespace Innovayse.Hostpanel.Domain.Clients.Enums;

/// <summary>How a check of a VAT number against the service that holds the register ended.</summary>
public enum VatCheckOutcome
{
    /// <summary>The service confirmed the number is registered.</summary>
    Valid = 1,

    /// <summary>The service answered and the number is not registered.</summary>
    Invalid = 2,

    /// <summary>The service, or the member state behind it, could not be reached or gave no verdict; nothing is known about the number.</summary>
    Unavailable = 3,
}
