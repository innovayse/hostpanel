using Hostpanel.Sdk.Plugins.Enums;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetDomainFields;

/// <summary>Query for the extra fields the registrar plugin asks for a domain and an operation.</summary>
/// <param name="Domain">The fully-qualified domain name the operation is about.</param>
/// <param name="Operation">The one operation about to run: register, transfer or contact change.</param>
/// <param name="Holder">The kind of registrant, which decides the fields of a zone that asks different ones of a person and an organisation; any when the caller does not say.</param>
public sealed record GetDomainFieldsQuery(
    string Domain,
    PluginDomainOperations Operation,
    PluginDomainHolder Holder = PluginDomainHolder.Any);
