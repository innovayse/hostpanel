using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Commands.ImportRegistrarDomain;

/// <summary>Validates <see cref="ImportRegistrarDomainCommand"/> inputs before the handler executes.</summary>
public sealed class ImportRegistrarDomainValidator : LocalizedValidator<ImportRegistrarDomainCommand>
{
    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    /// <param name="modules">Tells which registrar plugins are installed.</param>
    public ImportRegistrarDomainValidator(IStringLocalizer<ApplicationValidationMessages> localizer, IRegistrarModuleCatalogue modules)
        : base(localizer)
    {
        RuleFor(x => x.RegistrarModule)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(L["DomainListRegistrarInvalid"])
            .MustBeInstalledRegistrar(modules, L["DomainListRegistrarInvalid"]);
        RuleFor(x => x.ClientId).MustBePositiveId(L["DomainClientIdRequired"]);
        RuleFor(x => x.DomainName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(L["DomainNameRequired"])
            .MustBeDomainName(L["DomainNameInvalid"]);
    }
}
