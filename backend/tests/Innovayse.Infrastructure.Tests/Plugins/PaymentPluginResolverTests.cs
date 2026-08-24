namespace Innovayse.Infrastructure.Tests.Plugins;

using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;
using Innovayse.Infrastructure.Plugins;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

/// <summary>Tests for <see cref="PaymentPluginResolver"/> settings gating and instantiation.</summary>
public class PaymentPluginResolverTests
{
    /// <summary>Minimal payment plugin used as the registered implementation type.</summary>
    public sealed class FakePaymentPlugin : IPaymentPlugin
    {
        /// <summary>Initializes the fake with the composed configuration for assertions.</summary>
        /// <param name="configuration">The configuration the resolver composed.</param>
        public FakePaymentPlugin(IConfiguration configuration) => Configuration = configuration;

        /// <summary>Gets the configuration the resolver passed in.</summary>
        public IConfiguration Configuration { get; }

        /// <inheritdoc/>
        public string CurrencyCode => "840";

        /// <inheritdoc/>
        public Task<PaymentSession> CreatePaymentAsync(PaymentRequest request, CancellationToken ct)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task<GatewayPaymentStatus> GetStatusAsync(string gatewayOrderId, CancellationToken ct)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task<string> RefundAsync(string gatewayOrderId, long amountMinor, CancellationToken ct)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Implementation type that deliberately does not implement <see cref="IPaymentPlugin"/>,
    /// standing in for a third-party plugin whose manifest mistakenly (or maliciously) claims
    /// <see cref="PluginType.Payment"/> for an entry point that isn't one.
    /// </summary>
    public sealed class MistypedManifestPlugin
    {
    }

    private const string Module = "fake-pay";

    private static PluginManifest Manifest(PluginType type = PluginType.Payment) => new()
    {
        Id = Module,
        Name = "Fake Pay",
        Version = "1.0.0",
        Author = "Innovayse",
        Description = "Fake payment plugin for tests.",
        Type = type,
        Category = "Payment",
        EntryPoint = typeof(FakePaymentPlugin).FullName!,
        SdkVersion = "1.0.0",
        Fields =
        [
            new PluginField { Key = "api_key", Label = "API Key", Type = "secret", Required = true },
        ],
    };

    private static PaymentPluginResolver CreateResolver(
        Dictionary<string, string> settings,
        PluginType pluginType = PluginType.Payment,
        IConfiguration? hostConfig = null,
        Type? implementationType = null)
    {
        var registry = new PluginRegistry();
        registry.Register(new LoadedPlugin(Manifest(pluginType), implementationType ?? typeof(FakePaymentPlugin)));

        var repo = new Mock<ISettingRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings.Select(kv => Setting.Create(kv.Key, kv.Value, null)).ToList());

        var services = new ServiceCollection().BuildServiceProvider();
        return new PaymentPluginResolver(
            repo.Object, registry, services, hostConfig ?? new ConfigurationBuilder().Build(),
            NullLogger<PaymentPluginResolver>.Instance);
    }

    [Fact]
    public async Task ResolveAsync_EnabledAndConfigured_ReturnsPluginWithComposedConfig()
    {
        var resolver = CreateResolver(new()
        {
            [$"integration:{Module}:is_enabled"] = "true",
            [$"integration:{Module}:api_key"] = "k-123",
        });

        var plugin = await resolver.ResolveAsync(Module, CancellationToken.None);

        var fake = Assert.IsType<FakePaymentPlugin>(plugin);
        Assert.Equal("k-123", fake.Configuration[$"integration:{Module}:api_key"]);
    }

    [Fact]
    public async Task ResolveAsync_Disabled_ReturnsNull()
    {
        var resolver = CreateResolver(new()
        {
            [$"integration:{Module}:is_enabled"] = "false",
            [$"integration:{Module}:api_key"] = "k-123",
        });

        Assert.Null(await resolver.ResolveAsync(Module, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_MissingRequiredField_ReturnsNull()
    {
        var resolver = CreateResolver(new()
        {
            [$"integration:{Module}:is_enabled"] = "true",
        });

        Assert.Null(await resolver.ResolveAsync(Module, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_UnknownModule_ReturnsNull()
    {
        var resolver = CreateResolver([]);

        Assert.Null(await resolver.ResolveAsync("nope", CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_WrongPluginType_ReturnsNull()
    {
        var resolver = CreateResolver(
            new()
            {
                [$"integration:{Module}:is_enabled"] = "true",
                [$"integration:{Module}:api_key"] = "k-123",
            },
            pluginType: PluginType.Provisioning);

        Assert.Null(await resolver.ResolveAsync(Module, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_IsEnabledKeyAbsent_ReturnsNull()
    {
        var resolver = CreateResolver(new()
        {
            [$"integration:{Module}:api_key"] = "k-123",
        });

        Assert.Null(await resolver.ResolveAsync(Module, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAsync_ManifestClaimsPaymentButImplementationDoesNotImplementIPaymentPlugin_ReturnsNullWithoutThrowing()
    {
        // A manifest's Type == Payment is only a JSON claim; the resolver must verify the CLR
        // type actually implements IPaymentPlugin before casting, and fail closed (null) rather
        // than let an InvalidCastException escape the payment path when it doesn't.
        var resolver = CreateResolver(
            new()
            {
                [$"integration:{Module}:is_enabled"] = "true",
                [$"integration:{Module}:api_key"] = "k-123",
            },
            implementationType: typeof(MistypedManifestPlugin));

        var result = await resolver.ResolveAsync(Module, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveAsync_SettingsOverrideHostConfiguration()
    {
        var hostConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"integration:{Module}:api_key"] = "old",
            })
            .Build();

        var resolver = CreateResolver(
            new()
            {
                [$"integration:{Module}:is_enabled"] = "true",
                [$"integration:{Module}:api_key"] = "new",
            },
            hostConfig: hostConfig);

        var plugin = await resolver.ResolveAsync(Module, CancellationToken.None);

        var fake = Assert.IsType<FakePaymentPlugin>(plugin);
        Assert.Equal("new", fake.Configuration[$"integration:{Module}:api_key"]);
    }
}
