using FluentValidation;

namespace Innovayse.Hostpanel.Application.Admin.Queries.GetInstalledVersion;

/// <summary>
/// Validates <see cref="GetInstalledVersionQuery"/>. The query carries no parameters, so there is
/// nothing to validate; the class exists to satisfy the project convention.
/// </summary>
public sealed class GetInstalledVersionValidator : AbstractValidator<GetInstalledVersionQuery>
{
}
