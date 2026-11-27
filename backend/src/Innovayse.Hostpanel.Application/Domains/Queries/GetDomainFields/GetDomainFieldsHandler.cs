using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetDomainFields;

/// <summary>
/// Answers which extra fields the registrar plugin of a domain's extension asks for the operation, so the screens can render
/// them. A domain no registrar plugin asks anything for answers an empty list, never a refusal.
/// </summary>
public sealed class GetDomainFieldsHandler
{
    /// <summary>Reads the fields the registrar plugin's manifest declares for a domain.</summary>
    private readonly DomainFieldReader _fields;

    /// <summary>Initializes the handler.</summary>
    /// <param name="fields">Reads the fields the registrar plugin's manifest declares for a domain.</param>
    public GetDomainFieldsHandler(DomainFieldReader fields)
    {
        _fields = fields;
    }

    /// <summary>Handles <see cref="GetDomainFieldsQuery"/>.</summary>
    /// <param name="query">The domain, the operation and the kind of registrant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fields in declared order, texts in the caller's language; empty when none apply.</returns>
    public async Task<Result<IReadOnlyList<DomainFieldDto>>> HandleAsync(GetDomainFieldsQuery query, CancellationToken ct)
    {
        var fields = await _fields.DescribeAsync(query.Domain.Trim(), null, query.Holder, query.Operation, ct);
        return Result.Success(fields);
    }
}
