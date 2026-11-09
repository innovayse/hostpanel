using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Models.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Health;

/// <summary>Integration tests for GET /health.</summary>
public sealed class HealthEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Health endpoint returns 200 OK with the <see cref="HealthStatus.Ok"/> status, written as the enum
    /// member name like every enum on the wire.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_Health_Returns200WithStatusOkAsync()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.RootElement.GetProperty("status").GetString().Should().Be(nameof(HealthStatus.Ok));
    }
}
