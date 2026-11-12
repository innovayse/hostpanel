using FluentValidation;
using Innovayse.Hostpanel.Application.Common.Validators;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>Applies each shared rule once.</summary>
internal sealed class PagingProbeValidator : AbstractValidator<PagingProbe>
{
    /// <summary>Initializes the validator and declares its rules.</summary>
    public PagingProbeValidator()
    {
        RuleFor(x => x.Id).MustBePositiveId(ValidationRuleExtensionsTests.IdMessage);
        RuleFor(x => x.Page).MustBePage(ValidationRuleExtensionsTests.PageMessage);
        RuleFor(x => x.PageSize).MustBePageSize(ValidationRuleExtensionsTests.MaxPageSize, ValidationRuleExtensionsTests.PageSizeMessage);
        RuleFor(x => x.DiscountPercent).MustBeDiscountPercentRange(ValidationRuleExtensionsTests.DiscountMessage);
    }
}
