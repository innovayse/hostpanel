namespace Innovayse.Application.Migration.Queries.GetMigrationLogs;

using Innovayse.Application.Migration.Common;
using Innovayse.Application.Migration.Extensions;
using Innovayse.Application.Resources;
using Innovayse.Domain.Migration;
using Innovayse.Domain.Migration.Interfaces;
using Microsoft.Extensions.Localization;

/// <summary>Handles <see cref="GetMigrationLogsQuery"/>.</summary>
/// <remarks>
/// <para>
/// The two filters arrive as free strings off the query string, and this handler is the only
/// thing that checks them: by this project's convention a query never carries a FluentValidation
/// validator, so there is no middleware in front of it to refuse a bad value first. A validator
/// is therefore not the fix available here, and the guard has to live in the handler.
/// </para>
/// <para>
/// They were read with <c>Enum.Parse</c>, which answers a mistyped <c>?action=</c> with an
/// <see cref="ArgumentException"/> — and <c>ExceptionMiddleware</c> deliberately has no arm for
/// that type, because a blanket one would swallow the <see cref="ArgumentNullException"/> and
/// <see cref="ArgumentOutOfRangeException"/> that derive from it and remove the only place a
/// genuine programmer error is logged. So a caller's typo fell through to the catch-all and came
/// back as a 500. <c>TryParse</c> plus a localised refusal makes it the 400 it always was,
/// without touching that decision.
/// </para>
/// </remarks>
/// <param name="logRepo">Migration log repository.</param>
/// <param name="localizer">
/// The refusal sentence, in the culture <c>UseRequestLocalization</c> read off the request. The
/// handler resolves it itself because the middleware cannot guess which resource key the text of
/// a plain <see cref="InvalidOperationException"/> came from, and must not string-match to find
/// out.
/// </param>
public sealed class GetMigrationLogsHandler(
    IMigrationLogRepository logRepo,
    IStringLocalizer<ValidationMessages> localizer)
{
    /// <summary>Returns a paginated, filtered list of migration log entries.</summary>
    /// <param name="query">The job id, the two optional filters, and the page to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One page of log entries together with the unpaged total.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="GetMigrationLogsQuery.Action"/> or
    /// <see cref="GetMigrationLogsQuery.EntityType"/> is present but names no member of its enum.
    /// <c>ExceptionMiddleware</c> answers it 400 with code <c>INVALID_OPERATION</c> and the
    /// sentence resolved here.
    /// </exception>
    public async Task<MigrationLogPageDto> HandleAsync(GetMigrationLogsQuery query, CancellationToken ct)
    {
        var action = ParseFilter<MigrationLogAction>(query.Action);
        var entityType = ParseFilter<MigrationEntityType>(query.EntityType);

        var (items, total) = await logRepo.ListByJobAsync(
            query.JobId, action, entityType, query.Page, query.PageSize, ct);

        return new MigrationLogPageDto(
            items.Select(l => l.ToDto()).ToList(),
            total,
            query.Page,
            query.PageSize);
    }

    /// <summary>Reads an optional enum filter off the query string.</summary>
    /// <typeparam name="TEnum">The enum the filter selects a member of.</typeparam>
    /// <param name="value">The submitted value, or <see langword="null"/> for "no filter".</param>
    /// <returns>The parsed member, or <see langword="null"/> when no filter was submitted.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a value was submitted but names no member.
    /// </exception>
    /// <remarks>
    /// <c>ignoreCase</c> preserves what <c>Enum.Parse</c> did here before, so no filter spelling
    /// that used to work stops working; the only behaviour that changes is the answer to a value
    /// that never worked at all.
    /// </remarks>
    private TEnum? ParseFilter<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        if (value is null)
        {
            return null;
        }

        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            throw new InvalidOperationException(localizer["InvalidFilterValue", value]);
        }

        return parsed;
    }
}
