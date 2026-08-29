# hostpanel — Project Rules

> ## ⚠ REQUIRED FOR EVERYTHING: DOCUMENTATION
>
> **Doc comments are mandatory on every file, type, member, component and composable — no
> exceptions.** XML docs in C# ([xml-docs.md](xml-docs.md)), JSDoc on the frontend
> ([jsdoc.md](jsdoc.md)), plus inline comments wherever the *reason* for the code is not
> visible from the code. Undocumented code is unfinished code.
>
> `backend/src/Directory.Build.props` sets `GenerateDocumentationFile` **and**
> `TreatWarningsAsErrors`, so an undocumented public member under `src/` is a build failure,
> not a review comment. `backend/tests/` inherits neither — documentation there is a rule the
> compiler is not enforcing.

**What it is:** the self-hosted hosting-management platform — an open-source WHMCS
alternative. Clients order services, manage domains, pay invoices and open tickets; staff run
products, billing, provisioning and integrations. Production is `host.innovayse.com`; a demo
runs at `hostpanel.innovayse.com` with published credentials in `README.md`.

**This is a public, separately licensed repository.** BUSL 1.1, converting to GPL v2 on
2028-05-15. `origin` is **github.com/innovayse/hostpanel**, not GitLab. Anything written here
is read by people who have never seen the rest of the workspace, and the repo must build from
a clone of itself alone — that is why `Innovayse.Auth` is consumed as a package rather than a
project reference, and why `backend/nuget.config` restricts `auditSources` to nuget.org.

**Read first:** the six rules files beside this one — [clean-architecture.md](clean-architecture.md),
[csharp-style.md](csharp-style.md), [dotnet-format.md](dotnet-format.md),
[xml-docs.md](xml-docs.md), [jsdoc.md](jsdoc.md), [vuejs-style.md](vuejs-style.md) — then this
file. This repository is a submodule and keeps its **own** copies; the workspace's `rules/`
folder is not vendored here, and editing anything under it means committing in another
repository.

## Where this project's rules win over the workspace — read this before your first review

The workspace's copies of `clean-architecture.md` and `csharp-style.md` differ from the ones
in this folder, and the differences have already been reported as defects in code that is
correct. **The files beside this one are the authority here.** Four points, stated plainly:

- **Primary constructors on controllers and handlers are correct.**
  `public sealed class OrdersController(IMessageBus bus) : ControllerBase`. The workspace copy
  of clean-architecture.md requires an explicit `private readonly` field and a written
  constructor; [csharp-style.md](csharp-style.md) in this folder lists exactly that shape
  under **AVOID**. All 54 controllers use the primary constructor. Do not "fix" them.
- **`*Request` records under `API/<Feature>/Requests/` are sanctioned.** Ten such folders
  exist and the controller maps the request onto the command itself. The workspace forbids a
  request record beside the command and binds the command directly; that rule is not this
  project's.
- **There is no `Result<T>` and no `BaseApiController`.** Handlers return their value
  directly (`Task<PlaceOrderResultDto>`, `Task<int>`), controllers return `IActionResult` or
  `ActionResult<T>` and **do** name status codes, and every controller derives from
  `ControllerBase`. Refusals travel as exceptions: `ExceptionMiddleware` in
  `backend/src/Innovayse.API/ExceptionMiddleware.cs` is the single place a domain exception
  becomes a status and an error code. A refusal the frontend must act on gets its own
  exception type there — a bare `InvalidOperationException` lands in the unclassified 400 bin
  (`INVALID_OPERATION`) and the caller has only the sentence to go on.
