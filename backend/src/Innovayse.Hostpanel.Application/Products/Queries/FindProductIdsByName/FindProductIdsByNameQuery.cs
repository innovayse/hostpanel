namespace Innovayse.Hostpanel.Application.Products.Queries.FindProductIdsByName;

/// <summary>
/// Asks which products have a name containing the text, so another module can search its rows by the product they
/// belong to without reading the Products table itself.
/// </summary>
/// <param name="Search">The text to look for in the product names, in any letter case; a blank text matches nothing.</param>
public sealed record FindProductIdsByNameQuery(string Search);
