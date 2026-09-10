namespace Innovayse.Application.Admin.Commands.UpdateSetting;

using Innovayse.Application.Admin.Common;
using Innovayse.Application.Common;
using Innovayse.Domain.Settings;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="UpdateSettingCommand"/> by loading the setting, checking the value
/// against the key's vocabulary where it has one, and persisting the change.
/// </summary>
/// <remarks>
/// The vocabulary check lives here and not in a FluentValidation validator because the rule
/// depends on the row's <c>Key</c>, which the command does not carry — a validator would have
/// to load the row this handler loads a moment later. The admin panel constrains
/// <c>portal.template</c> to a dropdown, but the raw settings table on the same page and the
/// API itself accept any string, and a value the storefront does not recognise makes it fall
/// back to its default with no sign of why the operator's choice was ignored.
/// </remarks>
/// <param name="repo">Setting repository.</param>
/// <param name="uow">Unit of work for persisting changes.</param>
public sealed class UpdateSettingHandler(ISettingRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Loads the setting, applies the new value, and persists the change.
    /// </summary>
    /// <param name="command">The update setting command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the setting is not found.</exception>
    /// <exception cref="InvalidSettingValueException">
    /// Thrown when the key has a fixed vocabulary and the value is not in it.
    /// </exception>
    public async Task HandleAsync(UpdateSettingCommand command, CancellationToken ct)
    {
        var setting = await repo.FindByIdAsync(command.Id, ct)
            ?? throw new InvalidOperationException($"Setting {command.Id} not found.");

        // Trimmed for every key: a trailing space typed into the admin table is never what an
        // operator meant, and for a URL or a handle it is the difference between working and not.
        var value = (command.Value ?? string.Empty).Trim();

        if (PortalSettingKeys.AllowedValues.TryGetValue(setting.Key, out var allowed))
        {
            // The canonical spelling from the set is stored, not the caller's casing: the
            // storefront matches `portal.template` and the theme mode exactly, so "Aurora"
            // accepted as typed would still render the default.
            value = allowed.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidSettingValueException(setting.Key, value, allowed);
        }

        setting.UpdateValue(value);
        await uow.SaveChangesAsync(ct);
    }
}