- **The API sends the finished sentence, in the caller's language. The frontend does not
  translate codes.** The workspace's `api-driven-frontend.md` closes its error contract with
  "One mapping table. Codes translate to human text in a single `utils/*ErrorMessages.ts`".
  **This project deliberately does not follow that**, and `client/utils/portalErrorMessages.ts`
  is gone.

  The rule assumes a backend that answers in one language and a frontend that owns every
  translation. That is not this product. The portal ships **three** languages —
  `client/locales/{en,ru,hy}` — and the table only ever covered five codes, so a Russian or
  Armenian customer read English for every other refusal while the table's existence made it
  look solved. The backend now owns the wording for all three:

  - Sentences live in `backend/src/Innovayse.Application/Resources/ValidationMessages.resx` and its
    `.ru.` / `.hy.` siblings. `Resources/ValidationMessages.cs` is the marker type
    `IStringLocalizer<ValidationMessages>` names; **its full name is the resource base name**, so
    moving or renaming either half makes every lookup answer with the key instead of a sentence,
    silently. `ValidationMessagesLocalizationTests` exists because nothing else would notice.
  - The culture comes from `Accept-Language`, through `app.UseRequestLocalization(...)` in
    `Program.cs`, **above `app.UseMiddleware<ExceptionMiddleware>()`** — the other way round the
    sentence would be resolved outside the culture the request asked for. Supported languages
    come from `LocaleOptions.SupportedLocales`, beside the resources they select.
  - A refusal still carries `{ error, code }` and the route is unchanged. `error` is the
    sentence, now localised; `code` is still the machine-readable string, and it is still the
    **only** thing a page may branch on.

  **The frontend keeps the codes and loses the wording.** `client/utils/apiError.ts` replaces
  the deleted table with two readers — `apiErrorMessage(err)` for the sentence,
  `apiErrorCode(err)` for the branch — and a `PortalErrorCode` constant per code something
  actually branches on. `stores/client.ts` holds `clientProfileMissing` *and*
  `clientProfileMessage`, exactly as innovayse-sso's store holds `auth.errorCode` beside
  `auth.error`. Branching on a sentence was always fragile; now it is fragile in three languages.

  **Two sentences are still written in the frontend, and only these two.** The offline fallback
  in `apiErrorMessage` — a request that got no answer has no body to quote — and
  `client.acceptInvite.signInRequired`, because the refusal that carries
  `INVITE_SIGN_IN_REQUIRED` is written by the BFF (`server/api/portal/auth/accept-invite.post.ts`)
  before any call to the C# API is possible, so there is no API wording to read. Both are
  `api-driven-frontend.md`'s own two sanctioned exceptions, which this project does still follow.

  **A refusal a person reads gets its sentence from the resources, not from a string literal.**
  `ExceptionMiddleware` localises the typed exceptions by the `MessageKey` constant beside their
  `Code`; a handler that throws a bare `InvalidOperationException` must resolve the sentence
  itself through an injected `IStringLocalizer<ValidationMessages>` first, because the middleware
  cannot guess which key a message text came from and must not string-match to find out.

  **No `LocalizedValidator<T>` base class was added**, though the pattern this came from has one.
  The reason has changed since this was first written: all 57 validators are now wired and running
  (see below), so their messages are no longer messages nothing reads — but they are still English
  literals, and `ExceptionMiddleware` deliberately does not put them in the response for exactly
  that reason. A rejected form gets one localised sentence and one code. The base class, plus a
  resource key per validator message, is what per-field detail in the body waits on, and it is
  now the next piece of this work rather than a file with no purpose.

- **Application ports live in the feature's own `Interfaces/` folder, not one layer-wide
  folder.** The workspace's `file-layout.md` closes its ports section with "Whatever the
  folder, it is one folder, not one per feature", and names `innovayse-sso`'s
  `Application/Abstractions/` as the precedent. **This project deliberately does not follow
  that**, for the same kind of reason the thirty-four feature-grouped repositories stay where
  they are. Three things decide it:

  - *The rule's own justification does not hold here.* `file-layout.md` argues a port lives
    layer-wide because it is "a contract the whole layer implements against" that "every
    consumer of the Application layer may need to see". Exactly two ports here are that, and
    they are **already** layer-wide: `Application/Common/IUnitOfWork.cs` and
    `Application/Common/ICurrentRequestContext.cs`. Of the fifteen in `<Feature>/Interfaces/`,
    **ten are consumed by exactly one feature** — all 44 references to
    `Reports.Interfaces` sit inside `Application/Reports/**` and `Infrastructure/Reports/`,
    and `IMigrationSource`, `IClientExportRepository`, `IAuthModeProvider`, `IJwtService`,
    `ITwoFactorService`, `IUserService` and `IPluginRegistry` never leave theirs. Three more
    reach two or three features (`IStripeService`, `IPaymentPluginResolver`,
    `IServerConnectionTester`). Only **two** come close to the rule's premise:
    `IIdentityProvider`, used from seven features, and `IUserProvisioning` from five. Those
    two are the closest call and the first thing to revisit if this is ever reopened; they
    stay for the reason below, and because their implementations are the `AUTH_MODE` pair
    under `Infrastructure/Auth/` — the switch that makes them widely called is itself an Auth
    concern.
  - *Infrastructure is feature-grouped too, so a port and its implementation already mirror
    each other.* `Application/Reports/Interfaces/IReportRepository.cs` sits opposite
    `Infrastructure/Reports/ReportRepository.cs`, and the same holds for Auth (nine
    implementations under `Infrastructure/Auth/`), Clients and Servers. Collapsing only the
    Application side into an `Abstractions/` bucket would leave every one of those ports
    feature-grouped on the Infrastructure side and layer-grouped on the Application side, which
    is worse than either consistent shape.
  - *The contract is written in the feature's own vocabulary.* `file-layout.md` says the port
    carries the whole documentation of the contract, and seven of these spell that contract
    entirely in their own feature's `Common/` DTOs — `IReportRepository`, `IDiskUsageService`
    and `ISslMonitoringService` in `Reports.Common`, `IStripeService` in `Billing.Common`,
    `ITwoFactorService` in `Auth.Common`, `IMigrationSource` in `Migration.Common`,
    `IClientExportRepository` in its own query's folder. Moving an interface out of the folder
    its every signature names is the split that costs a reader, not the one that pays.

  So: **a new port goes in its feature's `Interfaces/` folder.** A port the whole layer
  genuinely uses goes in `Application/Common/`, where the two that qualify already are. This is
  not debt and does not need a plan — `Domain/<Feature>/Interfaces/` already holds forty
  interfaces in the same shape, so "the feature's `Interfaces/` folder" stays one rule for
  finding any interface in this backend rather than two.


