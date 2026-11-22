using FluentValidation;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Validators;
using Innovayse.Hostpanel.Application.Resources;
using Microsoft.Extensions.Localization;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Commands.CreateServer;

/// <summary>Validates <see cref="CreateServerCommand"/> before it reaches the handler.</summary>
public sealed class CreateServerValidator : LocalizedValidator<CreateServerCommand>
{
    /// <summary>Longest a server name may be.</summary>
    private const int NameMaxLength = 200;

    /// <summary>Longest a hostname may be.</summary>
    private const int HostnameMaxLength = 253;

    /// <summary>Longest an API username may be.</summary>
    private const int UsernameMaxLength = 100;

    /// <summary>
    ///     Initializes validator with localization service.
    /// </summary>
    /// <param name="localizer">String localizer for validation messages.</param>
    public CreateServerValidator(IStringLocalizer<ApplicationValidationMessages> localizer)
        : base(localizer)
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage(L["ServerNameRequired"])
            .MaximumLength(NameMaxLength).WithMessage(L["ServerNameTooLong", NameMaxLength]);

        RuleFor(x => x.Endpoint.Hostname).NotEmpty().WithMessage(L["ServerHostnameRequired"])
            .MaximumLength(HostnameMaxLength).WithMessage(L["ServerHostnameTooLong", HostnameMaxLength]);

        RuleFor(x => x.Module).Must(PluginServerModules.IsWellFormed).WithMessage(L["ServerModuleInvalid"]);

        // Optional: blank means the API is dialled at the hostname.
        RuleFor(x => x.Endpoint.ApiHost)
            .MaximumLength(HostnameMaxLength).WithMessage(L["ServerHostnameTooLong", HostnameMaxLength]);

        RuleFor(x => x.Username).NotEmpty().WithMessage(L["ServerUsernameRequired"])
            .MaximumLength(UsernameMaxLength).WithMessage(L["ServerUsernameTooLong", UsernameMaxLength]);

        RuleFor(x => x.Credentials.Password).MaximumLength(ProtectedValueLimits.ServerPassword)
            .WithMessage(L["ServerCredentialTooLong", ProtectedValueLimits.ServerPassword]);
        RuleFor(x => x.Credentials.ApiToken).MaximumLength(ProtectedValueLimits.ServerApiToken)
            .WithMessage(L["ServerCredentialTooLong", ProtectedValueLimits.ServerApiToken]);
        RuleFor(x => x.Credentials.AccessHash).MaximumLength(ProtectedValueLimits.ServerAccessHash)
            .WithMessage(L["ServerCredentialTooLong", ProtectedValueLimits.ServerAccessHash]);

        RuleFor(x => x.Hosting.MonthlyCost).GreaterThanOrEqualTo(0).WithMessage(L["ServerMonthlyCostInvalid"]);

        RuleFor(x => x.Limits.MaxAccounts).GreaterThan(0).When(x => x.Limits.MaxAccounts.HasValue)
            .WithMessage(L["ServerMaxAccountsInvalid"]);
    }
}
