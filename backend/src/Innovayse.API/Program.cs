using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Innovayse.API;
using Innovayse.API.Domains;
using Innovayse.Domain.Auth;
using Innovayse.Infrastructure;
using Innovayse.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using Serilog;
using Wolverine;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog — skip in Testing environment to avoid ReloadableLogger.Freeze() conflicts
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Host.UseSerilog((ctx, services, config) =>
            config.ReadFrom.Configuration(ctx.Configuration)
                  .ReadFrom.Services(services)
                  .Enrich.FromLogContext());
    }

    // CORS
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    // JWT Authentication
    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                // Map the long-form ClaimTypes.Role URI so [Authorize(Roles = "...")] works correctly.
                // Without this, the JWT middleware reads "role" (short name) but our tokens use the full URI.
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
            };

            // Reject tokens issued before a password change
            opts.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var cache = context.HttpContext.RequestServices.GetRequiredService<TokenRevocationCache>();
                    var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                    // ASP.NET may map "iat" to its long-form URI or keep it as-is — check both
                    var iatClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Iat)?.Value
                        ?? context.Principal?.FindFirst("iat")?.Value;

                    if (userId is not null && iatClaim is not null && long.TryParse(iatClaim, out var iatUnix))
                    {
                        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iatUnix);
                        if (cache.IsRevoked(userId, issuedAt))
                        {
                            context.Fail("Token has been revoked due to a password change.");
                        }
                    }

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(opts =>
    {
        opts.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
        opts.AddPolicy("ResellerOrAdmin", p => p.RequireRole(Roles.Admin, Roles.Reseller));
    });

    // MVC + OpenAPI
    builder.Services.AddMemoryCache();
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
            opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((doc, ctx, ct) =>
        {
            doc.Info = new()
            {
                Title = "Innovayse API",
                Version = "v1",
                Description = "Innovayse hosting panel API — WHMCS replacement built with ASP.NET Core 8",
                Contact = new() { Name = "Innovayse", Email = "support@innovayse.com" },
            };
            doc.Components ??= new();
            doc.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
            doc.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter JWT token (without 'Bearer' prefix)",
            };
            return Task.CompletedTask;
        });
        options.AddOperationTransformer((operation, context, ct) =>
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                }] = [],
            });
            return Task.CompletedTask;
        });
    });

    // Wolverine
    builder.Host.UseWolverine(opts =>
    {
        opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
        opts.Discovery.IncludeAssembly(typeof(Innovayse.Application.Auth.Commands.Register.RegisterHandler).Assembly);
    });

    // Domain scheduled jobs — daily expiry check (09:00 UTC) and auto-renew (10:00 UTC)
    builder.Services.AddHostedService<DomainScheduledJobsStartup>();

    // Infrastructure
    using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
    builder.Services.AddInfrastructure(builder.Configuration, bootstrapLoggerFactory);


    var app = builder.Build();

    // Seed roles — skip in Testing environment (migrations run after host starts via factory)
    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { Roles.Admin, Roles.Reseller, Roles.Client })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Dev seed — populate test data in Development
        if (app.Environment.IsDevelopment())
        {
            var seeder = ActivatorUtilities.CreateInstance<Innovayse.Infrastructure.Persistence.DevDataSeeder>(scope.ServiceProvider);
            await seeder.SeedAsync();
        }
    }

    if (!app.Environment.IsEnvironment("Testing"))
    {
        app.UseSerilogRequestLogging();
    }

    app.UseMiddleware<ExceptionMiddleware>();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Innovayse API";
        options.Theme = ScalarTheme.Purple;
        options.DefaultHttpClient = new(ScalarTarget.Shell, ScalarClient.Curl);
    });

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Entry point partial class — required for WebApplicationFactory in integration tests.</summary>
public partial class Program;
