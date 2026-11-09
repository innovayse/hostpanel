using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Domains.Queries.ListDomains;

/// <summary>Validates <see cref="ListDomainsQuery"/> inputs before the handler executes.</summary>
public sealed class ListDomainsValidator : LocalizedValidator<ListDomainsQuery>
{
    /// <summary>Largest page size an admin listing may ask for.</summary>
    private const int MaxPageSize = PagingLimits.MaxPageSize;

    /// <summary>Initializes validator with localization service.</summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public ListDomainsValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Page).MustBePage(L["DomainPageRequired"]);
        RuleFor(x => x.PageSize).MustBePageSize(MaxPageSize, L["DomainPageSizeRange"]);
    }
}
