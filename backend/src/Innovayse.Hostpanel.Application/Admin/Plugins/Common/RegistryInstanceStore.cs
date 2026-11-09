using System.Text.Json;
using System.Text.Json.Serialization;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// The one reader and writer of this installation's registry identity. It lives in a single settings row
/// (<see cref="PluginSettingKeys.RegistryInstance"/>) that holds the whole <see cref="RegistryInstanceRecord"/> as JSON and is
/// stored encrypted, because the record carries the private key. No endpoint returns the row.
/// </summary>
public sealed class RegistryInstanceStore
{
    /// <summary>The description the settings row is created with.</summary>
    private const string RowDescription = "This installation's identity towards the plugin registry. Encrypted; never returned.";

    /// <summary>The JSON conventions of the stored document: camelCase names and enums as their names.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _db;

    /// <summary>Encrypts the document before it is stored and decrypts it when it is read.</summary>
    private readonly ISecretSettingCipher _cipher;

    /// <summary>Initializes the store.</summary>
    /// <param name="db">The module's data context.</param>
    /// <param name="cipher">Encrypts the document before it is stored and decrypts it when it is read.</param>
    public RegistryInstanceStore(IHostpanelDbContext db, ISecretSettingCipher cipher)
    {
        _db = db;
        _cipher = cipher;
    }

    /// <summary>Reads the identity.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The identity, or <see langword="null"/> when none was created yet or the stored document is not readable (it is then made again).</returns>
    public async Task<RegistryInstanceRecord?> LoadAsync(CancellationToken ct)
    {
        var stored = await _db.Settings.AsNoTracking()
            .Where(s => s.Key == PluginSettingKeys.RegistryInstance)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RegistryInstanceRecord>(_cipher.Reveal(stored), Json);
        }
        catch (JsonException)
        {
            // A document that cannot be read is no identity at all: the caller makes a new one rather than failing for good.
            return null;
        }
    }

    /// <summary>Stores the identity, creating the row the first time.</summary>
    /// <param name="record">The identity.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the row is saved.</returns>
    public async Task SaveAsync(RegistryInstanceRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);

        var protectedValue = _cipher.Protect(JsonSerializer.Serialize(record, Json));
        var row = await _db.Settings.FirstOrDefaultAsync(s => s.Key == PluginSettingKeys.RegistryInstance, ct);
        if (row is null)
        {
            _db.Settings.Add(Setting.Create(PluginSettingKeys.RegistryInstance, protectedValue, RowDescription));
        }
        else
        {
            row.UpdateValue(protectedValue);
        }

        await _db.SaveChangesAsync(ct);
    }
}
