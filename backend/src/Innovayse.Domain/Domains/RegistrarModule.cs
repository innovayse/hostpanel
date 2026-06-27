namespace Innovayse.Domain.Domains;

/// <summary>
/// Identifies the registrar integration module used for domain operations.
/// Each value maps to a concrete <see cref="Interfaces.IRegistrarProvider"/> implementation.
/// </summary>
public enum RegistrarModule
{
    /// <summary>The name.am registrar module (Armenian TLDs).</summary>
    NameAm,

    /// <summary>The Namecheap registrar module (generic and country-code TLDs).</summary>
    Namecheap
}
