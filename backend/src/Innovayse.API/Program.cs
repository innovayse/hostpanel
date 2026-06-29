using Innovayse.API;
using Innovayse.API.Billing;
using Innovayse.API.Domains;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Domain.Auth;
using Innovayse.Infrastructure;
using Innovayse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
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

    // SSO Authentication — validate JWTs issued by the Innovayse SSO (OpenIddict)
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.Authority = builder.Configuration["Sso:Authority"];
            opts.Audience = builder.Configuration["Sso:ClientId"];
            opts.RequireHttpsMetadata = false;
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                NameClaimType = "sub",
                ValidateAudience = false,
            };
            opts.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var sub = context.Principal?.FindFirst("sub")?.Value;
                    var email = context.Principal?.FindFirst("email")?.Value;
                    if (sub is null || email is null) return;
                    var firstName = context.Principal?.FindFirst("given_name")?.Value ?? string.Empty;
                    var lastName = context.Principal?.FindFirst("family_name")?.Value ?? string.Empty;
                    var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                    await userService.ProvisionSsoUserAsync(sub, email, firstName, lastName, context.HttpContext.RequestAborted);

                    // Add local Identity roles to the principal so [Authorize(Roles=...)] works
                    var localUser = await userService.FindBySsoSubjectAsync(sub, context.HttpContext.RequestAborted);
                    if (localUser is not null)
                    {
                        var roles = await userService.GetRolesAsync(localUser.Value.Id, context.HttpContext.RequestAborted);
                        var identity = (System.Security.Claims.ClaimsIdentity)context.Principal!.Identity!;
                        foreach (var role in roles)
                            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
                    }
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
        opts.Discovery.IncludeAssembly(typeof(Innovayse.Application.Clients.Commands.AcceptInvitation.AcceptInvitationCommand).Assembly);
    });

    // Domain scheduled jobs — daily expiry check (09:00 UTC) and auto-renew (10:00 UTC)
    builder.Services.AddHostedService<DomainScheduledJobsStartup>();

    // Billing scheduled jobs — daily billable items cron processing (06:00 UTC)
    builder.Services.AddHostedService<BillingScheduledJobsStartup>();

    // Infrastructure
    using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
    builder.Services.AddInfrastructure(builder.Configuration, bootstrapLoggerFactory);


    var app = builder.Build();

    // Auto-apply EF Core migrations in Development only
    if (app.Environment.IsDevelopment())
    {
        using var migrScope = app.Services.CreateScope();
        var dbCtx = migrScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbCtx.Database.MigrateAsync();
    }

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
    app.UseStaticFiles();
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
