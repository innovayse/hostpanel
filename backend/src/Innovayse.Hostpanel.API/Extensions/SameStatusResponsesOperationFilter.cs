using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Innovayse.Hostpanel.API.Extensions;

/// <summary>
/// Describes every body an action can answer with under one status, as a <c>oneOf</c>.
/// </summary>
/// <remarks>
/// An action may declare the same status more than once, with a different type each time, when one
/// status carries two outcomes: a sign-in answers 200 with a token or with a second factor
/// challenge. The API explorer keeps one declaration per status before Swashbuckle sees any, so
/// without this filter the other body is missing from the document. The declarations are read from
/// the attributes of the action instead.
/// </remarks>
public sealed class SameStatusResponsesOperationFilter : IOperationFilter
{
    /// <summary>Replaces the schema of each status that is declared with several types by a <c>oneOf</c> of all of them.</summary>
    /// <param name="operation">The operation being described.</param>
    /// <param name="context">The action, its declared responses and the schema generator.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var shared = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<ProducesResponseTypeAttribute>()
            .Where(declared => declared.Type != typeof(void))
            .GroupBy(declared => declared.StatusCode.ToString(CultureInfo.InvariantCulture))
            .Select(group => (Status: group.Key, Types: group.Select(declared => declared.Type).Distinct().ToList()))
            .Where(group => group.Types.Count > 1);

        foreach (var (status, types) in shared)
        {
            if (!operation.Responses.TryGetValue(status, out var response))
            {
                continue;
            }

            var schemas = types
                .Select(type => context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository))
                .ToList();

            foreach (var content in response.Content.Values)
            {
                content.Schema = new OpenApiSchema { OneOf = schemas };
            }
        }
    }
}