## Architecture — Clean Architecture, four layers plus a plugin SDK

`backend/Innovayse.Backend.sln`, 14 projects:

```
backend/src/
  Innovayse.Domain/               entities, events, repository interfaces. No dependencies.
  Innovayse.Application/          use cases (Commands/, Queries/), DTOs, ports, options
  Innovayse.Infrastructure/       AppDbContext, migrations, repositories, Integrations/
  Innovayse.API/                  controllers, ExceptionMiddleware, composition root
  Innovayse.SDK/                  the plugin contract third parties compile against
  Innovayse.Providers.CWP/        control-panel provisioning provider
  Innovayse.Providers.CWP7/       control-panel provisioning provider
  Innovayse.Providers.Inecobank/  payment plugin (net8.0 — the API is net9.0)
backend/tests/                    Domain, Application, Infrastructure, CWP, Inecobank, Integration
client/                           Nuxt 4 client portal + public site (yarn)
admin/                            Vite + Vue 3 admin SPA (npm)
docker/                           api / client / admin / nginx Dockerfiles + nginx.conf
```

**Plugins are loaded reflectively, not linked.** `PluginLoader.DiscoverAndRegister` scans
`AppContext.BaseDirectory/plugins`, so `Innovayse.API.csproj` references
`Innovayse.Providers.Inecobank` with `ReferenceOutputAssembly=false` purely for build order and
stages exactly two files — the provider assembly and its `plugin.json`. Never widen that to a
`**\*.*` glob: the provider's `bin` also holds a build-time copy of `Innovayse.SDK.dll`, and
`Assembly.LoadFrom` over it would give the process two distinct `IPaymentPlugin` types with the
same name, after which every cast in `PaymentPluginResolver` fails. The CWP providers are
deliberately **not** staged, so they cannot activate at runtime by accident.

## CQRS — Wolverine, conforming in shape

Wolverine 5.31.0. `Program.cs` registers discovery over the API and Application assemblies;
handlers are never registered by hand and never called directly. Commands and queries live
one folder per use case under `Application/<Feature>/{Commands,Queries}/<UseCase>/`.

### Validators exist on commands only, and they now run

**This entry said the opposite until recently, and the old version is still quoted from
memory.** The pipeline is wired. Read the two calls below before you decide a rule is dormant.

- All 57 `*Validator.cs` files sit under a `Commands/` path. There is **not one validator on a
  query**, in any feature. That is the convention; do not read it as an oversight — and it
  matters more now than it did, because a query's inputs are checked by nothing but its handler.
- FluentValidation 11 is referenced (`FluentValidation.AspNetCore` in `Innovayse.API.csproj`)
  **and wired**. `Program.cs` calls
  `AddValidatorsFromAssemblyContaining<AcceptInvitationCommand>()` — the whole Application
  assembly, registered Scoped, which is load-bearing: `PlaceOrderValidator` takes the Scoped
  `ICurrentRequestContext` and a Singleton registration would be a captive dependency serving
  every checkout the first caller's identity — and the `UseWolverine(...)` block calls
  `opts.UseFluentValidation()`. **All 57 validators run**, as middleware in front of any handler
  whose message has one. A message with no validator passes straight through, so handlers that
  already check for themselves are untouched and their checks are now duplicated rather than
  replaced.
