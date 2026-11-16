using Innovayse.Hostpanel.Application.Common.Validators;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>Derives from the shared list validator with a page-size limit of its own.</summary>
internal sealed class ListQueryProbeValidator : ListQueryValidator<ListQueryProbe>
{
    /// <summary>Initializes the validator with the given page-size limit.</summary>
    /// <param name="localizer">Answers each message key with a marked copy of the key.</param>
    /// <param name="maxPageSize">The largest page size the probe accepts.</param>
    public ListQueryProbeValidator(IStringLocalizer localizer, int maxPageSize)
        : base(localizer, maxPageSize)
    {
    }
}
