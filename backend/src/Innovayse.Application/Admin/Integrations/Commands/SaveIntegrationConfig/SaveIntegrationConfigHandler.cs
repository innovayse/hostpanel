namespace Innovayse.Application.Admin.Integrations.Commands.SaveIntegrationConfig;

using Innovayse.Application.Common;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="SaveIntegrationConfigCommand"/> by upserting each field
/// as a key-value <see cref="Setting"/> and persisting in one transaction.
/// </summary>
/// <param name="repo">Setting repository for key-value lookups and inserts.</param>
/// <param name="uow">Unit of work for flushing changes.</param>
public sealed class SaveIntegrationConfigHandler(ISettingRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Placeholder value sent by the frontend when a secret field was not changed.
    /// Receiving this value means "do not overwrite the stored secret."
    /// </summary>
    private const string MaskPlaceholder = "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022";

    /// <summary>
    /// Upserts the is_enabled flag and every supplied field for the given slug,
    /// then saves all changes in a single call.
    /// </summary>
    /// <param name="command">The save integration config command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the settings have been persisted.</returns>
    public async Task HandleAsync(SaveIntegrationConfigCommand command, CancellationToken ct)
    {
        // Upsert the is_enabled flag.
        await UpsertAsync(
            $"integration:{command.Slug}:is_enabled",
            command.IsEnabled ? "true" : "false",
            $"Whether the {command.Slug} integration is enabled.",
            ct);

        // Upsert each supplied field, skipping masked placeholders.
        foreach (var (fieldKey, fieldValue) in command.Config)
        {
            if (fieldValue == MaskPlaceholder)
            {
                continue; // Frontend sent back a masked value -- do not overwrite stored secret.
            }

            await UpsertAsync(
                $"integration:{command.Slug}:{fieldKey}",
                fieldValue,
                $"{command.Slug} integration field: {fieldKey}.",
                ct);
        }

        await uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Finds an existing setting by <paramref name="key"/> and updates its value,
    /// or creates a new setting when none exists.
    /// </summary>
    /// <param name="key">The full setting key, e.g. integration:stripe:secret_key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="description">Human-readable description stored when creating a new setting.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous upsert operation.</returns>
    private async Task UpsertAsync(string key, string value, string description, CancellationToken ct)
    {
        var existing = await repo.FindByKeyAsync(key, ct);
        if (existing is null)
        {
            repo.Add(Setting.Create(key, value, description));
        }
        else
        {
            existing.UpdateValue(value);
        }
    }
}
