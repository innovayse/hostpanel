using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Commands.SavePluginSlotWidgetProducts;

/// <summary>
/// Validates <see cref="SavePluginSlotWidgetProductsCommand"/>: the slot and widget ids must fit their columns of
/// <c>PluginSlotProductBindings</c>, and the product list must be present, positive and bounded.
/// </summary>
public sealed class SavePluginSlotWidgetProductsValidator : LocalizedValidator<SavePluginSlotWidgetProductsCommand>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public SavePluginSlotWidgetProductsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.PluginId).NotEmpty().WithMessage(L["PluginIdRequired"]);
        RuleFor(x => x.PluginId)
            .Must(PluginIdentifier.IsValid)
            .When(x => !string.IsNullOrEmpty(x.PluginId))
            .WithMessage(L["PluginIdInvalid"]);

        RuleFor(x => x.SlotId).Must(PluginSlotProductBinding.IsValidId).WithMessage(L["PluginSlotIdInvalid"]);
        RuleFor(x => x.WidgetId).Must(PluginSlotProductBinding.IsValidId).WithMessage(L["PluginWidgetIdInvalid"]);

        RuleFor(x => x.ProductIds)
            .NotNull().WithMessage(L["PluginProductIdsInvalid", PluginSlotProductBinding.MaxProductsPerWidget]);
        RuleFor(x => x.ProductIds!)
            .Must(ids => ids.Count <= PluginSlotProductBinding.MaxProductsPerWidget && ids.All(id => id > 0))
            .When(x => x.ProductIds is not null)
            .WithMessage(L["PluginProductIdsInvalid", PluginSlotProductBinding.MaxProductsPerWidget]);
    }
}
