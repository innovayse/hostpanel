using Innovayse.Hostpanel.Application.Notifications.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Supplies the brand an outgoing mail is rendered with.
/// </summary>
/// <remarks>
/// A port rather than a settings lookup inside the renderer, because the renderer is a
/// stateless Application service that a test drives with a string template and a model; the
/// implementation reads the <c>portal.*</c> rows and the portal's public address and lives in
/// Infrastructure.
/// </remarks>
public interface IEmailBrandingProvider
{
    /// <summary>
    /// Resolves the current brand, every field filled.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The brand for this render.</returns>
    public Task<EmailBranding> GetAsync(CancellationToken ct = default);
}
