namespace Innovayse.Application.Admin.Commands.UpdateSetting;

using Innovayse.Application.Common;
using Innovayse.Domain.Settings.Interfaces;

/// <summary>
/// Handles <see cref="UpdateSettingCommand"/> by loading the setting and updating its value.
/// </summary>
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
    public async Task HandleAsync(UpdateSettingCommand command, CancellationToken ct)
    {
        var setting = await repo.FindByIdAsync(command.Id, ct)
            ?? throw new InvalidOperationException($"Setting {command.Id} not found.");

        setting.UpdateValue(command.Value);
        await uow.SaveChangesAsync(ct);
    }
}
