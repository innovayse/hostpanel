using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.Plugins;

/// <summary>
/// One product an operator bound a plugin slot widget to after install. A widget with at least one binding shows only for
/// those products and its manifest <c>productIds</c> are ignored; a widget never bound keeps the manifest binding. The
/// table is host owned and written only by the operator command, never through a plugin or a settings field, because
/// the binding decides where a plugin is shown and what it may reach. A widget that has rows keeps its binding even when
/// every product of it is deleted: it then shows nowhere, it never falls back to the manifest.
/// </summary>
/// <remarks>
/// <see cref="CreatedAt"/> is not set by <see cref="Create"/>: the auditable save interceptor stamps it on insert, and the
/// plugin data purge depends on that stamp to delete only the rows written up to the purge request. A row saved without
/// that interceptor would keep the default value and be purged by every request.
/// </remarks>
public sealed class PluginSlotProductBinding : BaseEntity, IAggregateRoot
{
    /// <summary>The column length of a slot id or a widget id.</summary>
    public const int IdMaxLength = 100;

    /// <summary>The most products one widget can be bound to.</summary>
    public const int MaxProductsPerWidget = 100;

    /// <summary>Gets the plugin that declares the widget.</summary>
    public string PluginId { get; private set; } = string.Empty;

    /// <summary>Gets the slot the widget is placed in.</summary>
    public string SlotId { get; private set; } = string.Empty;

    /// <summary>Gets the widget id within the plugin.</summary>
    public string WidgetId { get; private set; } = string.Empty;

    /// <summary>Gets the product the widget is bound to.</summary>
    public int ProductId { get; private set; }

    /// <summary>Gets when the binding was written; stamped on insert by the auditable save interceptor, and what a purge compares with the moment of the removal.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core materialisation only.</summary>
    private PluginSlotProductBinding()
    {
    }

    /// <summary>Binds a widget to one product. The only way a row comes into existence.</summary>
    /// <param name="pluginId">The plugin that declares the widget; not empty. The plugin id rule is checked by the use case's validator.</param>
    /// <param name="slotId">The slot the widget is placed in.</param>
    /// <param name="widgetId">The widget id within the plugin.</param>
    /// <param name="productId">The product, positive.</param>
    /// <returns>The new, unsaved binding.</returns>
    /// <exception cref="ArgumentException">An id is empty or too long, or the product id is not positive.</exception>
    public static PluginSlotProductBinding Create(string pluginId, string slotId, string widgetId, int productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        EnsureId(slotId, nameof(slotId));
        EnsureId(widgetId, nameof(widgetId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);
        return new PluginSlotProductBinding { PluginId = pluginId, SlotId = slotId, WidgetId = widgetId, ProductId = productId };
    }

    /// <summary>Whether <paramref name="id"/> is a slot id or widget id the table can hold.</summary>
    /// <param name="id">The candidate id.</param>
    /// <returns><see langword="true"/> for a non-empty id of at most <see cref="IdMaxLength"/> characters.</returns>
    public static bool IsValidId(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) && id.Length <= IdMaxLength;
    }

    /// <summary>Refuses a slot id or widget id the table cannot hold.</summary>
    /// <param name="id">The candidate id.</param>
    /// <param name="name">The parameter name for the exception.</param>
    /// <exception cref="ArgumentException">The id is empty or too long.</exception>
    private static void EnsureId(string id, string name)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentException("The id is empty or too long.", name);
        }
    }
}
