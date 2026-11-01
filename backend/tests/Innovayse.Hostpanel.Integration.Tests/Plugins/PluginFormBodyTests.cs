using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// A urlencoded or multipart body on a plugin route: MVC would read the whole form before the action runs, past the
/// cap and leaving the plugin an empty body, so the plugin doors bind nothing from the form and
/// <c>PluginCallRunner</c> is the only reader. Over the cap the answer is 413 and the plugin is not called; under it
/// the plugin gets the bytes as sent.
/// </summary>
public sealed class PluginFormBodyTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The multipart boundary the tests send.</summary>
    private const string Boundary = "b";

    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public PluginFormBodyTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>The form bodies the tests send, with the media type that makes MVC treat them as a form.</summary>
    public static TheoryData<string, bool> Forms => new()
    {
        { "application/x-www-form-urlencoded", false },
        { "application/x-www-form-urlencoded", true },
        { $"multipart/form-data; boundary={Boundary}", false },
        { $"multipart/form-data; boundary={Boundary}", true },
    };

    /// <summary>Makes the body of a form of exactly the given size in bytes.</summary>
    /// <param name="mediaType">The form's media type.</param>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The body.</returns>
    private static byte[] FormOfSize(string mediaType, int size)
    {
        if (!mediaType.StartsWith("multipart", StringComparison.Ordinal))
        {
            return Encoding.ASCII.GetBytes("a=" + new string('x', size - 2));
        }

        var head = $"--{Boundary}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\n";
        var tail = $"\r\n--{Boundary}--\r\n";
        return Encoding.ASCII.GetBytes(head + new string('x', size - head.Length - tail.Length) + tail);
    }

    /// <summary>Posts a form body to the admin echo route, declared or chunked.</summary>
    /// <param name="mediaType">The form's media type.</param>
    /// <param name="bytes">The body.</param>
    /// <param name="chunked">Whether the body is sent without a declared length.</param>
    /// <returns>The response.</returns>
    private async Task<HttpResponseMessage> PostAsync(string mediaType, byte[] bytes, bool chunked)
    {
        using var http = await _fixture.AdminClientAsync();
        using HttpContent content = chunked ? new UnknownLengthContent(bytes) : new ByteArrayContent(bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/plugins/{_fixture.PluginId}/ext/echo") { Content = content };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await http.SendAsync(request);
    }

    /// <summary>A form over the cap, declared or chunked, answers 413 and the plugin is not called.</summary>
    /// <param name="mediaType">The form's media type.</param>
    /// <param name="chunked">Whether the body is sent without a declared length.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(Forms))]
    public async Task AFormOverTheCap_Answers413WithoutCallingThePlugin(string mediaType, bool chunked)
    {
        var response = await PostAsync(mediaType, FormOfSize(mediaType, AppPluginFixture.MaxBodyBytes + 1), chunked);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE);
        _fixture.Probe.Constructed.Should().Be(0);
        _fixture.Probe.Calls.Should().BeEmpty();
    }

    /// <summary>A form under the cap reaches the plugin byte for byte, with its media type, and is not consumed by the host.</summary>
    /// <param name="mediaType">The form's media type.</param>
    /// <param name="chunked">Whether the body is sent without a declared length.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(Forms))]
    public async Task ASmallForm_ReachesThePluginByteForByte(string mediaType, bool chunked)
    {
        var bytes = FormOfSize(mediaType, AppPluginFixture.MaxBodyBytes);

        var response = await PostAsync(mediaType, bytes, chunked);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _fixture.Probe.Calls.Should().ContainSingle().Which.Body.ToArray().Should().Equal(bytes);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("contentType").GetString().Should().StartWith(mediaType.Split(';')[0]);
    }
}
