namespace Innovayse.Hostpanel.Application.Common.Extensions;

/// <summary>
/// What a search box holds, prepared once: the escaped pattern and, for a whole number, the id it names. The query
/// reads these as members of this object, so the database receives them as parameters and never as literals in the SQL.
/// </summary>
/// <param name="Pattern">The lower-cased, escaped "contains" pattern, see <see cref="LikePattern.Contains"/>.</param>
/// <param name="Id">The id the term names when it is a whole number; otherwise <see langword="null"/>.</param>
internal sealed record TextSearchTerm(string Pattern, int? Id);
