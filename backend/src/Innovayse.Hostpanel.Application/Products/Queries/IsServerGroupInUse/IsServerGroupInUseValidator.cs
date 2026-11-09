using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Products.Queries.IsServerGroupInUse;

/// <summary>Shape checks for <see cref="IsServerGroupInUseQuery"/>.</summary>
public sealed class IsServerGroupInUseValidator : LocalizedValidator<IsServerGroupInUseQuery>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public IsServerGroupInUseValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServerGroupId).MustBePositiveId(L["ServerGroupIdInvalid"]);
    }
}
