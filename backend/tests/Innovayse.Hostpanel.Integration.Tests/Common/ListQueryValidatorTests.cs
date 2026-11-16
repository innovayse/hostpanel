using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Resources;
using Innovayse.Hostpanel.Integration.Tests.Shared;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary>Pins the page, page size and search rules every list shares, including a use case's own page-size limit.</summary>
public sealed class ListQueryValidatorTests
{
    /// <summary>The page-size limit of the probe, below the ordinary one.</summary>
    private const int ProbeMaxPageSize = 25;

    /// <summary>Answers each message key with a marked copy of the key.</summary>
    private static readonly EchoLocalizer<ApplicationValidationMessages> Localizer = new();

    /// <summary>A page size up to the use case's own limit passes, and one above it is refused.</summary>
    /// <param name="pageSize">The page size under test.</param>
    /// <param name="valid">Whether the size is within the probe's limit.</param>
    [Theory]
    [InlineData(1, true)]
    [InlineData(ProbeMaxPageSize, true)]
    [InlineData(ProbeMaxPageSize + 1, false)]
    [InlineData(PagingLimits.MaxPageSize, false)]
    public void PageSize_FollowsTheUseCaseLimit(int pageSize, bool valid)
    {
        var result = new ListQueryProbeValidator(Localizer, ProbeMaxPageSize).Validate(new ListQueryProbe(1, pageSize));

        result.IsValid.Should().Be(valid);
    }

    /// <summary>A list given the ordinary limit accepts that maximum and no more.</summary>
    [Fact]
    public void PageSize_AtTheOrdinaryLimit_AcceptsTheMaximumOnly()
    {
        var validator = new ListQueryProbeValidator(Localizer, PagingLimits.MaxPageSize);

        validator.Validate(new ListQueryProbe(1, PagingLimits.MaxPageSize)).IsValid.Should().BeTrue();
        validator.Validate(new ListQueryProbe(1, PagingLimits.MaxPageSize + 1)).IsValid.Should().BeFalse();
    }

    /// <summary>A page below 1 is refused with the page sentence.</summary>
    [Fact]
    public void PageBelowOne_IsRefused()
    {
        var result = new ListQueryProbeValidator(Localizer, ProbeMaxPageSize).Validate(new ListQueryProbe(0, 10));

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("text:ListPageRequired");
    }

    /// <summary>A search of the longest allowed length passes and one character more is refused with the search sentence.</summary>
    [Fact]
    public void Search_IsBoundedByTheLongestAllowedLength()
    {
        var validator = new ListQueryProbeValidator(Localizer, ProbeMaxPageSize);

        validator.Validate(new ListQueryProbe(1, 10, new string('a', PagingLimits.MaxSearchLength))).IsValid.Should().BeTrue();

        var result = validator.Validate(new ListQueryProbe(1, 10, new string('a', PagingLimits.MaxSearchLength + 1)));

        result.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("text:ListSearchTooLong");
    }
}
