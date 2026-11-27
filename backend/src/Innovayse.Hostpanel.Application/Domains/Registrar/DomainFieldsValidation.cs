using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Domains.Registrar;

/// <summary>
/// The one check of the extra values given for a domain against the fields the registrar plugin's manifest declares for it,
/// done with the SDK's own rule (<see cref="PluginDomainFieldRules.Validate"/>) so the host and the plugin judge a value alike.
/// It asks the manifest, which is read from memory, so it runs before an order is placed and before a registrar is called. It
/// is a service and not a validator rule because choosing the fields needs the registrar of the domain's extension, which is a
/// database read, and no validator reads the database.
/// </summary>
public sealed class DomainFieldsValidation
{
    /// <summary>Chooses the fields the registrar plugin declares for the domain.</summary>
    private readonly DomainFieldReader _fields;

    /// <summary>Words a refusal in the caller's language.</summary>
    private readonly IErrorMessageResolver _messages;

    /// <summary>Initializes a new instance of the <see cref="DomainFieldsValidation"/> class.</summary>
    /// <param name="fields">Chooses the fields the registrar plugin declares for the domain.</param>
    /// <param name="messages">Words a refusal in the caller's language.</param>
    public DomainFieldsValidation(DomainFieldReader fields, IErrorMessageResolver messages)
    {
        _fields = fields;
        _messages = messages;
    }

    /// <summary>Tells the kind of registrant from the contact: a contact with an organisation is an organisation, any other is a person.</summary>
    /// <param name="organization">The organisation named on the registrant contact, or <see langword="null"/> or blank for none.</param>
    /// <returns>The holder the fields of a zone are chosen for.</returns>
    public static PluginDomainHolder HolderOf(string? organization)
    {
        return string.IsNullOrWhiteSpace(organization) ? PluginDomainHolder.Individual : PluginDomainHolder.Organization;
    }

    /// <summary>Checks the values given for a domain's extra fields.</summary>
    /// <param name="domainName">The fully-qualified domain name.</param>
    /// <param name="module">The registrar module the domain is held at, or <see langword="null"/> to follow the name's extension.</param>
    /// <param name="holder">The kind of registrant, see <see cref="HolderOf"/>.</param>
    /// <param name="operation">The one operation about to run.</param>
    /// <param name="attributes">The values by field key, or <see langword="null"/> for none.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success when the values are acceptable; otherwise the first refusal, <c>DOMAIN_FIELD_REQUIRED</c> for a required field
    /// with no value, <c>DOMAIN_FIELD_UNKNOWN</c> for a key no field declares or <c>DOMAIN_FIELD_INVALID</c> for a value the
    /// field refuses (or a set too long to store), with the field's key in <c>errors</c>.
    /// </returns>
    public async Task<Result> ValidateAsync(
        string domainName,
        string? module,
        PluginDomainHolder holder,
        PluginDomainOperations operation,
        IReadOnlyDictionary<string, string>? attributes,
        CancellationToken ct)
    {
        if (!DomainExtendedAttributes.TryCreate(attributes, out var values))
        {
            return Result.Failure(_messages.Resolve(HostpanelErrorCodes.DOMAIN_FIELD_INVALID), HostpanelErrorCodes.DOMAIN_FIELD_INVALID);
        }

        var fields = await _fields.MatchAsync(domainName, module, holder, operation, ct);
        var violations = PluginDomainFieldRules.Validate(fields, values.Values);
        if (violations.Count == 0)
        {
            return Result.Success();
        }

        var first = violations[0];
        var code = first.Reason switch
        {
            PluginDomainFieldViolationReason.Required => HostpanelErrorCodes.DOMAIN_FIELD_REQUIRED,
            PluginDomainFieldViolationReason.Unknown => HostpanelErrorCodes.DOMAIN_FIELD_UNKNOWN,
            _ => HostpanelErrorCodes.DOMAIN_FIELD_INVALID,
        };
        var sentence = _messages.Resolve(code);
        return Result.FailureWithDetail(sentence, code, first.Key, sentence);
    }
}
