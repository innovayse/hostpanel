using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Common.Extensions;

/// <summary>
/// The one way a list applies the text a person typed into its search box: a case-insensitive "contains" match
/// over the columns the list names, ahead of ordering and paging. The term is data, not a pattern: its <c>%</c>,
/// <c>_</c> and escape characters match themselves (<see cref="LikePattern"/>). A blank term leaves the query as it is. A row that has text per language (a child collection) is searched by the overload that takes the children.
/// </summary>
public static class TextSearchExtensions
{
    /// <summary><c>string.ToLower()</c>, which the provider translates to the lower-casing the pattern is built for.</summary>
    private static readonly MethodInfo ToLowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)
        ?? throw new InvalidOperationException("string.ToLower() is missing.");

    /// <summary><c>EF.Functions.Like(value, pattern, escape)</c>, the provider-agnostic match this layer may reference.</summary>
    private static readonly MethodInfo LikeMethod = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])
        ?? throw new InvalidOperationException("DbFunctions.Like(value, pattern, escape) is missing.");

    /// <summary>Keeps the rows where any of the columns contains the search text, in any letter case.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="source">The query to narrow.</param>
    /// <param name="term">What the person typed; blank means no search.</param>
    /// <param name="columns">The text to look in, one selector each; a computed value such as "First Last" is a selector too.</param>
    /// <returns>The query narrowed by the search, or <paramref name="source"/> when the term is blank.</returns>
    public static IQueryable<T> Search<T>(
        this IQueryable<T> source, string? term, params Expression<Func<T, string?>>[] columns)
    {
        return source.SearchCore(term, idColumn: null, columns);
    }

    /// <summary>
    /// Keeps the rows where any of the columns contains the search text, in any letter case, or whose id is the
    /// term when the term is a whole number, so a staff member can type an id straight into the search box.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="source">The query to narrow.</param>
    /// <param name="term">What the person typed; blank means no search.</param>
    /// <param name="idColumn">Selects the row's id.</param>
    /// <param name="columns">The text to look in, one selector each.</param>
    /// <returns>The query narrowed by the search, or <paramref name="source"/> when the term is blank.</returns>
    public static IQueryable<T> SearchIncludingId<T>(
        this IQueryable<T> source, string? term, Expression<Func<T, int>> idColumn, params Expression<Func<T, string?>>[] columns)
    {
        ArgumentNullException.ThrowIfNull(idColumn);

        return source.SearchCore(term, idColumn, columns);
    }

    /// <summary>
    /// Keeps the rows that have at least one child whose columns contain the search text, in any letter case: a row with
    /// text per language is found by what it says in any of them. The match is an <c>EXISTS</c> over the children, so a
    /// row that matches in several of them is still listed once.
    /// </summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <typeparam name="TChild">The type of the rows' children (for example a translation).</typeparam>
    /// <param name="source">The query to narrow.</param>
    /// <param name="term">What the person typed; blank means no search.</param>
    /// <param name="children">Selects the children of a row.</param>
    /// <param name="columns">The text to look in on a child, one selector each.</param>
    /// <returns>The query narrowed by the search, or <paramref name="source"/> when the term is blank.</returns>
    public static IQueryable<T> Search<T, TChild>(
        this IQueryable<T> source,
        string? term,
        Expression<Func<T, IEnumerable<TChild>>> children,
        params Expression<Func<TChild, string?>>[] columns)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(columns);

        if (string.IsNullOrWhiteSpace(term))
        {
            return source;
        }

        var holder = Expression.Constant(new TextSearchTerm(LikePattern.Contains(term.Trim()), null));
        var child = Expression.Parameter(typeof(TChild), "child");
        var matchesChild = LikeAnyOf(child, holder, columns) ?? Expression.Constant(false);
        var row = Expression.Parameter(typeof(T), "row");
        var childrenOfRow = new ParameterReplacer(children.Parameters[0], row).Visit(children.Body);
        var any = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Any),
            [typeof(TChild)],
            childrenOfRow,
            Expression.Lambda<Func<TChild, bool>>(matchesChild, child));

        return source.Where(Expression.Lambda<Func<T, bool>>(any, row));
    }

    /// <summary>Builds and applies the search predicate shared by both entry points.</summary>
    /// <typeparam name="T">The row type.</typeparam>
    /// <param name="source">The query to narrow.</param>
    /// <param name="term">What the person typed; blank means no search.</param>
    /// <param name="idColumn">Selects the row's id, or <see langword="null"/> when the search does not look at ids.</param>
    /// <param name="columns">The text to look in, one selector each.</param>
    /// <returns>The query narrowed by the search, or <paramref name="source"/> when the term is blank.</returns>
    private static IQueryable<T> SearchCore<T>(
        this IQueryable<T> source, string? term, Expression<Func<T, int>>? idColumn, Expression<Func<T, string?>>[] columns)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(columns);

        if (string.IsNullOrWhiteSpace(term))
        {
            return source;
        }

        var trimmed = term.Trim();

        // The id is parsed here, outside the query, so the SQL compares a plain parameter.
        int? id = idColumn is not null && int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        var holder = Expression.Constant(new TextSearchTerm(LikePattern.Contains(trimmed), id));
        var row = Expression.Parameter(typeof(T), "row");
        var predicate = LikeAnyOf(row, holder, columns);

        if (idColumn is not null)
        {
            var rowId = new ParameterReplacer(idColumn.Parameters[0], row).Visit(idColumn.Body);
            var sameId = Expression.Equal(Expression.Convert(rowId, typeof(int?)), Expression.Property(holder, nameof(TextSearchTerm.Id)));
            predicate = predicate is null ? sameId : Expression.OrElse(sameId, predicate);
        }

        // No column and no id column: there is nothing to look in, so the search cannot match anything.
        return source.Where(Expression.Lambda<Func<T, bool>>(predicate ?? Expression.Constant(false), row));
    }

    /// <summary>Builds "any of the columns of the row contains the term": one case-insensitive <c>LIKE</c> per column, joined by <c>OR</c>.</summary>
    /// <typeparam name="TRow">The row type the column selectors read.</typeparam>
    /// <param name="row">The parameter of the lambda the predicate becomes the body of; every selector is rewritten to read it.</param>
    /// <param name="holder">The prepared search term, whose pattern the <c>LIKE</c> compares with.</param>
    /// <param name="columns">The text to look in, one selector each.</param>
    /// <returns>The joined predicate, or <see langword="null"/> when no column is given.</returns>
    private static Expression? LikeAnyOf<TRow>(
        ParameterExpression row, ConstantExpression holder, Expression<Func<TRow, string?>>[] columns)
    {
        var functions = Expression.Property(null, typeof(EF), nameof(EF.Functions));

        Expression? predicate = null;
        foreach (var column in columns)
        {
            var value = new ParameterReplacer(column.Parameters[0], row).Visit(column.Body);
            var like = Expression.Call(
                LikeMethod,
                functions,
                Expression.Call(value, ToLowerMethod),
                Expression.Property(holder, nameof(TextSearchTerm.Pattern)),
                Expression.Constant(LikePattern.EscapeCharacter));
            predicate = predicate is null ? like : Expression.OrElse(predicate, like);
        }

        return predicate;
    }
}
