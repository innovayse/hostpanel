using Innovayse.API;
using Innovayse.API.Billing;
using Innovayse.API.Domains;
using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Billing.Options;
using Innovayse.Application.Common.Options;
using Innovayse.Domain.Auth;
using Innovayse.Infrastructure;
using Innovayse.Infrastructure.Auth;
using Innovayse.Infrastructure.Persistence;
using Innovayse.Auth;
using Innovayse.Auth.Endpoints;
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

    // Application-layer settings, bound here because IConfiguration stops at the composition
    // root: below it a handler states what it needs as a typed options class and nothing reads
    // a settings key by string.
    builder.Services.AddOptions<BillingOptions>()
        .Bind(builder.Configuration.GetSection(BillingOptions.SectionName))
        .Validate(
            o => o.DefaultCurrency.Length == 3,
            $"{BillingOptions.SectionName}:{nameof(BillingOptions.DefaultCurrency)} must be a "
                + "three-letter ISO 4217 alpha code.")
        .ValidateOnStart();

    // Not a section of its own. A payer may only be handed back to an origin the web edge
    // already trusts, so the list is the CORS one read just above rather than a second copy
    // that could drift from it -- see GatewayReturnUrlOptions for why it carries no SectionName.
    builder.Services.Configure<GatewayReturnUrlOptions>(o => o.AllowedOrigins = allowedOrigins);

    // ClientBaseUrl and DefaultLocale are bare top-level keys, never sections, so they are read
    // by key here and left at the option class's default when the deployment sets neither.
    var clientBaseUrl = builder.Configuration[ClientPortalOptions.ConfigurationKey];
    builder.Services.AddOptions<ClientPortalOptions>()
        .Configure(o =>
        {
            if (!string.IsNullOrWhiteSpace(clientBaseUrl))
            {
                o.BaseUrl = clientBaseUrl;
            }
        })
        .Validate(
            o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _),
            $"{ClientPortalOptions.ConfigurationKey} must be an absolute URL including the scheme, "
                + "e.g. https://client.example.com.")
        .ValidateOnStart();

    var defaultLocale = builder.Configuration[LocaleOptions.ConfigurationKey];
    builder.Services.AddOptions<LocaleOptions>()
        .Configure(o =>
        {
            if (!string.IsNullOrWhiteSpace(defaultLocale))
            {
                o.DefaultLocale = defaultLocale;
            }
        })
        .Validate(
            o => o.DefaultLocale.Length is 2 or 5,
            $"{LocaleOptions.ConfigurationKey} must be a locale code such as en or en-US.")
        .ValidateOnStart();

    var isLocalMode = AuthMode.IsLocal(builder.Configuration["Auth:Mode"]);

    // The scheme that stands in front of the real ones under SSO mode. See where it is
    // registered, below AddInnovayseAuth, for what it is for.
    const string SmartAuthScheme = "InnovayseSmart";

    // JwtTokenService is always registered — admin panel uses local auth regardless of mode
    builder.Services.AddSingleton<Innovayse.API.Auth.JwtTokenService>();

    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? Innovayse.API.Auth.JwtTokenService.DevSecretFallback;

    if (isLocalMode)
    {
        // Local-only mode
        if (jwtSecret.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.RequireHttpsMetadata = false;
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? Innovayse.API.Auth.JwtTokenService.DefaultIssuer,
                    ValidAudience = builder.Configuration["Jwt:Audience"] ?? Innovayse.API.Auth.JwtTokenService.DefaultAudience,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = "sub",
                };
            });
    }
    else
    {
        // SSO mode with local JWT fallback — admin panel uses local tokens,
        // client panel uses SSO tokens. Both are accepted.
        var localJwtIssuer = builder.Configuration["Jwt:Issuer"] ?? Innovayse.API.Auth.JwtTokenService.DefaultIssuer;
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.Authority = builder.Configuration["Sso:Authority"];
                opts.Audience = builder.Configuration["Sso:ClientId"];
                opts.RequireHttpsMetadata = false;
                opts.MapInboundClaims = false;
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = "sub",
                    ValidateAudience = false,
                };
                // If the JWT was issued by our local server, forward to LocalJwt scheme
                opts.ForwardDefaultSelector = context =>
                {
                    var auth = context.Request.Headers.Authorization.FirstOrDefault();
                    if (auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var token = auth["Bearer ".Length..];
                        try
                        {
                            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                            var jwt = handler.ReadJwtToken(token);
                            if (jwt.Issuer == localJwtIssuer) return "LocalJwt";
                        }
                        catch { /* not a valid JWT — let default scheme handle */ }
                    }
                    return null; // use default (SSO) scheme
                };
                opts.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        // The subject the SSO issued is the identifier this product uses,
                        // so there is nothing to map it onto. This used to provision a
                        // local copy of the user and swap NameIdentifier to that copy's
                        // id; the copy was written once and never updated, so a name or
                        // address changed in the SSO never reached here.
                        var sub = context.Principal?.FindFirst("sub")?.Value;
                        if (sub is null) return;

                        var identity = (System.Security.Claims.ClaimsIdentity)context.Principal!.Identity!;
                        var roleStore = context.HttpContext.RequestServices
                            .GetRequiredService<Innovayse.Domain.Auth.Interfaces.ISubjectRoleStore>();

                        foreach (var role in await roleStore.GetRolesAsync(sub, context.HttpContext.RequestAborted))
                            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
                    },
                };
            })
            .AddJwtBearer("LocalJwt", opts =>
            {
                opts.RequireHttpsMetadata = false;
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
                    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? Innovayse.API.Auth.JwtTokenService.DefaultIssuer,
                    ValidAudience = builder.Configuration["Jwt:Audience"] ?? Innovayse.API.Auth.JwtTokenService.DefaultAudience,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = "sub",
                };
            });
    }

    if (!isLocalMode)
    {
        // Cookie sessions, the third way in — and the only one a browser uses now.
        // The admin SPA held an access AND refresh token in localStorage, where any
        // script on the page could read a 30-day credential; with this the API runs
        // the OIDC exchange itself and the browser holds an opaque httpOnly cookie.
        //
        // Bearer stays for the machines: the client portal's Nuxt server calls this
        // API with an SSO token, and standalone deployments keep LocalJwt. That is
        // why this is an additional scheme rather than a replacement.
        builder.Services.AddInnovayseAuth(builder.Configuration);

        // …except that registering it made it the default scheme, which is not what
        // "additional" means. Everything below turns on the difference between the
        // default POLICY and the default SCHEME:
        //
        //   [Authorize]                     uses DefaultPolicy, which names all three
        //                                   schemes below and therefore worked.
        //   [Authorize(Roles = "Client")]   does not. Naming a role makes MVC build a
        //                                   policy on the spot, and that policy carries
        //                                   no schemes, so it falls back to the default
        //                                   authenticate scheme — the cookie.
        //
        // So every one of the 85 role-annotated endpoints stopped reading the
        // Authorization header. A caller presenting a perfectly good SSO token was
        // challenged for a cookie it does not have and got a bare 401, with no
        // WWW-Authenticate to say why. That is the whole client portal: its Nuxt server
        // calls this API with a bearer token, so /api/me/* answered 401 to every
        // request and the customer's dashboard rendered empty.
        //
        // A policy scheme restores the intent. It authenticates nothing itself; it
        // picks the real scheme per request, so a bearer token is read as a bearer
        // token whether or not the attribute happened to name a role.
        builder.Services.AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = SmartAuthScheme;
                opts.DefaultChallengeScheme = SmartAuthScheme;
            })
            .AddPolicyScheme(SmartAuthScheme, "Bearer when one is offered, cookie otherwise", opts =>
            {
                opts.ForwardDefaultSelector = context =>
                    context.Request.Headers.Authorization.FirstOrDefault()
                        ?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                            // The bearer handler has its own selector that sends locally
                            // issued tokens on to LocalJwt, so this only has to choose
                            // between "a token" and "a cookie".
                            ? JwtBearerDefaults.AuthenticationScheme
                            : global::Innovayse.Auth.CookieSessionHandler.SchemeName;
            });

        // The cookie principal carries the SSO's claims; the roles live in this
        // database. The transformation maps one onto the other.
        builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation,
            Innovayse.API.Auth.SsoSessionClaimsTransformation>();
    }

    builder.Services.AddAuthorization(opts =>
    {
        if (!isLocalMode)
        {
            // Accept tokens from either SSO or local JWT scheme
            opts.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme, "LocalJwt",
                global::Innovayse.Auth.CookieSessionHandler.SchemeName)
                .RequireAuthenticatedUser()
                .Build();
        }
        opts.AddPolicy("AdminOnly", p =>
        {
            if (!isLocalMode)
                p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "LocalJwt",
                    global::Innovayse.Auth.CookieSessionHandler.SchemeName);
            p.RequireRole(Roles.Admin);
        });
        opts.AddPolicy("ResellerOrAdmin", p =>
        {
            if (!isLocalMode)
                p.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, "LocalJwt",
                    global::Innovayse.Auth.CookieSessionHandler.SchemeName);
            p.RequireRole(Roles.Admin, Roles.Reseller);
        });
    });

    // MVC + OpenAPI
    builder.Services.AddMemoryCache();
    builder.Services.AddControllers(opts =>
        {
            // Every 201 Created in this API is built with CreatedAtAction(nameof(GetAsync), …),
            // and MVC trims the "Async" suffix from action names by default — so the name never
            // matched and the framework threw "No route matches the supplied values". The request
            // had already succeeded by then: the row was written, then the response failed, and
            // the caller saw a 400 for a creation that worked. An admin clicking again got a
            // duplicate.
            //
            // Five controllers do this — TldConfigs, Slides, ServerGroups, Clients and
            // EmailTemplates — so the fix belongs here rather than in each of them. No action
            // name is referenced as a string anywhere, so nothing depends on the trimmed form.
            opts.SuppressAsyncSuffixInActionNames = false;
        })
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

    // Wolverine logs "Invocation of {Message} failed!" at Error, with a stack trace, for every
    // exception a handler throws — unconditionally, before its own error-handling policies get a
    // say, and there is no knob in WolverineFx 5.31.0 that changes that. A staff identity with no
    // client row is not a fault, so four of those stack traces per client-dashboard load were
    // making a healthy platform read as a failing one and would eventually bury a real error.
    // This drops exactly that line for exactly those refusals; see the filter for why nothing
    // else can be lost. Serilog's ReadFrom.Services() above collects it from the container.
    builder.Services.AddSingleton<Serilog.Core.ILogEventFilter, ControlFlowExceptionLogFilter>();

    // Domain scheduled jobs — daily expiry check (09:00 UTC) and auto-renew (10:00 UTC)
    builder.Services.AddHostedService<DomainScheduledJobsStartup>();

    // Billing scheduled jobs — daily billable items cron processing (06:00 UTC)
    builder.Services.AddHostedService<BillingScheduledJobsStartup>();

    // Infrastructure
    using var bootstrapLoggerFactory = LoggerFactory.Create(b => b.AddConsole());
    builder.Services.AddInfrastructure(builder.Configuration, bootstrapLoggerFactory);


    var app = builder.Build();

    // Apply EF Core migrations on startup, in every environment except Testing —
    // the test factory runs them itself once the host is up.
    //
    // This used to be Development-only, which quietly made Development the only
    // environment a deployed host could run: name it anything else and the schema
    // simply stopped following the code, with nothing in the log to say so. Nobody
    // was going to notice until a migration was missing. The SSO has always migrated
    // this way; hostpanel is the one that did not.
    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var migrScope = app.Services.CreateScope();
        var dbCtx = migrScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbCtx.Database.MigrateAsync();
    }

    // Seed roles — skip in Testing environment (migrations run after host starts via factory)
    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var scope = app.Services.CreateScope();

        // Identity's own role table, and only where this deployment owns its users. A
        // deployment whose people live in the SSO does not register Identity at all, so
        // RoleManager is not there to resolve — asking for it with GetRequiredService
        // threw before the API had finished starting, and the container never came up.
        //
        // Nothing is lost by skipping it. What a person is allowed to do is decided by
        // subject_roles in both modes; AspNetRoles is scaffolding for the local
        // UserManager, which an SSO-owned deployment never calls.
        if (isLocalMode)
        {
            var roleManager = scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var role in new[] { Roles.Admin, Roles.Reseller, Roles.Client })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        // Support departments — every environment, for the same reason as the roles
        // above: the ticket form cannot be submitted without at least one.
        await Innovayse.Application.Support.Services.DefaultDepartmentsSeeder.EnsureSeededAsync(
            scope.ServiceProvider.GetRequiredService<Innovayse.Domain.Support.Interfaces.IDepartmentRepository>(),
            scope.ServiceProvider.GetRequiredService<Innovayse.Application.Common.IUnitOfWork>());

        // Storefront settings — also every environment. SettingsController can update
        // an existing key but cannot create one, so a key that was never seeded is a
        // key no operator can ever set from the admin panel.
        await Innovayse.Application.Admin.Services.PortalSettingsSeeder.EnsureSeededAsync(
            scope.ServiceProvider.GetRequiredService<Innovayse.Domain.Settings.Interfaces.ISettingRepository>(),
            scope.ServiceProvider.GetRequiredService<Innovayse.Application.Common.IUnitOfWork>());

        // Dev seed — populate test data in Development. Local mode only: the seeder
        // creates its people through Identity's UserManager, which an SSO-owned
        // deployment does not register, and there would be nowhere local to put them
        // anyway. Running it there threw on construction and took the API down with it.
        if (app.Environment.IsDevelopment() && isLocalMode)
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
    if (!isLocalMode)
    {
        // Forwarded headers, the CSRF check, authentication and authorisation, in the
        // order they have to be in. Bearer callers pass the CSRF check untouched — a
        // bearer header is already something a cross-site page cannot attach.
        app.UseInnovayseAuth();
        // GET /api/auth/login|callback and POST /api/auth/logout, for the admin SPA.
        // Not /api/auth/me: this product's own AuthController answers it with the
        // roles the database assigns, which the token cannot carry.
        app.MapInnovayseAuth(mapMe: false);
    }
    else
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Innovayse API";
        options.Theme = ScalarTheme.Purple;
        options.DefaultHttpClient = new(ScalarTarget.Shell, ScalarClient.Curl);
    });

    // Liveness for the container healthcheck. Deliberately not a database probe: this
    // answers "is the process still serving HTTP", which is the question a restart
    // policy can act on. Without it the container had no healthcheck at all and Docker
    // reported "Up" through a crash loop — main-api spent minutes restarting on an
    // unresolvable database host while `docker ps` showed nothing wrong.
    app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");

    // Rethrow, don't just log. Swallowing it made Main return normally, so the process
    // exited 0 — a container whose startup failed reported success and the orchestrator
    // had no reason to restart or alert. In the integration tests the same swallow turned
    // every startup error into "The entry point exited without ever building an IHost",
    // which names nothing about the actual cause.
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Entry point partial class — required for WebApplicationFactory in integration tests.</summary>
public partial class Program;
