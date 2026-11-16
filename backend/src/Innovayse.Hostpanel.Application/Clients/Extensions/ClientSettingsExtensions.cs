using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Extensions;

/// <summary>
/// The per-client switches an admin sets on the account (card processing, single sign-on), checked in one place
/// for every module that acts on them, so a use case asks the question instead of reading the Clients tables or
/// repeating the refusal.
/// </summary>
public static class ClientSettingsExtensions
{
    /// <summary>Refuses when the account has credit card processing disabled.</summary>
    /// <param name="bus">The Wolverine bus the client's brief is read through.</param>
    /// <param name="clientId">The client record's id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success when card payments and saved card charges may be taken, <c>CLIENT_NOT_FOUND</c> when the id
    /// names no record, or <c>CLIENT_CARD_PAYMENT_DISABLED</c>.
    /// </returns>
    public static async Task<Result> EnsureClientMayPayByCardAsync(this IMessageBus bus, int clientId, CancellationToken ct)
    {
        var client = await bus.FindClientBriefAsync(clientId, withEmail: false, ct);

        return client switch
        {
            null => Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND),
            { Settings.DisableCcProcessing: true } => Result.FailureCode(HostpanelErrorCodes.CLIENT_CARD_PAYMENT_DISABLED),
            _ => Result.Success(),
        };
    }

    /// <summary>Refuses when the account does not allow single sign-on into the hosting panels of its services.</summary>
    /// <param name="bus">The Wolverine bus the client's brief is read through.</param>
    /// <param name="clientId">The client record's id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success when the client may sign in to a panel through single sign-on, <c>CLIENT_NOT_FOUND</c> when the id
    /// names no record, or <c>CLIENT_SSO_DISABLED</c>.
    /// </returns>
    public static async Task<Result> EnsureClientMayUsePanelSsoAsync(this IMessageBus bus, int clientId, CancellationToken ct)
    {
        var client = await bus.FindClientBriefAsync(clientId, withEmail: false, ct);

        return client switch
        {
            null => Result.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND),
            { Settings.AllowSso: false } => Result.FailureCode(HostpanelErrorCodes.CLIENT_SSO_DISABLED),
            _ => Result.Success(),
        };
    }
}
