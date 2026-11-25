using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListRegistrarDomains;

/// <summary>Validates <see cref="ListRegistrarDomainsQuery"/>: the registrar is an installed plugin and the cursor is a sane length.</summary>
public sealed class ListRegistrarDomainsValidator : LocalizedValidator<ListRegistrarDomainsQuery>
{
    /// <summary>The longest cursor accepted; a registrar's cursor is an opaque token, never a document.</summary>
    public const int MaxCursorLength = 1000;

    /// <summary>Initializes the rules.</summary>
    /// <param name="localizer">Validator sentences.</param>
    /// <param name="modules">Tells which registrar plugins are installed.</param>
    public ListRegistrarDomainsValidator(IStringLocalizer<ApplicationValidationMessages> localizer, IRegistrarModuleCatalogue modules)
        : base(localizer)
    {
        RuleFor(x => x.RegistrarModule)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(L["DomainListRegistrarInvalid"])
            .MustBeInstalledRegistrar(modules, L["DomainListRegistrarInvalid"]);
        RuleFor(x => x.Cursor).MaximumLength(MaxCursorLength).WithMessage(L["RegistrarDomainCursorInvalid"]);
    }
}
