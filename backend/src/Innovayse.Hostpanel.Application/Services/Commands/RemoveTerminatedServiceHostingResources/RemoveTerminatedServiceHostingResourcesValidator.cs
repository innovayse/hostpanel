using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Commands.RemoveTerminatedServiceHostingResources;

/// <summary>Validates <see cref="RemoveTerminatedServiceHostingResourcesCommand"/> before the handler executes.</summary>
public sealed class RemoveTerminatedServiceHostingResourcesValidator : LocalizedValidator<RemoveTerminatedServiceHostingResourcesCommand>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public RemoveTerminatedServiceHostingResourcesValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceId).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
