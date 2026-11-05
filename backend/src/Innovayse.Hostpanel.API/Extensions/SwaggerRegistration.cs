using System.Reflection;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;

namespace Innovayse.Hostpanel.API.Extensions;

/// <summary>
/// The generated OpenAPI document: Swashbuckle over the controllers, described by the XML docs of the
/// API, Application and Shared layers, served only where a developer or a test reads it.
/// </summary>
/// <remarks>
/// Never on a deployed host: the document hands whoever asks the full shape of the surface, every
/// route and every refusal. A developer reads it from a local run instead.
/// </remarks>
public static class SwaggerRegistration
{
    /// <summary>The one document's name, which is also its URL segment.</summary>
    public const string DocumentName = "v1";

    /// <summary>Where the JSON document is served.</summary>
    public const string DocumentPath = "/swagger/v1/swagger.json";

    /// <summary>The tag on anonymous storefront and probe operations.</summary>
    public const string PublicTag = "Public";

    /// <summary>The tag on sign-in, session and account operations.</summary>
    public const string AuthTag = "Auth";

    /// <summary>The tag on operations a signed-in client user calls.</summary>
    public const string ClientTag = "Client";

    /// <summary>The tag on operations any signed-in user may call, whatever their role.</summary>
    public const string MemberTag = "Member";

    /// <summary>The tag on staff operations.</summary>
    public const string AdminTag = "Admin";

    /// <summary>The route prefix every sign-in and session operation sits under.</summary>
    private const string AuthRoutePrefix = "api/auth";

    /// <summary>The security scheme id of the session cookie.</summary>
    private const string SessionCookieScheme = "SessionCookie";

    /// <summary>The security scheme id of the bearer token.</summary>
    private const string JwtBearerSchemeId = "Bearer";

    /// <summary>Registers the document generator.</summary>
    /// <param name="services">The container.</param>
    /// <returns>The container, for chaining.</returns>
    public static IServiceCollection AddHostpanelSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "Hostpanel API",
                Version = DocumentName,
                Description = "Self-hosted hosting management API.",
            });

            options.EnableAnnotations();
            options.TagActionsBy(description => [TagFor(description)]);

            // Two operations may share a CLR type name in different modules; the full name keeps
            // their schemas apart instead of failing the whole document on the first collision.
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));

            foreach (var assembly in new[] { typeof(Program).Assembly, typeof(HostpanelErrorCodes).Assembly, typeof(ErrorResponse).Assembly })
            {
                var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            }

            options.AddSecurityDefinition(SessionCookieScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Cookie,
                Name = LocalSessionCookie.Name,
                Description = "The signed-in session cookie the admin panel and the client area carry.",
            });
            options.AddSecurityDefinition(JwtBearerSchemeId, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = $"A session token sent as \"{BearerScheme.HeaderPrefix}<token>\".",
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [Reference(SessionCookieScheme)] = [],
                [Reference(JwtBearerSchemeId)] = [],
            });
        });

        return services;
    }

    /// <summary>Serves the document and its UI in Development and Testing, and nowhere else.</summary>
    /// <param name="app">The application.</param>
    /// <returns>The application, for chaining.</returns>
    public static WebApplication UseHostpanelSwagger(this WebApplication app)
    {
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment(EnvironmentNames.Testing))
        {
            app.UseSwagger();
            app.UseSwaggerUI(options => options.SwaggerEndpoint(DocumentPath, "Hostpanel API v1"));
        }

        return app;
    }

    /// <summary>Picks the area tag of one operation from its route and its authorization.</summary>
    /// <param name="description">The operation as the API explorer sees it.</param>
    /// <returns>The tag.</returns>
    private static string TagFor(ApiDescription description)
    {
        var metadata = description.ActionDescriptor.EndpointMetadata;
        var route = description.RelativePath ?? string.Empty;

        if (route.StartsWith(AuthRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthTag;
        }

        if (metadata.OfType<IAllowAnonymous>().Any())
        {
            return PublicTag;
        }

        var policies = metadata.OfType<IAuthorizeData>().Select(data => data.Policy);
        if (policies.Contains(Policies.Client))
        {
            return ClientTag;
        }

        if (policies.Contains(Policies.Member))
        {
            return MemberTag;
        }

        return AdminTag;
    }

    /// <summary>A reference to a declared security scheme, for a requirement.</summary>
    /// <param name="schemeId">The scheme id.</param>
    /// <returns>The reference.</returns>
    private static OpenApiSecurityScheme Reference(string schemeId)
    {
        return new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = schemeId },
        };
    }
}
