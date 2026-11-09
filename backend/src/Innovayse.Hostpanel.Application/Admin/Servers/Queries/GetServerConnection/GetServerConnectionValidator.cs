using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Queries.GetServerConnection;

/// <summary>Shape checks for <see cref="GetServerConnectionQuery"/>.</summary>
public sealed class GetServerConnectionValidator : LocalizedValidator<GetServerConnectionQuery>
{
    /// <summary>Initialises the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public GetServerConnectionValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServerId).MustBePositiveId(L["ServerIdInvalid"]);
    }
}
