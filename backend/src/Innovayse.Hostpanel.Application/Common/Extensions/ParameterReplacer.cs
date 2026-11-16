using System.Linq.Expressions;

namespace Innovayse.Hostpanel.Application.Common.Extensions;

/// <summary>
/// Rewrites an expression so it reads another lambda parameter. It lets selectors written as separate lambdas
/// (<c>c =&gt; c.FirstName</c>, <c>c =&gt; c.LastName</c>) be combined into one predicate over a single row parameter.
/// </summary>
/// <param name="from">The parameter the expression reads.</param>
/// <param name="to">The parameter it should read instead.</param>
internal sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
{
    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node == from ? to : base.VisitParameter(node);
    }
}
