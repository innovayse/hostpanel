using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetPublicLegalText;

/// <summary>
///     Validates <see cref="GetPublicLegalTextQuery"/>. The page is checked against the catalogue by the handler,
///     which answers an unknown page the way it answers an unknown setting; the validator exists so every query has
///     one, and a rule added later lands here.
/// </summary>
public sealed class GetPublicLegalTextValidator : LocalizedValidator<GetPublicLegalTextQuery>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetPublicLegalTextValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
    }
}
