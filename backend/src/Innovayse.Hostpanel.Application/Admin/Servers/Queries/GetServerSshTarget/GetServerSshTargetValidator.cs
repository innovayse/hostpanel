using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerSshTarget;

/// <summary>Shape checks for <see cref="GetServerSshTargetQuery"/>.</summary>
public sealed class GetServerSshTargetValidator : LocalizedValidator<GetServerSshTargetQuery>
{
    /// <summary>Initialises the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public GetServerSshTargetValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServerId).MustBePositiveId(L["ServerIdInvalid"]);
    }
}
