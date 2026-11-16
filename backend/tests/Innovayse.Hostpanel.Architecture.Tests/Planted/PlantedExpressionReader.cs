using System.Linq.Expressions;
using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Architecture.Tests.Planted;

/// <summary>
/// A stand-in for a handler that reaches another module's set only inside an expression tree, the way a search
/// lambda handed to a query does, so the getter is never called but its handle is loaded.
/// </summary>
public sealed class PlantedExpressionReader
{
    /// <summary>The forbidden read the ownership rule must catch even though it sits in an expression and is never run.</summary>
    /// <returns>An expression that reads the domain set of the context.</returns>
    public Expression<Func<IHostpanelDbContext, object>> ReadsDomainsInsideAnExpression()
    {
        return context => context.Domains;
    }
}
