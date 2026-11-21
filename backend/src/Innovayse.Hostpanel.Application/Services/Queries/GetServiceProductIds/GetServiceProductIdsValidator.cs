using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetServiceProductIds;

/// <summary>Validates <see cref="GetServiceProductIdsQuery"/>: every id must be a real id.</summary>
public sealed class GetServiceProductIdsValidator : LocalizedValidator<GetServiceProductIdsQuery>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    public GetServiceProductIdsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.ServiceIds).NotNull();
        RuleForEach(x => x.ServiceIds).MustBePositiveId(L["ServiceIdInvalid"]);
    }
}
