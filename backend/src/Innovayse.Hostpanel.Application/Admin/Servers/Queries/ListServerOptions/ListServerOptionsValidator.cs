using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.ListServerOptions;

/// <summary>Validates <see cref="ListServerOptionsQuery"/>: the module filter, when given, names a module.</summary>
public sealed class ListServerOptionsValidator : LocalizedValidator<ListServerOptionsQuery>
{
    /// <summary>Initializes the validator.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListServerOptionsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not a member.
        RuleFor(x => x.Module).IsInEnum().WithMessage(L["ServerListModuleInvalid"]);
    }
}
