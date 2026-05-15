namespace Innovayse.Domain.Products;

/// <summary>Classifies a product by the type of service it represents.</summary>
public enum ProductType
{
    /// <summary>Shared web hosting plan.</summary>
    SharedHosting,

    /// <summary>Virtual private server.</summary>
    Vps,

    /// <summary>Dedicated server.</summary>
    Dedicated,

    /// <summary>Domain name registration.</summary>
    Domain,

    /// <summary>SSL certificate.</summary>
    Ssl,

    /// <summary>Other or custom service.</summary>
    Other,
}