- A failure throws FluentValidation's `ValidationException`, which `ExceptionMiddleware` catches
  and answers **400** with the same `{ error, code }` body as every other refusal, code
  `VALIDATION_FAILED`. It is not a 500.

**What still is not done, and is the reason this is not simply "fixed":**

- **Validator messages are English literals.** `WithMessage("Type must be 'Immediate' or
  'EndOfBillingPeriod'.")` and its fifty-odd siblings are string literals in the Application
  assembly, not keys in `ValidationMessages*.resx`. The per-field failures are therefore **logged and
  not sent** — `ExceptionMiddleware` writes them to the log line and puts the localised
  `ValidationFailed` sentence in `error`. So a rejected form gets **one localised sentence and
  one code**, and the caller cannot tell the reader which field was wrong. That is the deliberate
  trade: one sentence a Russian or Armenian customer can read, rather than per-field detail only
  an English speaker can. It is not the end state.
- **There is no `LocalizedValidator<T>` base class**, and no resource keys for validator messages
  at all. The pattern this came from has both. Adding per-field detail to the response means
  doing that first — every message becomes a key, the base class resolves it through
  `IStringLocalizer<ValidationMessages>`, and only then can the failures travel in the body without
  answering two of the three shipped languages in English.

So a rule written in a validator **is** enforced, and a check duplicated in a handler is now
belt-and-braces rather than the only guard. But a rule whose *wording* the reader needs still has
to be thrown from the handler as a typed exception with a `MessageKey`, because a validator
cannot yet say anything the caller will see.

(`innovayse-main`, which forked from this backend, wired the same two calls first; this repo
followed it rather than inventing a second shape.)

## Stack

.NET 9 · Wolverine 5.31.0 · EF Core 9 + Npgsql 9 · PostgreSQL 17 · ASP.NET Identity (local
mode) + JWT bearer · `Innovayse.Auth` 1.0.0 (cookie session for the admin SPA, sso mode) ·
Serilog · Scalar + Swashbuckle · Mapster · Stripe.net · MailKit · Otp.NET · Fluid.Core ·
DnsClient · Redis (sessions + data-protection key ring) · RabbitMQ · xUnit + FluentAssertions +
Moq + Testcontainers.PostgreSql 4.0.0 · Nuxt 4.3 / Vue 3.5 / Pinia / Tailwind / vitest (client,
**yarn**) · Vite + Vue 3.5 / Tailwind 4 (admin, **npm**).

Central package management: versions live once in `backend/Directory.Packages.props`; a
`.csproj` carries `<PackageReference Include="…" />` with no `Version`.

## Build, test, run

```bash
./dev up hostpanel                # from the workspace root, when working inside the workspace
```

To build without touching the dev stack — **run docker through PowerShell, not Git Bash**,
which rewrites `-w /src` into `C:/Program Files/Git/src` and exits 125:

```powershell
docker run --rm `
  -v "C:\Users\Dell\Desktop\Projects\innovayse\innovayse-workspace\hostpanel\backend:/src" `
  -v "C:\Users\Dell\.nuget\packages:/root/.nuget/packages" `
  -w /src mcr.microsoft.com/dotnet/sdk:9.0 bash -lc "dotnet build Innovayse.Backend.sln"
