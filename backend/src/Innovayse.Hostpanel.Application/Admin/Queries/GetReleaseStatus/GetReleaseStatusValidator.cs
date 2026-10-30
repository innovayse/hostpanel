using FluentValidation;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetReleaseStatus;

/// <summary>
/// Validates <see cref="GetReleaseStatusQuery"/>. The query carries no parameters, so there is
/// nothing to validate; the class exists to satisfy the project convention.
/// </summary>
public sealed class GetReleaseStatusValidator : AbstractValidator<GetReleaseStatusQuery>
{
}
