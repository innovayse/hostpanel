using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Commands.UploadBrandingImage;

/// <summary>
///     Validates <see cref="UploadBrandingImageCommand"/> before it reaches the handler. Only the
///     shape is checked here: whether the decoded image itself is acceptable is the handler's
///     answer, because that ceiling this deployment enforces, not a malformed request.
/// </summary>
public sealed class UploadBrandingImageValidator : LocalizedValidator<UploadBrandingImageCommand>
{
    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public UploadBrandingImageValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        // IsInEnum: the model binder also accepts a numeric segment and yields a value that is not
        // one of the named members, so "/branding/7" would otherwise reach the storage layer and
        // become a directory called "7"; caller-steered input in a path, which is exactly what
        // that layer documents as impossible.
        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithMessage(L["BrandingKindInvalid"]);

        RuleFor(x => x.Length).GreaterThan(0).WithMessage(L["BrandingImageRequired"]);
    }
}