```

**A cold full solution build takes about eight minutes** — 7m43s measured, 14 projects, 0
warnings. It prints nothing for minutes at a stretch and **appears to stop after
`Innovayse.Infrastructure`**; that project is simply the slow one, and `Innovayse.API`, the
test projects and the rest land minutes later. Only treat it as hung well past ten minutes.
Three agents in a row abandoned this build as stalled and fell back to a narrower one, losing
the cross-project signal it exists for.

The host package cache covers the private feed for a local build: `nuget.config`'s
`packageSourceMapping` sends only `Innovayse.Auth` to the GitLab registry, and that package is
already cached. Where a credential *is* needed it is
`NuGetPackageSourceCredentials_innovayse=Username=<gitlab-account>;Password=<PAT>` — the exact
spelling NuGet reads; `gitlab-ci-token` as the username is for CI job tokens only and answers
401 with a personal token. The feed is addressed by **numeric project id**, not by the encoded
namespace path, which answers 400 to everything and reports as NU1301.

**`Innovayse.Integration.Tests` needs Testcontainers**, and that means the docker socket plus
both `-e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` and
`--add-host=host.docker.internal:host-gateway`. Without them the resource reaper cannot start
and every test in the suite fails with a stack trace that looks nothing like a networking
problem. **Do not pass `--no-build` to the integration suite** — it silently produces no output
and exits 0 whether or not the assembly is there. The three unit suites are fine with it.

Containers: `hostpanel-api` (5148), `hostpanel-client` (3000, published 3001),
`hostpanel-admin`, `hostpanel-db`, plus `hostpanel-redis`, `hostpanel-rabbitmq`,
`hostpanel-mailhog` locally. Services address each other by **network alias**
(`hostpanel-api`), never by the bare `api` — that name also belongs to `innovayse-main`'s
backend on the shared network, and Docker's DNS round-robins between the two.

## CI is on GitLab. Pull requests are on GitHub. Both halves are load-bearing.

`origin` is GitHub; merges happen there. `.github/workflows/mirror-to-gitlab.yml` pushes
**only `main` and `develop`** to `gitlab.com/innovayse/hostpanel`, whose `.gitlab-ci.yml`
starts the pipeline. The consequences are not obvious and cost time every few months:

- **A feature branch never gets a pipeline.** Nothing is mirrored, so nothing builds or tests
  it. A pull request into `develop` shows no checks because there are none to show, not
  because they passed.
- The one GitHub job that *does* run on a pull request is
  `.github/workflows/branch-flow.yml`, a required status check on `main` that refuses any
  source branch other than `develop` or `hotfix/*`. It enforces what neither platform has a
  setting for.
- Tests, image builds and both deploys live in `.gitlab-ci.yml`. `deploy:staging` runs on
  `develop` against LocalServer; `deploy:prod` is **manual** on `main` against ProdServer.
- `.gitlab-ci.yml` inlines its templates. This repo cannot see the workspace's shared
  `ci/templates.yml`, so the hidden jobs are copied in. Change one here and the workspace
  copy does not move with it.

**`deploy:prod` builds the images on the production host** — it consumes no registry images
and declares `needs: []`, because the build jobs want a `builder` runner this project's prod
host does not have. It dumps the database with `pg_dump -Fc` into `backups/` **before**
anything is rebuilt and fails the deploy if the dump fails: the API applies pending EF
migrations on startup, so `up -d` is the moment the schema changes, and a deploy that cannot be
undone should not start. The last ten dumps are kept.

`nginx` is deliberately left out of both deploys' service lists. It publishes 80/443, which on
these hosts belong to the shared Nginx Proxy Manager; naming it would fail the whole deploy.
Its own nginx exists for a standalone install.

## Configuration and the two switches that decide what this product *is*

- **`AUTH_MODE` (`sso` | `local`).** `sso` authenticates against innovayse-sso and reads every
  account out of it through `/api/service/*` with `SSO_SERVICE_KEY`; `local` is the standalone
  open-source path where hostpanel owns its own user table and registers no SSO scheme at all.
  Only this product implements both, and most "why is there a second way to do this" in the
  auth code is this switch. Under `sso` the API refuses to start without
  `CLIENT_HOSTPANEL_SECRET` rather than come up and hand every visitor an anonymous session.
- **`ASPNETCORE_ENVIRONMENT` is set by the deploy job, never by a host's `.env`.**
  `docker-compose.yml` defaults it to `Development`, which is right for a laptop and was what
  deployed hosts silently inherited. A Development API serves developer exception pages and
  treats an absent `EncryptionKey` as allowed rather than fatal — which is how server
  credentials and client-service passwords reached the database in plain text.
- **Settings bind into a sealed `*Options` class naming its own `SectionName`** —
  `BillingOptions`, `CPanelOptions`, `NamecheapOptions`, `NameAmOptions`, `StripeOptions`, each
  in an `Options/` folder inside the feature or integration that owns it.

## The frontend: three templates, one live header, and one env-var trap

`client/` is Nuxt 4 with SSR on the public site and `ssr: false` route rules for `/client/**`,
`/cart/**` and `/checkout/**`.

**The header you are looking at is almost certainly not `components/layout/Header.vue`.**
`layouts/default.vue` renders `<component :is="header" />`, and `header` comes from
`useTemplate().slot('header')`, which resolves through `templates/registry.ts`. There are three
templates — `aurora`, `nova`, `classic` — and the active one is decided by, in order: the
operator's `portal.template` admin setting, then `NUXT_PUBLIC_PORTAL_TEMPLATE`, then the
built-in default **`aurora`**; anything unrecognised falls back to `aurora` rather than
rendering nothing. `components/layout/Header.vue` is reached only through
`templates/classic/layout/Header.vue`, a two-line wrapper — so on a default deployment it is
live code that nothing renders. Editing it and reloading changes nothing, and that has cost
real time. Start from `templates/<active>/layout/Header.vue`.

`nova` deliberately reuses `aurora`'s `Domains.vue` and `Checkout.vue` — the checkout is the
ordering path and a second copy is a second place for a payment bug to live. If a shared aurora
page grows a new section import, add it to `AURORA_SHARED_COMMERCE` in `nuxt.config.ts` or nova
starts downloading the whole `template-aurora` chunk.

**Runtime config in a production image.** A production Nitro build freezes `runtimeConfig`
defaults — `nuxt.config.ts` is not re-evaluated at container start as it is in dev. Nitro's own
escape hatch is reading `NUXT_<KEY>` / `NUXT_PUBLIC_<KEY>` at runtime and overriding the frozen
default, so `docker-compose.prod.yml` spells every one of them that way. **The plain names used
in `docker-compose.yml` are silently ignored on a deployed host** — that is how
`NUXT_PUBLIC_PORTAL_TEMPLATE` and the apps launcher stayed permanently at their build-time
values on production with no `.env` edit able to change them.

The genuine build-time cases are the ones `nuxt.config.ts` reads from `process.env` **outside**
`runtimeConfig`, and only those. The app-launcher `<script src="…/widget/header.js">` tag is
built from `process.env.NUXT_PUBLIC_MAIN_URL` inside the static `app.head.script` array, so it
is passed as a **build arg** in `docker/client.Dockerfile` *and* repeated as runtime env; the
same applies to `site.url` and the esbuild `drop`. Adding a new `process.env` read outside
`runtimeConfig` adds a value nobody can change without a rebuild — put it in `runtimeConfig`
instead unless it truly cannot go there.

That widget is served by **innovayse-main**, not by this product. It must be HTTPS on an HTTPS
deployment or the browser blocks it as mixed content and the client dashboard's hydration
hangs.

## Migration debt — recorded, not endorsed

The workspace standard is [clean-architecture.md](clean-architecture.md). These are the places
this product is not on it. **Never spread one of them**, and never restructure as a side effect
of a feature.

- **Every repository implementation is now in its feature's folder — the four strays are
  gone.** `ServerRepository` and `ServerGroupRepository` moved out of
  `Infrastructure/Repositories/` into `Infrastructure/Servers/`, beside `ServerConnectionTester`;
  `MigrationJobRepository` and `MigrationLogRepository` moved out of
  `Infrastructure/Persistence/Repositories/` into `Infrastructure/Integrations/Migration/`,
  beside `MigrationSourceClient`. Both emptied folders went with them.

  The workspace's `file-layout.md` says repository implementations go in
  `Infrastructure/Persistence/`. **This project deliberately does not follow that.** Keeping a
  repository in its feature's own folder is the same grouping `Integrations/<Provider>/` and
  `<Feature>/Options/` already use here. Collapsing the rest into `Persistence/` would empty
  seven folders down to a lone `Configurations/` child — `Audit`, `Clients`, `Orders`,
  `Products`, `Settings`, `Slides` and `Support` — while the other eleven keep code either way,
  so it buys a flat `Persistence/Repositories/` at the cost of scattering seven features.
  **A new repository goes in its feature's folder.**

  That is the honest version of the argument. An earlier draft of this entry said "a dozen
  folders"; it is seven, and the count is worth checking rather than repeating.

  **The Migration pair cannot live in an `Infrastructure/Migration/` folder**, which is the name
  every other layer uses (`Domain/Migration/`, `Application/Migration/`). A namespace
  `Innovayse.Infrastructure.Migration` is a member called `Migration` of
  `Innovayse.Infrastructure`, and every scaffolded EF migration in
  `Innovayse.Infrastructure.Migrations` derives from a bare `Migration`. Namespace-member lookup
  walks the enclosing namespaces before it ever reads a `using`, so it finds the folder rather
  than EF Core's base class and fails **every** migration file with CS0118. That has already
  cost one agent a build. `Integrations/Migration/` is where this feature's Infrastructure code
  already was, and it is a fair home rather than a dodge: the whole migration subsystem exists
  to pull from a foreign install, and `MigrationJob` / `MigrationLog` are that integration's own
  bookkeeping. It is also the only option that neither invents a name the feature is not called
  anywhere else nor splits its Infrastructure code across two folders. Do not "correct" it to
  `Infrastructure/Migration/`.
- **`Infrastructure/Persistence/Configurations/` still holds fifteen EF configurations belonging
  to eleven different features** — `ServerConfiguration`, `MigrationJobConfiguration`,
  `InvoiceItemConfiguration`, `KbCategoryConfiguration`, `SslCheckConfiguration` and the rest —
  while thirty-odd others sit in `Infrastructure/<Feature>/Configurations/`. "The repository sits
  beside the `Configurations/` that maps the same entities" is therefore the intent here, not yet
  the fact, and for the four repositories just moved it is now false in the other direction. This
  is the same debt the repositories had and it is untouched; move a configuration when its
  feature is being worked on anyway, never as a sweep.
- **`LocalAuthController` resolves `IUserService` from an injected `IServiceProvider`, and that
  stays.** It is the only service-locator call in the API layer, so it reads as debt, but every
  alternative examined is worse. `IUserService` is registered **only** inside the
  `if (ownsItsUsers)` branch of `Infrastructure/DependencyInjection.cs`; as a constructor
  parameter it would make every route on the controller fail to resolve under `sso` mode —
  including the seven that answer 404 on purpose, which would answer 500 instead. Moving the
  actions down into Wolverine handlers is not a free swap either: **no Application handler in
  this repository injects `IUserService`** (its only other consumer is `LocalTwoFactorService`,
  registered in that same branch), so whether Wolverine's startup code generation tolerates a
  handler with an unregistered dependency in `sso` mode is unproven here — and being wrong about
  it takes the API down at boot rather than at one endpoint. Make the local-mode registrations
  unconditional behind the same `IAuthModeProvider` the controller already uses before reopening
  this. The same reasoning is in the controller's own `<remarks>`.
- **`ClientRegisteredIntegrationEvent` carries five parameters nothing fills.**
  `LocalAuthController.RegisterAsync` publishes four arguments and stops, so `IpAddress`,
  `UserAgent`, `DeviceType`, `OperatingSystem` and `Browser` all arrive null and `Client.Create`
  writes five mapped, migrated columns from them. This is half of a removal: the
  `UserAgentParser` that filled the last three had zero consumers after local self-registration
  was rewritten and has now been deleted, but the fields have not. Finish it by capturing all
  five from the request in `RegisterAsync`, not by trimming the record — cutting three of five
  equally-empty fields is an arbitrary line and the columns would then be unreachable. Wolverine
  runs with discovery only — no broker, no publishing rules — so despite the name this event
  never leaves the process and no outside consumer can be reading those fields.
- **Two repository interfaces are in Application, not Domain** —
  `Application/Clients/Interfaces/` and `Application/Reports/Interfaces/` — while the other
  forty are under `Domain/<Feature>/Interfaces/` as the rule says.
- **`MigrationPullWorker` is 1,516 lines in the Application layer.** It no longer touches
  HTTP: the transport moved behind `Application/Migration/Interfaces/IMigrationSource.cs`,
  implemented by `Infrastructure/Integrations/Migration/MigrationSourceClient.cs`, and
  `TestMigrationConnectionHandler` folded onto the same port, so there is one way to reach a
  foreign install. `Microsoft.Extensions.Http` is gone from `Innovayse.Application.csproj` and
  nothing in that project mentions `HttpClient`.

  What is left is size, not layering — orchestration, batching and progress accounting for
  fifteen entity types in one file. Splitting it is its own piece of work; the boundary that
  made it untestable is already gone, so a fake `IMigrationSource` can now drive it without a
  web server.
- **Twelve refusal messages are wired through the localizer but still English only.** They have
  entries in `ValidationMessages.resx` and none in `ValidationMessages.ru.resx` or `ValidationMessages.hy.resx`,
  so `ResourceManager` serves the neutral file and a Russian or Armenian caller reads English for
  these and only these: `ServiceNotFound`, `CancellationAlreadyPending`, `ProductNotFound`,
  `ProductNotAvailable`, `ClientServiceNotFound`, `ServiceNotPending`, `NoEligibleServer`,
  `ProvisioningFailed`, `ServiceNoProvisioningReference`, `ServiceNoServerAssigned`,
  `ServiceNotProvisioned`. Every one of them prints an entity id or a provider's own message, and
  most read as diagnostics rather than as something a customer can act on — rewording them is part
  of translating them. **Finishing this is two resx entries per key and no code change**, which is
  the whole point of the shape: the mechanism reaches these throw sites already.

  Untouched entirely, and not in the resources at all: the `Client {id} not found.` family in
  `AddContactHandler`, `UpdateContactHandler`, `RemoveContactHandler`, `UpdateClientHandler`,
  `RemoveUserFromClientHandler` and `GetClientUsersHandler`, and every refusal thrown from the
  admin-only features (`Admin`, `Audit`, `Migration`, `Notifications`, `Orders`, `Products`,
  `Reports`, `Servers`, `Settings`, `Slides` and the staff half of `Billing`, `Domains` and
  `Support`). Roughly 230 of the 264 `throw` sites in the Application layer. They still answer in
  English, from the string literal in the handler, exactly as before — nothing regressed, nothing
  improved. A staff member reading English in an English-only admin SPA is the deliberate
  ordering, not an oversight.
- **Validator messages are English literals and are not sent to the caller.** FluentValidation is
  wired now, so this is no longer the "nothing runs them" debt it was; what is left is that
  `WithMessage(...)` text lives in the Application assembly rather than in `ValidationMessages*.resx`,
  so `ExceptionMiddleware` logs the per-field failures and answers with one localised
  `ValidationFailed` sentence. Finishing it needs a `LocalizedValidator<T>` base class and a
  resource key per message — both still absent. See the CQRS section above.

## Where things go

| Kind | Path |
|---|---|
| HTTP endpoint | `backend/src/Innovayse.API/<Feature>/<Name>Controller.cs` |
| Request record | `backend/src/Innovayse.API/<Feature>/Requests/` |
| Exception → status mapping | `backend/src/Innovayse.API/ExceptionMiddleware.cs` — nowhere else |
| Sentence a person reads for a refusal | `backend/src/Innovayse.Application/Resources/ValidationMessages*.resx` |
| Command / query + handler | `backend/src/Innovayse.Application/<Feature>/{Commands,Queries}/<UseCase>/` |
| DTO shared by several use cases in a feature | `backend/src/Innovayse.Application/<Feature>/Common/` |
| DTO used by exactly one use case | that use case's own folder, beside its command or query |
| Options class | `.../<Feature or Integration>/Options/<Name>Options.cs` |
| Extension class | `Extensions/` inside the layer or feature that owns it |
| Port the Application declares | `.../Innovayse.Application/<Feature>/Interfaces/` — `Application/Common/` only if the whole layer uses it |
| Entity, domain event, repository interface | `backend/src/Innovayse.Domain/<Feature>/` |
| EF configuration, migration, repository impl | `backend/src/Innovayse.Infrastructure/` |
| External service client | `backend/src/Innovayse.Infrastructure/Integrations/<Provider>/` |
| Portal page / section | `client/templates/<template>/pages/`, `.../sections/` |
| Shared component | `client/components/`, `client/components/ui/` |
| API call | `client/composables/apis/` |
| Nitro BFF route | `client/server/api/portal/` |
| Exported TS type | `client/types/` |
| Admin screen | `admin/src/` |
| Plan / spec | `docs/superpowers/plans/`, `docs/superpowers/specs/` |

## Gotchas

- **`dotnet watch` in `hostpanel-api` dies with "An item with the same key has already been
  added".** `DOTNET_USE_POLLING_FILE_WATCHER=1` makes the container enumerate the bind mount on
  a loop, and enumerating a directory being written to occasionally returns an entry twice over
  the Docker Desktop mount. It is a mount artefact, not your build. Recover with
  `stop` then `up -d`, never `restart`. The API itself serves fine; only hot reload is lost.
  This is also why `Innovayse.API.csproj` carries `<Watch Remove="logs/**/*" />` — and
  `<Content Remove>`, so a publish does not ship yesterday's logs.
- **Wolverine logs `Invocation of {Message} failed!` at Error with a stack trace for every
  handler exception**, before its own policies get a say, and 5.31.0 has no knob for it. A
  staff identity with no client row is not a fault, and four such traces per dashboard load
  made a healthy platform read as a failing one. `ControlFlowExceptionLogFilter` drops exactly
  those lines; extend the filter rather than removing it.
- **Migrations are applied on startup in every environment except `Testing`.** This used to be
  Development-only, which quietly made Development the only environment whose schema followed
  the code.
- **Role seeding is skipped when this deployment does not own its users.** Under `sso` mode
  Identity is not registered, so `GetRequiredService<RoleManager<…>>` threw before the API
  finished starting and the container never came up.
- **`Innovayse.Providers.Inecobank` targets `net8.0`** while everything else targets `net9.0`;
  the MSBuild staging target pins that TFM in its copy paths. Retargeting the provider means
  editing those paths.
