using FluentValidation;
using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Domain.Domains;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetDomainFields;

/// <summary>Validates <see cref="GetDomainFieldsQuery"/> inputs before the handler executes.</summary>
public sealed class GetDomainFieldsValidator : LocalizedValidator<GetDomainFieldsQuery>
{
    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public GetDomainFieldsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Domain)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(L["DomainNameRequired"])
            .Must(domain => DomainNameRules.IsValidName(domain.Trim())).WithMessage(L["DomainNameInvalid"]);

        // One operation at a time: None is no operation and a combination is not one the host runs.
        RuleFor(x => x.Operation)
            .Must(operation => operation is PluginDomainOperations.Register or PluginDomainOperations.Transfer or PluginDomainOperations.ContactChange)
            .WithMessage(L["DomainFieldsOperationInvalid"]);

        RuleFor(x => x.Holder).IsInEnum().WithMessage(L["DomainFieldsHolderInvalid"]);
    }
}
