namespace Innovayse.Domain.Domains;

/// <summary>
/// Result returned by a registrar operation.
/// Wraps success/failure state and relevant registrar data.
/// </summary>
/// <param name="Success">Whether the registrar operation succeeded.</param>
/// <param name="RegistrarRef">Registrar-assigned reference/order ID; null on failure.</param>
/// <param name="ExpiresAt">Domain expiry date returned by the registrar; null when not applicable.</param>
/// <param name="ErrorMessage">Human-readable error description; null on success.</param>
public record RegistrarResult(bool Success, string? RegistrarRef, DateTimeOffset? ExpiresAt, string? ErrorMessage);
