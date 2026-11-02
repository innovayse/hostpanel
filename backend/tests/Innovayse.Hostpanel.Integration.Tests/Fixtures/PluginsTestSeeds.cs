using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Shared seeding and authentication helpers for the Admin/Plugins module's per-use-case
/// Integration tests: building well-formed and malformed plugin ZIP archives, an authenticated
/// admin or Client-role caller, and reading a refusal's error code. Every
/// <c>Admin/Plugins/*Tests.cs</c> file calls into this rather than repeating its own copy.
/// </summary>
internal static class PluginsTestSeeds
{
    /// <summary>Shared test password for every client user this class registers.</summary>
    public const string TestPassword = "Pass@123!";

    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>An authenticated client with the Admin role.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory)
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Registers a brand-new client user and returns an authenticated <see cref="HttpClient"/>
    /// carrying only the Client role: the caller every admin-only boundary test refuses.
    /// </summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <param name="label">A short label folded into the unique email, for readable test failures.</param>
    /// <returns>An authenticated client with no elevated role.</returns>
    public static async Task<HttpClient> ClientRoleClientAsync(IntegrationTestFactory factory, string label)
    {
        var httpClient = factory.CreateClient();
        var email = $"{label}-{Guid.NewGuid():N}@example.com";
        var registerResponse = await httpClient.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = TestPassword,
            firstName = "Plugin",
            lastName = "Boundary",
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var token = await factory.GetClientTokenAsync(email, TestPassword);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return httpClient;
    }

    /// <summary>Asserts the refusal carries the expected error code.</summary>
    /// <param name="response">The refusal.</param>
    /// <param name="expected">The code it must carry.</param>
    /// <returns>A task that completes once the body has been read.</returns>
    public static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(expected);
    }

    /// <summary>The <c>plugin.json</c> text of a well-formed test plugin.</summary>
    /// <param name="pluginId">The plugin id to declare.</param>
    /// <param name="extraMembers">Further JSON members to append, each starting with a comma, for example <c>,"manifestVersion":2</c>; empty for none.</param>
    /// <returns>The manifest text.</returns>
    public static string ManifestJson(string pluginId, string extraMembers = "")
    {
        return $$"""
            {
              "id": "{{JsonEncodedText.Encode(pluginId)}}",
              "name": "Test Plugin",
              "version": "1.0.0",
              "author": "Test",
              "description": "A plugin used only in tests.",
              "type": "Provisioning",
              "category": "Test",
              "entryPoint": "Test.Plugin",
              "sdkVersion": "1.0"{{extraMembers}}
            }
            """;
    }

    /// <summary>Builds a ZIP holding exactly the given entries, in order.</summary>
    /// <param name="entries">Entry name to text content.</param>
    /// <returns>The ZIP archive's raw bytes.</returns>
    public static byte[] BuildZip(IReadOnlyDictionary<string, string> entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(text);
            }
        }

        return ms.ToArray();
    }

    /// <summary>Builds a ZIP holding exactly the given binary entries, in order.</summary>
    /// <param name="entries">Entry name to raw content.</param>
    /// <returns>The ZIP archive's raw bytes.</returns>
    public static byte[] BuildZip(IReadOnlyDictionary<string, byte[]> entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(content);
            }
        }

        return ms.ToArray();
    }

    /// <summary>Builds a well-formed plugin ZIP with the given id, one harmless file and any extra entries.</summary>
    /// <param name="pluginId">The plugin id to write into <c>plugin.json</c>.</param>
    /// <param name="extraEntries">Further entries to add after the harmless file, name to text content.</param>
    /// <returns>The ZIP archive's raw bytes.</returns>
    public static byte[] BuildValidPluginZip(string pluginId, IReadOnlyDictionary<string, string>? extraEntries = null)
    {
        var entries = new Dictionary<string, string>
        {
            ["plugin.json"] = ManifestJson(pluginId),
            ["readme.txt"] = "harmless file",
        };
        foreach (var (name, text) in extraEntries ?? new Dictionary<string, string>())
        {
            entries[name] = text;
        }

        return BuildZip(entries);
    }

    /// <summary>Builds a ZIP with no <c>plugin.json</c> at its root.</summary>
    /// <returns>The ZIP archive's raw bytes.</returns>
    public static byte[] BuildZipWithoutManifest()
    {
        return BuildZip(new Dictionary<string, string> { ["readme.txt"] = "no manifest here" });
    }

    /// <summary>The folder an uploaded plugin with this id is unpacked into, under the default settings.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The absolute folder path.</returns>
    public static string InstalledFolder(string pluginId)
    {
        return Path.Combine(new PluginOptions().InstallRoot, pluginId);
    }

    /// <summary>
    /// Places a plugin in the build output's plugin folder, as the build stages a shipped plugin,
    /// so a test can act on a built-in plugin. The caller deletes the returned folder.
    /// </summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The folder created.</returns>
    public static string StageBuiltInPlugin(string pluginId)
    {
        var folder = Path.Combine(PluginOptions.BuiltInRoot, pluginId);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), ManifestJson(pluginId));
        return folder;
    }

    /// <summary>A client of <paramref name="host"/> carrying the seeded admin's bearer token.</summary>
    /// <param name="factory">The shared API + PostgreSQL host, which issues the token.</param>
    /// <param name="host">A host derived from <paramref name="factory"/> with its own settings.</param>
    /// <returns>An authenticated client of the derived host.</returns>
    public static async Task<HttpClient> AdminClientAsync(IntegrationTestFactory factory, WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Posts a multipart install request carrying the given ZIP bytes.</summary>
    /// <param name="client">Authenticated admin client.</param>
    /// <param name="zipBytes">The archive to upload.</param>
    /// <returns>The install response.</returns>
    public static async Task<HttpResponseMessage> InstallAsync(HttpClient client, byte[] zipBytes)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zipBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", "plugin.zip");
        return await client.PostAsync("/api/admin/plugins/install", content);
    }

    /// <summary>Posts a multipart install request whose form carries no file at all.</summary>
    /// <param name="client">Authenticated admin client.</param>
    /// <returns>The install response.</returns>
    public static async Task<HttpResponseMessage> InstallWithoutFileAsync(HttpClient client)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("nothing"), "note");
        return await client.PostAsync("/api/admin/plugins/install", content);
    }
}
