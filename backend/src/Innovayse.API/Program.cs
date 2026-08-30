using FluentValidation;
using Innovayse.API;
using Innovayse.API.Billing;
using Innovayse.API.Domains;
using Innovayse.API.RateLimiting.Extensions;
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
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using Serilog;
using Wolverine;
using Wolverine.FluentValidation;

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

    // The refusal sentences in Innovayse.Application/Resources/ValidationMessages*.resx. No
    // ResourcesPath: the resx files sit in a Resources/ folder whose name is already part of the
    // ValidationMessages marker type's namespace, and setting it would make the factory look for
    // Resources.Resources.ValidationMessages instead.
    builder.Services.AddLocalization();

    var isLocalMode = AuthMode.IsLocal(builder.Configuration["Auth:Mode"]);

    // The scheme that stands in front of the real ones under SSO mode. See where it is
    // registered, below AddInnovayseAuth, for what it is for.
    const string SmartAuthScheme = "InnovayseSmart";

    // The token signer itself is registered by AddInnovayseInfrastructure, in both modes — the
    // admin panel uses local tokens whatever the mode is. What stays here is the composition
    // root's own reading of the same three keys, for the validators built below: the signer and
    // the validators have to agree, so both fall back to the constants the implementation names.
    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? Innovayse.Infrastructure.Auth.JwtTokenService.DevSecretFallback;

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
                    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? Innovayse.Infrastructure.Auth.JwtTokenService.DefaultIssuer,
                    ValidAudience = builder.Configuration["Jwt:Audience"] ?? Innovayse.Infrastructure.Auth.JwtTokenService.DefaultAudience,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = "sub",
                };

                // Local mode had no Events at all, so nothing here ever read subject_roles —
                // the store every grant in this product writes to. Roles came only from the
                // claims baked into the token at sign-in, which are Identity's AspNetUserRoles,
                // leaving the two stores disjoint: setup, admin-created clients and
                // guest-checkout customers all wrote a grant that authorized nothing.
                opts.Events = new JwtBearerEvents
                {
                    // The admin SPA no longer holds this token in sessionStorage, where any
                    // script injected into the page could read it. The API writes it into an
                    // httpOnly cookie instead — the same decision the SSO path already made,
                    // for the same reason — and this is the half that reads it back.
                    //
                    // The Authorization header still wins where one is present, and has to: the
                    // client portal's Nuxt server calls this API machine-to-machine with a
                    // bearer token and has no browser to hold a cookie. The cookie is a
                    // fallback for the one caller that does.
                    //
                    // Nothing about the token itself changed. Same issuer, same audience, same
                    // fifteen-minute lifetime, same validation parameters above.
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token)
                            && context.Request.Cookies.TryGetValue(
                                Innovayse.API.Auth.LocalSessionCookie.Name, out var cookieToken)
                            && !string.IsNullOrEmpty(cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = Innovayse.API.Auth.SubjectRoleClaimsEnricher.OnTokenValidated(),
                };
            });
    }
    else
    {
        // SSO mode with local JWT fallback — admin panel uses local tokens,
        // client panel uses SSO tokens. Both are accepted.
        var localJwtIssuer = builder.Configuration["Jwt:Issuer"] ?? Innovayse.Infrastructure.Auth.JwtTokenService.DefaultIssuer;
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
                // The subject the SSO issued is the identifier this product uses, so there is
                // nothing to map it onto. This used to provision a local copy of the user and
                // swap NameIdentifier to that copy's id; the copy was written once and never
                // updated, so a name or address changed in the SSO never reached here.
                //
                // The role merge itself is shared with both locally issued schemes rather than
                // written inline here — it was the only reader of subject_roles in the process,
                // which is what made that store SSO-only in practice.
                opts.Events = new JwtBearerEvents
                {
                    OnTokenValidated = Innovayse.API.Auth.SubjectRoleClaimsEnricher.OnTokenValidated(),
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
                    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? Innovayse.Infrastructure.Auth.JwtTokenService.DefaultIssuer,
                    ValidAudience = builder.Configuration["Jwt:Audience"] ?? Innovayse.Infrastructure.Auth.JwtTokenService.DefaultAudience,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = "sub",
                };

                // Same merge as the SSO scheme above. This one had no Events either, and the
                // ForwardDefaultSelector sends every token this product minted itself here —
                // so the admin panel's own credential was the one credential in SSO mode that
                // could not see a role granted through subject_roles.
                opts.Events = new JwtBearerEvents
                {
                    OnTokenValidated = Innovayse.API.Auth.SubjectRoleClaimsEnricher.OnTokenValidated(),
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

    // Every AbstractValidator<T> in the Application assembly, which is where all of them live --
    // there is not one in the API, the Domain or the Infrastructure project. Registered Scoped,
    // which is this call's default and is load-bearing rather than incidental: PlaceOrderValidator
    // takes ICurrentRequestContext, itself Scoped, so a Singleton registration here would be a
    // captive dependency that answers every checkout with the first caller's identity.
    //
    // AddValidatorsFromAssemblyContaining, not a hand-written list: a validator that is added and
    // not registered is exactly the dead file this whole change exists to stop producing.
    builder.Services.AddValidatorsFromAssemblyContaining<
        Innovayse.Application.Clients.Commands.AcceptInvitation.AcceptInvitationCommand>();

    // Wolverine
    builder.Host.UseWolverine(opts =>
    {
        opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
        opts.Discovery.IncludeAssembly(typeof(Innovayse.Application.Clients.Commands.AcceptInvitation.AcceptInvitationCommand).Assembly);

        // Validation as middleware in front of every handler whose message has a validator, so
        // no handler has to remember to ask and no rule can be written in a validator and quietly
        // never run -- which is what all 57 of them did until this line existed. A message with no
        // validator passes straight through, so the handlers that already check for themselves are
        // untouched.
        //
        // On failure the middleware throws FluentValidation's ValidationException, which
        // ExceptionMiddleware catches and turns into the same { error, code } body as every other
        // refusal on this API. It is not a 500: the request was wrong, not the server.
        opts.UseFluentValidation();
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

    // Rate limiting, and the forwarded-header handling it depends on. Registered as one call
    // because both are the web edge's reading of the same proxy chain, and a limiter whose idea
    // of "the caller" disagrees with the rest of the pipeline's is worse than none: it would
    // partition every browser onto nginx's address and let one visitor's burst refuse everybody.
    //
    // Everything is limited by a global budget whether or not its action names a policy — see
    // RateLimitingExtensions for why an opt-in scheme does not survive fifty-eight controllers.
    builder.Services.AddPlatformRateLimiting(builder.Configuration, builder.Environment);

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
        // Nothing is lost by skipping it. Every grant this product makes is written to
        // subject_roles, and SubjectRoleClaimsEnricher merges that store onto the principal
        // on every JWT scheme in both modes — so a role granted there authorizes in both.
        //
        // This comment used to say authorization was decided by subject_roles in both modes
        // full stop, and that was not true: no scheme registered under AUTH_MODE=local read
        // the store at all, so local authorization was decided solely by the claims baked
        // into the token at sign-in. It is worth stating precisely, because the two role
        // tables still are not one. AspNetRoles remains scaffolding for the local
        // UserManager, and the roles it holds also reach the principal — via the token
        // LocalAuthController mints from IUserService.GetRolesAsync. A local deployment
        // therefore authorizes on the union of the two, with subject_roles the only one a
        // grant made after sign-in can land in.
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

        // The first-run setup token, and only where this deployment owns its own people.
        //
        // POST /api/auth/setup grants the Admin role, and until this existed it granted it to
        // whichever authenticated caller asked first. Registration on a standalone install is
        // public, so on a box that is reachable before its owner has finished configuring it —
        // the normal shape of a self-hosted deployment — whoever registered and claimed first
        // owned the installation. The token moves that decision to "who can read this log",
        // which the operator can and a passer-by cannot.
        //
        // Deliberately last in this block, after DevDataSeeder: a Development box already has
        // an Admin by the time this runs, so nothing is issued and no token line is printed to
        // be mistaken for one that matters.
        //
        // Nothing happens under Auth:Mode=sso. Accounts there belong to the sign-on service, so
        // the callers who can reach an authenticated endpoint at all are already the ones the
        // operator provisioned, and that path is in production use.
        if (isLocalMode)
        {
            var setupToken = await Innovayse.Application.Auth.Services.SetupTokenSeeder.EnsureIssuedAsync(
                scope.ServiceProvider.GetRequiredService<Innovayse.Domain.Settings.Interfaces.ISettingRepository>(),
                scope.ServiceProvider.GetRequiredService<Innovayse.Domain.Auth.Interfaces.ISubjectRoleStore>(),
                scope.ServiceProvider.GetRequiredService<Innovayse.Application.Common.IUnitOfWork>());

            if (setupToken is not null)
            {
                // Warning level so it survives a production log filter, and formatted to be
                // read by a person tailing `docker logs` rather than by a sink. It is repeated
                // on every boot for as long as setup is outstanding: that is what makes a
                // restart mid-setup recoverable instead of a lockout, and the token is retired
                // the moment it is used.
                //
                // The token is a template argument rather than a concatenation so that a
                // structured sink stores it as a field — this line is a secret either way, and
                // an operator who ships logs off the box should know it is in them until setup
                // completes.
                Log.Warning(
                    "FIRST-RUN SETUP IS OUTSTANDING. Nobody holds the Admin role yet. " +
                    "Open the admin panel, create your account, and paste this setup token when " +
                    "it asks for one: {SetupToken}", setupToken);
            }
        }
    }

    if (!app.Environment.IsEnvironment("Testing"))
    {
        app.UseSerilogRequestLogging();
    }

    // Above ExceptionMiddleware, deliberately. This middleware sets CurrentUICulture from
    // Accept-Language and the one below it is what turns a refusal into a response body -- run
    // the other way round, the sentence would be resolved outside the culture the request asked
    // for and every caller would read English. Supported cultures come from the Application
    // layer's LocaleOptions so they cannot drift from the .resx files they select.
    var localeOptions = app.Services.GetRequiredService<IOptions<LocaleOptions>>().Value;
    app.UseRequestLocalization(new RequestLocalizationOptions()
        .SetDefaultCulture(localeOptions.DefaultLocale)
        .AddSupportedCultures([.. LocaleOptions.SupportedLocales])
        .AddSupportedUICultures([.. LocaleOptions.SupportedLocales]));

    app.UseMiddleware<ExceptionMiddleware>();

    // Forwarded headers first, in BOTH modes, and above everything that reads the request's
    // origin. Two things depend on it: Request.Scheme, which is otherwise Kestrel's view of the
    // plain-HTTP hop from nginx, and Connection.RemoteIpAddress, which is otherwise nginx itself
    // — and that address is what the rate limiter partitions anonymous callers on. Left to
    // resolve wrongly it would put every visitor in one bucket, so one person's burst refuses
    // everybody: the failure that looks most like the feature working.
    //
    // UseInnovayseAuth runs its own forwarded-header handling in the SSO branch below, with the
    // framework's default ForwardLimit of 1 — one entry short of this two-hop chain. Running ours
    // first resolves the address correctly and consumes the header, so its call finds nothing left
    // to process and cannot undo the result.
    app.UseForwardedHeaders();

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

        // The limiter has to sit after authentication — it partitions on the signed-in subject
        // and falls back to an address only for callers with no credential, so above
        // UseAuthentication it would see an empty User on every request and quietly degrade to
        // one bucket per address, which for the client portal is one bucket for every customer.
        //
        // Ideally it would sit BETWEEN authentication and authorisation, so that a request refused
        // by authorisation still spends budget. It cannot here: UseInnovayseAuth is one call that
        // does forwarded headers, the CSRF check, authentication and authorisation together, and
        // splitting it would mean re-running authentication or forking a published package. The
        // consequence is bounded and worth stating — a caller spraying an [Authorize] route with
        // no credential is refused by authorisation before the limiter counts them. That is cheap
        // to serve (no handler, no database), and every route where an unauthenticated flood is
        // actually expensive — the credential endpoints, the contact form, the registrar lookups
        // — is [AllowAnonymous], so authorisation does not short-circuit it and the budget applies.
        app.UseRateLimiter();
    }
    else
    {
        // Above authentication, so a cross-site forgery is refused before anything reads the
        // cookie it was hoping to ride on. The SSO branch gets the equivalent check from
        // UseInnovayseAuth; local mode had none because a bearer header in an Authorization
        // header is CSRF-safe on its own, and that stopped being the whole story the moment the
        // local session moved into a cookie. It examines only requests that actually present
        // that cookie, so bearer callers and anonymous webhooks are untouched.
        app.UseMiddleware<Innovayse.API.Auth.LocalSessionCsrfMiddleware>();

        app.UseAuthentication();

        // Between authentication and authorisation, which is where it belongs: User is populated,
        // so the per-subject partition works, and a request that authorisation is about to refuse
        // has already spent budget rather than being free to repeat.
        app.UseRateLimiter();
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
