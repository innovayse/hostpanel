namespace Innovayse.Application.Admin.Integrations.DTOs;

/// <summary>
/// Result of a test-connection request for an integration.
/// </summary>
/// <param name="Success">
/// True when all required fields are configured; otherwise false.
/// </param>
/// <param name="Message">Human-readable explanation of the outcome.</param>
/// <param name="TestedAt">UTC timestamp when the test was performed.</param>
public record IntegrationTestResultDto(
    bool Success,
    string Message,
    DateTimeOffset TestedAt);
