using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Manifest;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>How the host reads a <c>plugin.json</c>: the optional <c>capabilities</c> list and the version 2 blocks.</summary>
public sealed class PluginManifestReaderTests
{
    /// <summary>Builds a manifest text with the given extra members after the required ones.</summary>
    /// <param name="extra">Extra JSON members, for example <c>,"capabilities":null</c>, or an empty string.</param>
    /// <returns>The manifest text.</returns>
    private static string Manifest(string extra)
    {
        return "{\"id\":\"p\",\"name\":\"P\",\"version\":\"1.0.0\",\"author\":\"A\",\"description\":\"D\","
            + "\"type\":\"payment\",\"category\":\"Payment\",\"entryPoint\":\"P.Plugin\",\"sdkVersion\":\"1.0\"" + extra + "}";
    }

    /// <summary>A manifest without <c>capabilities</c> reads as an empty list.</summary>
    [Fact]
    public void Deserialize_CapabilitiesAbsent_IsEmpty()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest(string.Empty));

        manifest!.Capabilities.Should().NotBeNull().And.BeEmpty();
    }

    /// <summary>A manifest with <c>"capabilities": null</c> reads as an empty list, never null.</summary>
    [Fact]
    public void Deserialize_CapabilitiesNull_IsEmpty()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest(",\"capabilities\":null"));

        manifest!.Capabilities.Should().NotBeNull().And.BeEmpty();
    }

    /// <summary>A manifest that lists capabilities keeps them in order.</summary>
    [Fact]
    public void Deserialize_CapabilitiesPresent_KeepsThem()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest(",\"capabilities\":[\"storedCredentials\"]"));

        manifest!.Capabilities.Should().Equal(PluginCapabilities.StoredCredentials);
    }

    /// <summary>The file reader returns the manifest with an empty list when <c>capabilities</c> is null.</summary>
    [Fact]
    public void TryRead_CapabilitiesNull_ReturnsManifestWithEmptyList()
    {
        var path = Path.Combine(Path.GetTempPath(), $"plugin-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, Manifest(",\"capabilities\":null"));
        try
        {
            var manifest = new PluginManifestReader(NullLogger<PluginManifestReader>.Instance).TryRead(path);

            manifest.Should().NotBeNull();
            manifest!.Capabilities.Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A manifest with every version 2 block is read into the matching SDK types.</summary>
    [Fact]
    public void Deserialize_ManifestVersion2_ParsesEveryBlock()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest("""
            ,"manifestVersion":2,
            "locales":{"en":{"menu.reports":"Reports"},"ru":{"menu.reports":"Reports RU"}},
            "settings":[
              {"key":"baseUrl","label":"setting.baseUrl","type":"url","required":true},
              {"key":"mode","label":"setting.mode","type":"select","options":[{"value":"live","label":"Live"},{"value":"test","label":"Test"}]},
              {"key":"apiKey","label":"setting.apiKey","type":"secret","required":true},
              {"key":"retries","label":"setting.retries","type":"number"},
              {"key":"verbose","label":"setting.verbose","type":"boolean"},
              {"key":"note","label":"setting.note","type":"textarea"}
            ],
            "api":{"client":[{"method":"get","path":"/invoices/{id}","permission":"Any"},{"method":"post","path":"/sync"}],"admin":[{"method":"get","path":"/status"}]},
            "ui":{
              "client":{"menus":[{"id":"reports","label":"menu.reports","page":"reports","icon":"chart"}],
                        "pages":[{"id":"reports","path":"/reports","title":"menu.reports","view":"views/reports.json","permission":"Any"}]},
              "admin":{"menus":[{"id":"a","label":"A","page":"a"}],"pages":[{"id":"a","path":"/a","title":"A","view":"views/a.json"}]},
              "slots":[{"id":"card","slot":"client.dashboard.cards","view":"views/card.json","title":"Card"}]
            },
            "externalOrigins":["https://example.com"]
            """));

        manifest!.ManifestVersion.Should().Be(2);
        manifest.Locales["ru"]["menu.reports"].Should().Be("Reports RU");
        manifest.Settings.Select(s => s.Type).Should().Equal(
            PluginSettingType.Url, PluginSettingType.Select, PluginSettingType.Secret,
            PluginSettingType.Number, PluginSettingType.Boolean, PluginSettingType.Textarea);
        manifest.Settings[0].Required.Should().BeTrue();
        manifest.Settings[1].Options.Select(o => o.Value).Should().Equal("live", "test");
        manifest.Settings[1].Options[1].Label.Should().Be("Test");
        manifest.Settings[0].Options.Should().BeEmpty();
        manifest.Api.Client.Select(r => (r.Method, r.Path, r.Permission)).Should().Equal(
            (PluginRouteMethod.Get, "/invoices/{id}", PluginPermissions.Any), (PluginRouteMethod.Post, "/sync", null));
        manifest.Ui.Client.Menus.Should().ContainSingle().Which.Icon.Should().Be("chart");
        manifest.Ui.Client.Menus[0].Page.Should().Be("reports");
        manifest.Ui.Client.Pages.Should().ContainSingle().Which.View.Should().Be("views/reports.json");
        manifest.Ui.Client.Pages[0].Permission.Should().Be(PluginPermissions.Any);
        manifest.Ui.Admin.Pages.Should().ContainSingle().Which.Permission.Should().BeNull();
        manifest.Ui.Admin.Menus.Should().ContainSingle().Which.Icon.Should().BeNull();
        manifest.Ui.Slots.Should().ContainSingle().Which.Slot.Should().Be("client.dashboard.cards");
        manifest.Ui.Slots[0].Title.Should().Be("Card");
        manifest.Ui.Slots[0].Id.Should().Be("card");
        manifest.ExternalOrigins.Should().Equal("https://example.com");
    }

    /// <summary>A version 1 manifest reads as before: version 1, its fields, and every new block empty.</summary>
    [Fact]
    public void Deserialize_ManifestVersion1_IsUnchanged()
    {
        var manifest = PluginManifestReader.Deserialize(
            Manifest(",\"fields\":[{\"key\":\"k\",\"label\":\"K\",\"type\":\"text\",\"required\":true}]"));

        manifest!.ManifestVersion.Should().Be(1);
        manifest.Fields.Should().ContainSingle().Which.Key.Should().Be("k");
        manifest.Settings.Should().BeEmpty();
        manifest.Locales.Should().BeEmpty();
        manifest.Api.Client.Should().BeEmpty();
        manifest.Ui.Client.Menus.Should().BeEmpty();
        manifest.Ui.Client.Pages.Should().BeEmpty();
        manifest.Ui.Admin.Pages.Should().BeEmpty();
        manifest.Ui.Slots.Should().BeEmpty();
        manifest.ExternalOrigins.Should().BeEmpty();
    }

    /// <summary>A <c>null</c> in any new block reads as empty, never as null.</summary>
    [Fact]
    public void Deserialize_NewBlocksNull_AreEmpty()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest(
            ",\"settings\":null,\"locales\":null,\"api\":null,\"ui\":{\"client\":null,\"admin\":{\"menus\":null,\"pages\":null},\"slots\":null},\"externalOrigins\":null"));

        manifest!.Settings.Should().BeEmpty();
        manifest.Locales.Should().BeEmpty();
        manifest.Api.Client.Should().BeEmpty();
        manifest.Api.Admin.Should().BeEmpty();
        manifest.Ui.Client.Pages.Should().BeEmpty();
        manifest.Ui.Admin.Menus.Should().BeEmpty();
        manifest.Ui.Slots.Should().BeEmpty();
        manifest.ExternalOrigins.Should().BeEmpty();
    }

    /// <summary>A <c>null</c> client or admin route list reads as empty, never as null.</summary>
    [Fact]
    public void Deserialize_ApiListsNull_AreEmpty()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest(",\"api\":{\"client\":null,\"admin\":null}"));

        manifest!.Api.Client.Should().NotBeNull().And.BeEmpty();
        manifest.Api.Admin.Should().NotBeNull().And.BeEmpty();
    }

    /// <summary>The routes split into the client and admin lists, and an admin route carries no permission.</summary>
    [Fact]
    public void Deserialize_ApiClientAndAdmin_AreSeparateLists()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest(
            ",\"api\":{\"client\":[{\"method\":\"get\",\"path\":\"/a\",\"permission\":\"Any\"}],\"admin\":[{\"method\":\"delete\",\"path\":\"/b\"}]}"));

        manifest!.Api.Client.Select(r => r.Path).Should().Equal("/a");
        manifest.Api.Admin.Select(r => (r.Method, r.Path, r.Permission)).Should().Equal(
            (PluginRouteMethod.Delete, "/b", null));
    }

    /// <summary>A route reads its rate limit policy and streaming flag; absent means no policy (default) and not streaming.</summary>
    [Fact]
    public void Deserialize_RouteRateLimitAndStreaming_AreRead()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest("""
            ,"api":{"client":[
              {"method":"post","path":"/chat","rateLimit":"strict","streaming":true},
              {"method":"get","path":"/report","rateLimit":"heavy"},
              {"method":"get","path":"/plain","rateLimit":"default"},
              {"method":"get","path":"/none"}
            ]}
            """));

        var routes = manifest!.Api.Client;
        routes.Select(r => r.RateLimit).Should().Equal(
            PluginRateLimitPolicy.Strict, PluginRateLimitPolicy.Heavy, PluginRateLimitPolicy.Default, null);
        routes.Select(r => r.Streaming).Should().Equal(true, false, false, false);
    }

    /// <summary>A slot entry reads its id, order, permission, product binding and context keys.</summary>
    [Fact]
    public void Deserialize_SlotEntryMembers_AreRead()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest("""
            ,"ui":{"slots":[
              {"id":"usage","slot":"client.service.panel","view":"v/usage.json","order":5,"permission":"Any",
               "productIds":[3,7],"contextKeys":["serviceId"]},
              {"id":"bare","slot":"admin.client.panel","view":"v/bare.json","productIds":null,"contextKeys":null}
            ]}
            """));

        var slots = manifest!.Ui.Slots;
        slots[0].Id.Should().Be("usage");
        slots[0].Order.Should().Be(5);
        slots[0].Permission.Should().Be(PluginPermissions.Any);
        slots[0].ProductIds.Should().Equal(3, 7);
        slots[0].ContextKeys.Should().Equal("serviceId");
        slots[1].Order.Should().Be(0);
        slots[1].Permission.Should().BeNull();
        slots[1].ProductIds.Should().NotBeNull().And.BeEmpty();
        slots[1].ContextKeys.Should().NotBeNull().And.BeEmpty();
    }

    /// <summary>A menu entry reads its order; absent reads as zero.</summary>
    [Fact]
    public void Deserialize_MenuItemOrder_IsReadAndDefaultsToZero()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest("""
            ,"ui":{"client":{"menus":[
              {"id":"a","label":"A","page":"a","order":4},
              {"id":"b","label":"B","page":"b"}
            ]}}
            """));

        manifest!.Ui.Client.Menus.Select(m => m.Order).Should().Equal(4, 0);
    }

    /// <summary>A setting reads its description, default, placeholder, min and max; absent members are null.</summary>
    [Fact]
    public void Deserialize_SettingDescriptionDefaultPlaceholderMinMax_AreRead()
    {
        var manifest = PluginManifestReader.Deserialize(Manifest("""
            ,"settings":[
              {"key":"retries","label":"R","type":"number","description":"setting.retries.help","default":"3",
               "placeholder":"3","min":1,"max":10.5},
              {"key":"plain","label":"P","type":"text"}
            ]
            """));

        var settings = manifest!.Settings;
        settings[0].Description.Should().Be("setting.retries.help");
        settings[0].Default.Should().Be("3");
        settings[0].Placeholder.Should().Be("3");
        settings[0].Min.Should().Be(1m);
        settings[0].Max.Should().Be(10.5m);
        settings[1].Description.Should().BeNull();
        settings[1].Default.Should().BeNull();
        settings[1].Placeholder.Should().BeNull();
        settings[1].Min.Should().BeNull();
        settings[1].Max.Should().BeNull();
    }
}
