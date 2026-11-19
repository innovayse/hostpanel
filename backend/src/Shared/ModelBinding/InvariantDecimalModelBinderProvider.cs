using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Shared.ModelBinding;

/// <summary>
///     Provider that replaces the default decimal model binder with
///     <see cref="InvariantDecimalModelBinder"/> for <see cref="decimal"/> and
///     <see cref="Nullable{Decimal}"/> types, accepting both period and comma as decimal separators.
/// </summary>
public sealed class InvariantDecimalModelBinderProvider : IModelBinderProvider
{
    /// <summary>Localizer passed through to the binder for per-request error message resolution.</summary>
    private readonly IStringLocalizer _localizer;

    /// <summary>
    ///     Initializes the provider with a string localizer.
    /// </summary>
    /// <param name="localizer">
    ///     Localizer used by the binder to resolve the error message at request time,
    ///     so the message respects the current request culture (en/ru/hy/de/es).
    /// </param>
    public InvariantDecimalModelBinderProvider(IStringLocalizer localizer)
    {
        _localizer = localizer;
    }

    /// <inheritdoc/>
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = context.Metadata.ModelType;
        var isNullable = modelType == typeof(decimal?);

        if (modelType != typeof(decimal) && !isNullable)
        {
            return null;
        }

        return new InvariantDecimalModelBinder(isNullable, _localizer);
    }
}
