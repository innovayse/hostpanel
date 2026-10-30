# hostpanel — Project Rules

> ## ⚠ REQUIRED FOR EVERYTHING: DOCUMENTATION
>
> **Doc comments are mandatory on every file, type, member, component and composable — no
> exceptions.** XML docs in C# ([xml-docs.md](xml-docs.md)), JSDoc on the frontend
> ([jsdoc.md](jsdoc.md)), plus inline comments wherever the *reason* for the code is not
> visible from the code. Undocumented code is unfinished code.
>
> `backend/src/Directory.Build.props` and `backend/tests/Directory.Build.props` both set
> `GenerateDocumentationFile` **and** `TreatWarningsAsErrors`, so an undocumented public member
> under `src/` or `tests/` is a build failure, not a review comment.

**What it is:** the self-hosted hosting-management platform — open-source hosting billing and
automation. Clients order services, manage domains, pay invoices and open tickets; staff run
products, billing, provisioning and integrations. A public demo
is the same build with `Demo__Enabled=true` (`deploy:demo`); its sign-in page shows the demo accounts,
served by `GET /api/public/demo` — they are not written in `README.md`.

**This is a public, separately licensed repository.** AGPL-3.0-or-later, except the plugin SDK (`backend/src/Innovayse.SDK`), which is MIT (its own `LICENSE`, packed into the package) so a plugin may be free or paid, open or closed; `SdkPackageTests` pins the package metadata. The SDK lives in its own public repository, **github.com/innovayse/hostpanel-sdk**, and is a git submodule at that same path (`.gitmodules`): clone with `--recurse-submodules` (or run `git submodule update --init` after a plain clone), and an SDK change lands as a pull request there first, then as a submodule bump here. `origin` is **github.com/innovayse/hostpanel**, not GitLab. Anything written here
is read by people who have never seen the rest of the workspace, and the repo must build from
a clone of itself alone (with its submodule) — that is why `Innovayse.Auth` is consumed as an optional package (only in an
`InnovayseSso=true` build, see `backend/Directory.Build.props`) rather than a project reference, and why `backend/nuget.config` restricts `auditSources` to nuget.org.

**Read first:** the six rules files beside this one — [clean-architecture.md](clean-architecture.md),
[csharp-style.md](csharp-style.md), [dotnet-format.md](dotnet-format.md),
[xml-docs.md](xml-docs.md), [jsdoc.md](jsdoc.md), [vuejs-style.md](vuejs-style.md) — then this
file. This repository is a submodule and keeps its **own** copies; the workspace's `rules/`
folder is not vendored here, and editing anything under it means committing in another
repository.

## Where this project's rules differ from the workspace — read this before your first review

This backend is on the workspace shape: no primary constructors on handlers or controllers, no
`*Request` records, no exception-only refusals, no feature-local `Interfaces/` folders, no
repositories. The departures are short:

- **`LocalAuthController` follows the shape like every other controller.** It sits in
  `API/Controllers/`, derives from `BaseApiController` and each action binds a command and
  dispatches it once (`SignIn`, `TwoFactorSignIn`, `Register`, `ConfirmEmail`, `ForgotPassword`,
  `ResetPassword`, `SignOut`). Every route answers 404 unless the deployment is in local mode:
  `API/Auth/LocalModeOnlyFilter`, a resource filter, refuses before binding and before the bus, so
  `SignInHandler`, `TwoFactorSignInHandler` and `RegisterHandler` never run under `sso` mode. Their
  handler code is still generated in that mode (the image runs `codegen write` in its own auth mode with
  service location refused), so every port they take is registered in both modes: the Identity-backed
  `UserService`, `TwoFactorChallengeService` and `TwoFactorCodeGuard` in local mode, and in `sso` mode
  the stand-ins `SsoUserService`, `SsoTwoFactorChallengeService` and `SsoTwoFactorCodeGuard`
  (`Infrastructure/Auth/`), which hold no accounts, answer every read as for an unknown account, throw
  on every write and never touch Identity. `SsoModeHandlerChainTests` builds every chain in `sso` mode
  (an `InnovayseSso=true` build). A sign-in sets
  the httpOnly session cookie through `BaseApiController.HandleSession` (the handler returns an
  `ISessionGrant`, `Shared/Models/Interfaces`; `LocalSessionCookie` lives in `Shared/Infrastructure`)
  and still returns the token in the body for scripted callers, and `logout` clears the cookie
  through `HandleSessionEnded`. `HealthController` derives from `BaseApiController` with
  `[AllowAnonymous]` like the rest.
- **Anonymous public storefront feeds.** `/api/public/{faqs,locations,partners,product-groups,products,projects,testimonials}` (like `/api/public/slides` and
  `/api/public/demo` before them) are `[AllowAnonymous]` read-only queries: the storefront renders them before anyone has
  signed in, so they cannot carry the usual `[Authorize]` on every action. They stay on `BaseApiController`, dispatch one
  query, return only active, already-localised records and never expose an admin field; writes remain behind `Policies.Admin`.
- **Handler and validator naming is `<UseCase>Handler` / `<UseCase>Validator`; the product rule wins over
  workspace `cqrs.md`.** Use-case names are unique across modules (`ProvisionService` in Provisioning,
  `SetupService` in Services), one use case per state transition (`SuspendService`, `UnsuspendService`,
  `TerminateService`: every route, cron and status change dispatches them, and each checks the state
  before it asks the panel), and a DTO two use cases share lives once in `<Module>/Common/` (one
  `ProductPriceDto`, one `FileDownloadDto` in `Application/Common/` for every export and download).
- **No `*Request` types in Application, API or Domain.** Provider inputs in Domain
  are named for what they carry (`DomainRegistrationOrder`, `DomainRenewalOrder`,
  `DomainTransferOrder`, `ProvisionAccountOrder`, `SuspendAccountOrder`, `TerminateAccountOrder`);
  `NoRequestTypesTests` covers all three layers (the `CancellationRequest` entity is exempt). The wire models inside
  `Innovayse.Providers.*` and the SDK's `PaymentRequest` are third-party-facing contracts and keep their names.
- **Recurring jobs and background handlers return `Task`, not `Result`.** The crons
  (`CheckOverdueInvoicesCron`, `ProcessBillableItemsCron`, `ProcessRenewalsCron`,
  `ReconcileGatewayPaymentsCron`, `SendRenewalRemindersCron`, `SuspendOverdueServicesCron`,
  `AutoTerminateSuspendedCron`, `SyncServerAccountCountsCron`) and `ResetDemoData`,
  `CheckDomainExpiries` and `SyncPendingDomainStatuses` are scheduled messages, not dispatched by a
  caller, so nothing would receive a `Result`. So are `RunMigrationPull` (published by
  `MigrationPullQueue` once a job's queued state is saved; the job row records how the pull ended)
  and `RecoverInterruptedMigrationJobs` (a recurring job: it fails queued or running pulls whose `MigrationJobs.LastHeartbeatAt` is older than `ScheduledJobs:MigrationStaleAfterMinutes`, so a pull another instance still runs is left alone). The crons run each pass through
  `Application/Common/RecurringJobs.RunAsync`, which takes the job's lease, logs a failed pass and schedules
  the next one from the end of the pass; the log line is the record of the failure. Wrapping them in a
  `Result` nobody reads would add nothing. Every handler a controller dispatches still returns `Result`.
- **Recurring jobs run on one instance at a time and are caught up after downtime.** The `JobRuns` table
  (`JobRun` in Domain, `IJobLease` in Application, `Infrastructure/Scheduling/JobLease`) holds a lease per job:
  a pass takes it first, an instance that does not get it skips the pass and keeps its own chain, and a lease
  whose holder crashed expires. On startup `RecurringJobs.StartAsync` runs a job at once when its last success
  is older than its period, and otherwise schedules its next regular run. Every hour and interval lives in
  `ScheduledJobsOptions` (section `ScheduledJobs`), read by the startup services and the handlers alike; a
  scheduled run is in memory, so a host that is stopping ends the chain and the next start catches up.
- **Our own API contracts carry no `[JsonPropertyName]`**: the wire name is the property name in camelCase.
  A route id is overlaid on the bound command with `with { ... }`; there are no `*Body` types. The SDK ports are
  vendor-free too (`IHostingPanelApiClient`, `IHostingPanelV7ApiClient`; `NoVendorNamesTests` covers the SDK).
- **Errors are one `ErrorResponse` body, `application/problem+json`**
  (`Type`/`Title`/`Status`/`Detail`/`Errors`, camelCase on the wire), written by one serializer,
  `Shared/Infrastructure/ErrorBody.cs`, for every surface: `BaseApiController`,
  `ExceptionHandlingMiddleware`, the model-state factory, the auth-event writers and the rate
  limiter. The body carries only those members, and both frontends read `type`/`detail`; do not
  add an `error`/`code` pair beside them.
- **The API sends the finished sentence, in the caller's language; the frontend does not
  translate codes.** Sentences live in `Application/Resources/ErrorMessages*.resx` (looked up by
  error code through `IErrorMessageResolver`) and `Shared/Resources/ValidationMessages*.resx` (middleware, binders) and
  `Application/Resources/ApplicationValidationMessages*.resx` (validator sentences; the marker is named differently from the
  Shared one so the two resource sets can never share a base name),
  in en/ru/hy, with `ResourceCoverageTests` failing on a code with no sentence or a sentence with
  no code. The marker type's full name is the resource base name — moving either half makes every
  lookup answer with the key. Culture comes from the `X-Language` header only
  (`PlatformLocalizationExtensions` clears the other providers), through
  `app.UseRequestLocalization(...)` registered **above** `ExceptionHandlingMiddleware` in
  `Program.cs`. Both frontends stamp it from the active i18n locale on every call
  (`client/composables/useApi.ts`, `admin/src/composables/useApi.ts`); the Nuxt BFF reads it from
  the browser request, validates it against `AppLocale` (`client/utils/transportLanguage.ts`) and
  forwards it on every backend call, the post-refresh retry included. `client/lib/errorHandler.ts`
  (Nuxt) and `admin/src/lib/errorHandler.ts` (admin) are the frontend readers. Only two sentences
  are written in the frontend: the offline fallback (`errors.noAnswer`), and
  `client.acceptInvite.signInRequired` (refused by the Nuxt BFF before any API call exists).

## Architecture — Clean Architecture, four layers plus a plugin SDK

`backend/Innovayse.Hostpanel.sln`, 15 projects:

```
backend/src/
  SharedKernel/                      BaseEntity (DomainEvents), AuditableEntity, DomainException, IDomainEvent
  Shared/                            BaseApiController + StatusMapping, Result/Result<T>, ErrorResponse,
                                     ErrorBody, middleware, validation policy, resources, options
  Innovayse.Hostpanel.Domain/        entities, events, Enums/ per area. References the kernel only.
  Innovayse.Hostpanel.Application/   <Feature>/{Commands,Queries}/<UseCase>/, <Feature>/Common/,
                                     Common/Interfaces/ (every port), Common/Errors/, Resources/
  Innovayse.Hostpanel.Infrastructure/ AppDbContext, migrations, <Capability>/ adapters, Integrations/
  Innovayse.Hostpanel.API/           controllers, Program.cs, composition root
  Innovayse.SDK/                     the plugin contract third parties compile against (git submodule, own repository)
  Innovayse.Providers.CWP/ CWP7/ Inecobank/   provisioning and payment providers (Inecobank is net8.0)
backend/tests/                       Domain, Integration, Architecture, CWP, Inecobank
client/  admin/                    Nuxt 4 portal, Vite admin SPA — each owns its Dockerfile (backend/Dockerfile too)
docker/                              compose set (shared·dev·prod·platform), nginx/, scripts/, .env.example — docker/README.md
```

- **No repositories.** Handlers use `IHostpanelDbContext` (`Application/Common/Interfaces/`)
  directly, and the DbContext is the unit of work: a handler saves with
  `IHostpanelDbContext.SaveChangesAsync`, which also translates a concurrency conflict to
  `ConcurrencyConflictException`. There is no separate unit-of-work port.
  Domain has no `Interfaces/` folders.
- **Every application port is in `Application/Common/Interfaces/`** (`ICardPaymentGateway`,
  `IProvisioningProvider`, `IEmailSender`, `IIdentityProvider`, `IUserService`, …) and
  `PortPlacementTests` fails on one anywhere else. Adapters sit in Infrastructure under a
  capability folder (`Storage/`, `Email/`, `Integrations/<Provider>/`, `Auth/`); EF configurations sit in `Persistence/Configurations/`.
  Two documented exceptions in `PortPlacementTests`. A request-scoped adapter lives under `API/` and
  sits in the test's allow-list with what it reads (today `API/Identity/HttpCallerAccessToken`,
  the `Authorization` header, behind `ICallerAccessToken`). `Innovayse.Providers.CWP7` implements
  `IProvisioningProvider` and is not scanned: it is not a plugin, Infrastructure links it and
  `ProvisioningProviderFactory` builds it per server, so it is an Infrastructure adapter in its
  own assembly. The SDK's `IProvisioningPlugin` is only a marker with no operations, so there is
  no SDK contract it could implement instead.
- **"Test Connection" runs a live probe where a provider has one.** `IIntegrationConnectionProbe`
  (a port in `Application/Common/Interfaces/`, keyed by integration slug) is implemented per
  integration in `Infrastructure/Integrations/<Provider>/` (today `NameAmConnectionProbe`, which
  logs in with the saved credentials) and every implementation is registered in
  `DependencyInjection`; `TestIntegrationConnectionHandler` picks the one whose `Slug` matches once
  every required field is stored. A probe failure is logged and answered with a fixed sentence —
  the provider's own text never reaches the response. `NameAmClient` reads the e-mail, password,
  API URL and test mode saved on the admin Integrations page over the bound options
  (`IsConfiguredAsync`), so the page configures what requests use.
- **Enums live in `Enums/` namespaces** (`Domain/<Area>/Enums/`, `SharedKernel/Domain/Enums/`,
  `Infrastructure/Email/Enums/`); `EnumPlacementTests` enforces it.
- **Domain refusals are `DomainException(code, message)`** (abstract, in the kernel); the exception
  middleware turns an uncaught one into a body whose status comes from `StatusMapping.For(code)`
  and whose sentence is the localised one for that code. A handler that can foresee a refusal returns
  `Result.Failure` instead.
- **Plugins are loaded reflectively, not linked.** `PluginLoader.DiscoverAndRegister` scans
  `AppContext.BaseDirectory/plugins`, so `Innovayse.Hostpanel.API.csproj` references
  `Innovayse.Providers.Inecobank` with `ReferenceOutputAssembly=false` purely for build order and
  stages exactly two files — the provider assembly and its `plugin.json`. Never widen that to a
  `**\*.*` glob: the provider's `bin` also holds a build-time copy of `Innovayse.SDK.dll`, and
  `Assembly.LoadFrom` over it would give the process two distinct `IPaymentPlugin` types with the
  same name, after which every cast in `PluginActivator` fails. The CWP providers are
  deliberately **not** staged, so they cannot activate at runtime by accident.
- **Six plugin types, one activator.** The loader registers each plugin under the SDK contract of the
  type its manifest declares: provisioning, payment, registrar, identity (`IClientResolver`),
  fulfilment (`IFulfilmentPlugin`) and hosted (`IHostedPlugin`). Every plugin becomes an instance in
  one place, `Infrastructure/Plugins/PluginActivator`: it checks the loaded type really implements the
  contract, that `integration:{id}:is_enabled` is `true` and that every required manifest field is
  stored, then builds the plugin with its own configuration. The resolvers only choose:
  `PaymentPluginResolver` (`IPaymentPluginResolver`, one plugin by module id),
  `FulfilmentPluginResolver` (`IFulfilmentPluginResolver`, every enabled fulfilment plugin in id order) and
  `IdentityPluginResolver` (`IIdentityPluginResolver`, the one enabled identity plugin; two or more
  enabled throw `IdentityPluginConflictException` rather than picking one). `HostedPluginRunner` starts
  every enabled hosted plugin (manifest type `Hosted`, or any plugin that also implements
  `IHostedPlugin`) once the host has started and stops them as it begins to stop; an activation, start or
  stop that throws is logged with the plugin id and never stops the host or the other plugins. A plugin
  is enabled from the admin Integrations page, where it is listed (with its type) when its manifest sets
  `showInIntegrations`.
- **`PluginActivator` takes `IConfiguration` (documented exception to configuration.md rule 6).**
  The plugin SDK's base classes take an `IConfiguration` in their constructor, so the
  activator hands each plugin a configuration built from **its own section only**: the host's
  `integration:{id}` section with the plugin's `integration:{id}:*` settings rows layered over it
  (the rows are filtered by prefix in SQL, never the whole `Settings` table). Connection strings,
  signing keys and other integrations never reach a plugin. The keys keep their full path because
  the SDK base classes read `integration:{id}:{key}`; a relative `IConfigurationSection` would make
  every such read miss. This is the public plugin contract; typed plugin settings are planned for a
  later SDK version, and until then this is the one type below the composition root allowed to hold
  `IConfiguration` (allow-listed in `ConfigurationTests`). Nothing else may follow it.
- **Two plugin folders: built-in and uploaded (`PluginOptions`, section `Plugins`).** The build's own
  `plugins/` folder (`PluginOptions.BuiltInRoot`) holds the plugins shipped with the build; they load
  first, are listed with `isBuiltIn: true`, and cannot be removed or replaced from the admin panel
  (`PLUGIN_BUILT_IN`, 409). Uploads go to `Plugins:InstallPath` (default `plugins-installed` beside
  the app; mount it on a volume to keep uploads across redeploys), which `PluginOptionsValidator`
  keeps apart from the built-in folder. All plugin file system work sits behind `IPluginPackageStore`
  (`Infrastructure/Plugins/PluginPackageStore`); Application never touches the disk. An upload is
  streamed to a staging folder (`.staging`, which fails the plugin id rule and so is never loaded),
  checked, unpacked there and moved into place with one rename, so a refusal leaves nothing behind.
  The checks: the plugin id rule (`Domain/Plugins/PluginIdentifier`: lower-case letters, digits and
  single hyphens, at most 64 characters) and a resolved path that stays a direct child of the upload
  folder; every entry inside the plugin's own folder; `Plugins:MaxEntryCount` and
  `Plugins:MaxUncompressedBytes` (counted while unpacking; `PLUGIN_ARCHIVE_TOO_LARGE`, 413); no
  assembly named like the SDK, `Innovayse.Hostpanel.*`, `System.*`, `Microsoft.*` or anything in the
  host's trusted platform assembly list (`PLUGIN_HOST_ASSEMBLY_REFUSED`); and, when
  `Plugins:TrustedPackageSha256` lists any digest, the archive's SHA-256 must be one of them
  (`PLUGIN_NOT_TRUSTED`; an empty list, the default, accepts any archive that passes the other
  checks). Every install and removal writes an activity log line with no client (`ActivityLog.ClientId`
  is nullable for system-wide actions), carrying the plugin id and, for an install, the digest.

## CQRS — Wolverine, conforming

Wolverine 5.41.0. `Extensions/MessagingRegistration.cs` registers discovery over the API and
Application assemblies; handlers are never registered by hand and never called directly. One
folder per use case under `Application/<Feature>/{Commands,Queries}/<UseCase>/`.

- **Handlers return `Result` / `Result<T>`** (`Shared/Models`); controllers derive from
  `BaseApiController` and return through `HandleResult`, which maps the failure code to a status
  with `StatusMapping`. Every controller derives from it (`ControllerBaseTests`); handlers and
  controllers use explicit `private readonly` fields and a written constructor
  (`ExplicitConstructorTests`).
- **Validation is a Wolverine policy.** `opts.Policies.Add<ValidationPolicy>()` puts
  `ValidationMiddleware<TMessage>`, closed over the chain's message type, at position 0 of every chain
  (its validators arrive through the constructor, never from the service provider); validators are registered Scoped with
  `AddValidatorsFromAssemblyContaining<AcceptInvitationCommand>()` (Scoped is load-bearing:
  validators take `ICurrentUserService`). A failure is a 400 `VALIDATION_ERROR` with the
  field errors grouped under `errors`. Validators exist on both commands and queries.
- **No service location in generated handler code.** `opts.ServiceLocationPolicy = NotAllowed` in every
  environment: Wolverine builds each handler's dependencies as constructor calls, so an adapter is
  registered by type (no `sp => ...` factory for a scoped or transient service), is `public` with a public
  constructor whose parameters are public too, and never takes `IServiceProvider`. A vendor type stays `internal` by
  never appearing in an adapter's public signature, not by hiding the adapter. The services only the container
  can build are named once in `MessagingRegistration.ContainerBuiltServices`: the data context and its
  `IHostpanelDbContext` port, Identity's `UserManager<AppUser>`, `PluginActivator` (it builds a
  plugin type loaded at run time, for every plugin resolver) and every typed HTTP client `AddHttpClient<T>` registers. A test double
  is registered as an instance (`AddSingleton<IPort>(fake)`) or by type, never through a factory.
  `MessagingRegistrationTests` builds every chain, with and without the demo, both on the test host and on a
  host that keeps the production container (`IntegrationTestFactory.WithProductionAdapters`, no test double), and
  walks the production registrations for a type, constructor or constructor parameter that is not public; the
  test host's public fakes would otherwise hide a production adapter that is not.
- **No identity on a message** (`NoIdentityOnMessagesTests`); handlers read it from
  `ICurrentUserService` (`Shared/MultiTenancy/`).
- **Client portal permissions are enforced on the server, per action.** Every action behind `Policies.Client` carries
  exactly one of `[RequiresClientPermission(ClientPermission.X)]` or `[AvailableToEveryClientUser]`
  (`API/Authorization/`; `ClientPortalPermissionTests` fails otherwise). The filter asks `IClientAccess`, which reads the
  caller from `ICurrentUserService`: the owner of a client record holds every permission, a linked user holds the
  stored grant, anyone else holds none, and a refusal is 403 `CLIENT_PERMISSION_DENIED`. The action-to-permission
  table is in the remarks of `RequiresClientPermissionAttribute`. The use cases behind the portal still resolve the
  caller's own client record (`FindReadOnlyByUserIdAsync`), so a linked user with no record of their own passes the
  gate and is then answered by the use case; letting them act on the account they are linked to is a separate change.
- **Architecture tests** (`Architecture.Tests`): `ControllerBase`, `ExplicitConstructor`,
  `NoRequestTypes`, `NoIdentityOnMessages`, `PortPlacement`, `EnumPlacement`, `NoVendorNames`,
  plus resource coverage, kernel purity, SDK contract and use-case shape. Application types carry
  no vendor names (`ICardPaymentGateway`, `GetControlPanelSsoUrl`,
  `GetHostingPanelServerInfo`/`V7`).
- **A test per use case, enforced.** `UseCaseShapeTests` fails when
  `Application/<Feature>/{Commands,Queries}/<UseCase>/` has no
  `Integration.Tests/<Feature>/<UseCase>Tests.cs` (crons and event handlers under `Commands`
  count as use cases).
- **Time comes from `IClock`; timestamps are stamped on save.** `IClock`
  (`Application/Common/Interfaces/`) is the port, `Infrastructure/Time/SystemClock` reads the
  registered `TimeProvider`, so a test that swaps `TimeProvider` moves both. The entities declare
  their own audit columns rather than deriving from `AuditableEntity`, so
  `Infrastructure/Persistence/AuditableEntityInterceptor.cs` stamps by convention: `CreatedAt` on
  insert unless already set (`Backdate` before the first save wins), `UpdatedAt` on change unless
  the entity set it itself. Factories do not write `CreatedAt`; a domain method that needs a
  moment takes it from the caller (`ClientService.Activate` gets its billing anchor that way: the
  activation moment, when provisioning succeeds, not the order date; it never lowers a later renewal date).
- **A module owns its tables** (`ModuleTableOwnershipTests`, allow-list in `CrossModuleReadModels`). Another
  module's rows are reached through its query on `IMessageBus` or a port, never its `DbSet`. The shared
  blocks: `GetClientBriefsQuery` (id, owner subject, name, company, currency, status, invoicing preference, credit balance and, on request, the
  sign-in email; `ClientBriefBusExtensions` holds the call shapes) for anything that names or mails a client;
  `ListCurrenciesQuery`, `GetSetting`/`GetPublicSettings`/`GetSettingValues` (several keys read together)
  and `GetProductSummariesQuery` for currencies, settings and products; `GetCallerClientAsync`
  (`CallerClientExtensions`) for the caller's own client record; `GetServerConnectionQuery` (through
  `ServiceServerExtensions.FindServerAsync` in Provisioning) for the server a service is hosted on. Existence of a parent row in another module is asked of its owner too: `ClientExistsAsync` (Clients), `ProductExistsAsync` (Products), `ServiceExistsAsync` (Services) are bus extensions; `IsServerInUseQuery` (Services), `IsServerGroupInUseQuery` (Products) and `GetDiskUsageByDomainsQuery` (Reports) answer the server module's and the service list's questions. Activity-log lines are written through the `IActivityLogger` port, not an Audit
  command: the port stages the line on the caller's own context, so the line and the change it records
  commit or fail together, which a separately saving command could not promise. A client's credit
  balance changes the same way through `IClientCredit`, so moving credit between an invoice and the balance
  (`InvoiceCreditExtensions` in Billing) is one save. The renewal read models belong to their owners and Billing
  reaches them over the bus: Services owns `GetRenewableServices`, `GetServiceBillingStates`,
  `ListOverdueSuspendedServices`, `ListUndatedRenewingServices` and `ExtendServiceRenewal`; Domains owns
  `GetRenewableDomains`, `GetDomainRenewalStates` and `GetTldRenewPrice`. The setup token kept
  among the settings is issued, checked and retired through `ISetupTokenStore`. Provisioning counts as part of the
  Services module (both drive `ClientServices`). One documented writer exception: the migration importers
  (`Application/Migration/Importers`, with `MigrationClientResolver` and `MigrationProductLookup`) write
  many modules' tables directly, because they are a bulk import of legacy data across modules that runs as
  one job with its own log; the reason is written on `IMigrationEntityImporter` and the entry is marked
  `CrossModuleAccess.Writer` in `CrossModuleReadModels`. No other writer joins it without an owner decision.
- **Domain events are dispatched.** `BaseEntity.DomainEvents` collects them and
  `Infrastructure/Persistence/DomainEventDispatchInterceptor.cs` publishes them through Wolverine
  after `SaveChanges` (inside an explicit transaction: on its commit, dropped on rollback), then clears them. `IDomainEventGate` suppresses dispatch for bulk writers
  (`MigrationPullWorker`, `DevDataSeeder`; tests use `Fixtures/SuppressedDomainEventGate`).
  `DevDataSeeder` reads the passwords of the accounts it creates from `DevDataSeederOptions` (section `DevDataSeed`, both required, no default in code or `appsettings.json`). The dev compose file supplies them as `DevDataSeed__AdminPassword` / `DevDataSeed__ClientPassword` from `DEV_SEED_ADMIN_PASSWORD` / `DEV_SEED_CLIENT_PASSWORD` (documented dev defaults in `docker/.env.example`); no `appsettings.Development.json` is kept, it is git-ignored.
  `BaseEntity.AddDomainEvent(Func<IDomainEvent>)` defers building an event that needs the id
  until after the save. `ClientService.Suspend/Unsuspend/Terminate` take `hostingAlreadyUpdated`
  (handlers pass `true` after the panel call) so the event handler does not repeat it. Event
  handlers only send notifications, with these exceptions: `Billing/Events/RenewalPaymentReceivedHandler` dispatches
  `ApplyPaidRenewalsCommand`. It is safe because it only triggers an idempotent command (the line's
  `RenewalAppliedAt` marker decides) that the daily renewal sweep also runs, so a dropped event is
  applied within a day. The second exception is `Services/Events/ClientServiceTerminatedHandler`, which dispatches
  `RetireTerminatedServiceCommand` (closes the service's open cancellation requests and revokes its SSH keys on
  the server each key is pinned to, leaving a key the agent does not confirm removed marked as a failed revoke for
  staff); it is idempotent, and a failure is logged for staff. `Services/Events/SuspendSshKeysOnServiceSuspendedHandler`
  dispatches `SuspendServiceSshKeysCommand` on `ClientServiceSuspendedEvent`, which takes the service's installed SSH
  keys off their server, and `Services/Events/RestoreSshKeysOnServiceUnsuspendedHandler` dispatches
  `RestoreServiceSshKeysCommand` on `ClientServiceUnsuspendedEvent`, which puts them back. `Provisioning/Events/ServiceUnsuspendedHandler`
  dispatches the same `RestoreServiceSshKeysCommand` a second time, but only after the panel answered `Updated` for
  the unlock, because an agent refuses keys for a locked account and the two unsuspensions are not ordered. All three
  only trigger idempotent commands, log a refusal or failure for staff and never throw, since Wolverine runs every
  handler of one message in a single chain.
  The remaining three react to `ClientClosedEvent` and are described under "Client account status" below.

- **`InvoiceItems.ClientServiceId` and `InvoiceItems.RegisteredDomainId` carry no foreign key, on purpose.** An
  invoice line is a financial record that must outlive what it billed: a terminated service's row or a cancelled
  domain's row can be removed, and a constraint would either block that delete or, with a cascade, delete the invoice
  line (and so change a total already sent to a client). Each column is indexed for the reads that filter on it ("the
  invoices for this service", "this domain's renewals", the renewal duplicate guard) and nothing else enforces it;
  a line whose target is gone simply bills a name it keeps in its own `Description`. The renewal markers on the line
  decide what is applied, never the existence of the target, and the sweeps read the target through its module's
  query, which answers "not found" for a removed row.

### Recurring billing

Renewal invoicing, payment and suspension run as four crons (UTC, the default hours of `ScheduledJobsOptions`):
`ProcessRenewalsCron` 05:00 (applies paid but
unapplied renewal lines, self-heals zero-total open renewal invoices, alerts on stale claimed lines, then raises
renewal invoices per client), `SendRenewalRemindersCron` 06:00, `CheckOverdueInvoicesCron` 07:00 and
`SuspendOverdueServicesCron` 09:00. Settings (`BillingSettingKeys`, not public): `billing.renewal.invoice_days_before`
(14), `billing.renewal.reminder_days` (`7,3,1`, may be cleared), `billing.renewal.auto_apply_credit` (true),
`billing.renewal.suspend_after_overdue_days` (3). `InvoiceItem.RenewalAppliedAt` is the exactly-once marker and
`RenewalAttemptAt` claims a domain line before the registrar call. Every suspension carries a `SuspensionSource`
(`Manual`, `Overdue`, `DomainExpired`, `Other`): payment lifts only `Overdue`, and `DomainRenewedHandler` lifts only
`DomainExpired`. Renewal alerts go to `Notifications:AdminEmail`, falling back to `Notifications:ContactEmail`.

**Renewal invoice lines are not unique per period, on purpose.** The indexes on `InvoiceItems`
`(ClientServiceId, RenewalPeriodStart)` and `(RegisteredDomainId, RenewalPeriodStart)` stay non-unique: a
cancelled invoice's renewal line must not block raising that period again. Double raising is prevented
by two things instead: the single runner of each recurring pass (the `IJobLease` lease, so one instance
at a time runs `ProcessRenewalsCron`), and the `AnyAsync` guard in `RaiseClientRenewalInvoicesHandler`,
which looks only at invoices that are not `Cancelled` and refuses a target that already has an open
renewal line, a paid line not yet applied, or a line for the same period. A unique index would need a
filter on the invoice status, which lives on another table, so it cannot be expressed as an index.

### Client account status

`Client.Status` is enforced, not just stored. The transitions live in the aggregate and nowhere else
(`Suspend`, `Close`, `Activate`, `Deactivate`, each taking the caller's `now` and raising its event):

| From | Suspend | Close | Activate | Deactivate |
| --- | --- | --- | --- | --- |
| Active | Suspended | Closed | no-op | Inactive |
| Inactive | Suspended | Closed | Active | no-op |
| Suspended | `CLIENT_SUSPEND_NOT_ALLOWED` | Closed | Active | `CLIENT_DEACTIVATE_NOT_ALLOWED` |
| Closed | `CLIENT_SUSPEND_NOT_ALLOWED` | `CLIENT_CLOSE_NOT_ALLOWED` | `CLIENT_ACTIVATE_NOT_ALLOWED` | `CLIENT_DEACTIVATE_NOT_ALLOWED` |

A no-op raises no event. The events are `ClientSuspendedEvent`, `ClientClosedEvent`,
`ClientActivatedEvent` and `ClientDeactivatedEvent`, each carrying `OccurredAt`.

What each status means, following the usual billing-panel convention (staff decide, the account is
never deleted):

- **Active** and **Inactive** behave the same for money. Inactive only marks a client that is no longer a
  current customer; its services and renewals carry on.
- **Suspended**: portal sign-in still works and everything is readable, so the client can see their invoices
  and open tickets, but the account cannot place orders (`PlaceOrder`, `OrderMyService`), cannot start a
  new card or gateway payment (`PayMyInvoice`, `StartGatewayPayment`, `CreateOrderPaymentIntent`) and is not
  billed: `RaiseClientRenewalInvoices` refuses it and `ProcessRenewalsCron` leaves it out. The refusal is
  403 `CLIENT_SUSPENDED`. Settling a payment that is already in flight (`CompleteMyGatewayPayment`,
  `ConfirmOrderPayment`, `CompleteOrderGatewayPayment`) is never blocked, so money already taken is recorded.
  Suspending the client does not suspend its services; staff do that per service.
- **Closed**: everything a suspended account cannot do, with 403 `CLIENT_CLOSED`, and the closing is
  settled by three handlers of `ClientClosedEvent`, each failure-isolated and logged:
  `Clients/Events/ClientClosedSessionsHandler` ends the owner's sessions through every `ISessionRevoker`
  (a security stamp change in local mode), `Services/Events/ClientClosedServicesHandler` suspends every
  Active service through `SuspendServiceCommand` with `SuspensionSource.Other` (so no payment lifts it) and
  `Billing/Events/ClientClosedInvoicesHandler` cancels the client's `Unpaid` renewal invoices through
  `CancelInvoiceCommand` (applied credit returns to the balance). `Overdue` invoices are debts on record and
  stay. Sign-in itself is not blocked, so a closed client can still read their history. A closed account
  cannot be reopened.

The guard is `ClientStatusExtensions.EnsureMayTransact` (and `EnsureClientMayTransactAsync` for a caller that
only has a client id), so every module asks the same question. Staff routes are not guarded: an admin can still
create an order or record a payment for a suspended client.

### Roles and the Reseller role

Three roles exist: `Admin`, `Reseller` and `Client`. The staff API is `Policies.Admin` only. A reseller is
linked to no clients (no `ResellerId` on any record), so there is nothing to scope a reseller's view to, and
every route that once accepted `Reseller` over unscoped data (clients, domains, invoices, orders, services,
transactions, reports, quotes, dashboard, provisioning, tickets) is now Admin only; `Policies.ResellerOrAdmin`
no longer exists. Today the Reseller role can sign in and can do what any signed-in user can; it reaches no
staff route. Introducing reseller-owned clients is a separate design (a `ResellerId` on `Clients` and a scope on
every query), not a policy change.

### Client account switches and contact copies

The switches an admin sets on a client are enforced where the feature exists, and a switch whose
feature does not exist is not kept. The decisions:

- **`TaxExempt`.** No tax is added to an invoice automatically (there is no tax engine, only the
  rate an admin types on the invoice options), so the switch is enforced there:
  `UpdateInvoiceOptions` refuses a new or higher rate on a tax-exempt client's invoice with 409
  `CLIENT_TAX_EXEMPT`. A rate the invoice already carries may stay. New and renewal invoices carry no
  tax to remove.
- **`OverdueNotices`.** The payment reminder mail for an unpaid renewal invoice
  (`SendRenewalReminder`) goes only to a client with both `NotifyInvoice` and `OverdueNotices` on. The
  reminder threshold is still recorded when nobody is mailed, so it is not retried every day. No other
  overdue mail exists.
- **`DisableCcProcessing`.** The two card flows refuse an account with it on, with 403
  `CLIENT_CARD_PAYMENT_DISABLED`: `PayMyInvoice` (charge of a saved card) and
  `CreateOrderPaymentIntent` (a new card payment). The check is
  `ClientSettingsExtensions.EnsureClientMayPayByCardAsync`, beside the status guard
  `EnsureClientMayTransactAsync`. Hosted gateway sessions (`StartGatewayPayment`) and staff-entered
  payments are not card processing and are not refused.
- **`AllowSso`.** It means single sign-on into the hosting panel of the client's own services (the
  product has no per-client external sign-on for the client sign-in itself, which is the same for every
  account). `GetMyServiceControlPanelSsoUrl` refuses an account with it off, with 403
  `CLIENT_SSO_DISABLED`; the staff route is unscoped and unaffected. It sits on top of the per-user
  `AllowSingleSignOn` permission: both must allow.
- **`LateFees`** had no late fee feature behind it and was removed (entity, DTOs, commands, admin form,
  the unused `billing.late_fee` seed row). The column is dropped by a migration.
- **Contact `Notify*` flags.** Every mail goes through `SendEmail`, so a sender that mails a client
  passes `SendEmailCommand.Copy` (`ContactCopy`: the client and a `ContactNotification` kind) and the
  handler also sends the same rendered message to the contacts who switched that kind on
  (`GetContactNotificationEmails`), once per address, one log line each. Invoice created, payment
  received and renewal reminders use `Invoice`; ticket replied and closed use `Support`; service
  provisioned uses `Product`; the domain mails use `Domain`. Mail to staff, verification and
  security mail are never copied. `General` and `Affiliate` have no sender yet.
- **Client data export** lists the whole profile, contacts with address and `Notify*` flags, linked users
  with permissions, invitations (never the token), and mail to the account's and every contact's address,
  matched case-insensitively. The lists with no ceiling, `emails` and `activityLog`, carry the newest 500
  rows as `{ items, truncated, limit }`. An empty section is an empty list, never a sentence, and a key that
  names no section is refused with 400.
- **Client list.** Search is case-insensitive over first name, last name, "First Last", company and,
  for a whole number, the client id, escaped so `%` and `_` match themselves. Page and page size are
  validated (400, not clamped), and the order ends in the id so a page never repeats a row. A
  `pg_trgm` GIN index on `lower("FirstName")`, `lower("LastName")`, `lower("FirstName" || ' ' || "LastName")`
  and `lower("CompanyName")` would serve the search once the table is large; it is not created yet.

## Demo mode

`Demo__Enabled=true` turns a deployment into the public demo; nothing is a default, so
`Demo__ResetIntervalHours`, `Demo__AdminEmail`, `Demo__AdminPassword`, `Demo__ClientEmail` and
`Demo__ClientPassword` must all be set or startup is refused (`DemoOptionsValidator`). It needs
`AUTH_MODE=local`. What it does, and where:

- **Fakes, chosen once in DI.** `Infrastructure/Demo/DemoServiceRegistration` replaces every outward
  port (payment, registrar, provisioning, rates, contact notifier, monitoring) with an `internal`
  offline fake. Mail is not replaced. No adapter or handler asks whether it is a demo.
- **Guard.** A command that must not run on a demo carries `IDemoRestricted` (`Shared/Contracts/Interfaces`);
  `DemoGuardPolicy` puts `DemoGuardMiddleware` on exactly those chains and it answers 403
  `DEMO_ACTION_DISABLED`. A new dangerous command is covered by adding the marker, never an `if`.
  The demo accounts' own credentials are protected by one rule, `IDemoAccountGuard` (Application port, `Infrastructure/Demo/DemoAccountGuard`):
  with demo on, the email and password of `Demo__AdminEmail` / `Demo__ClientEmail` cannot change; password
  reset (`ResetPasswordCommand`) and `UpdateClientHandler`'s email change ask it and answer 403 `DEMO_ACTION_DISABLED`. Local sign-in (`UserService`) asks it too: wrong passwords are not counted against those two accounts, so no visitor can lock them.
- **Currency.** The demo payment plugin accepts every currency stored (`IPaymentPlugin.AcceptsCurrency`), not only USD.
- **Data.** `IDemoDataManager` resets (wipes and reseeds) at startup instead of running `DevDataSeeder`, so every deploy shows the current seed, and
  `ResetDemoDataCommand` truncates every table except the identity roles and reseeds in one
  transaction every `ResetIntervalHours`; `GET /api/public/demo` publishes the accounts and the next
  reset. Seed text names no outside product.
- **Seed.** The demo seed is fixed and deterministic: `DemoCatalogueSeeder`, `DemoPeopleSeeder` and `DemoClientProfile`
  produce the same sample data on every reset, and it is maximal, with every table populated enough to show each
  feature. The site name is `Hostpanel Demo`. The storefront contact is the one operator-set exception: optional
  `Demo__ContactEmail` / `Demo__ContactPhone` (`DEMO_CONTACT_*`) replace the fictional `.example` / `+1 555 0100` pair
  on every seed and reset; they are display only, and demo mail still goes to mailhog.
- **Network.** The demo's services sit on a demo-private network and never join the shared production
  network, so the demo cannot reach production's services. See the configuration section below.
- **Deploy scope.** The demo is deployed with its own deployment CI variables, including its own `DEPLOY_DIR`.

## Stack

.NET 9 · Wolverine 5.41.0 · EF Core 9 + Npgsql 9 · PostgreSQL 17 · ASP.NET Identity (local
mode) + JWT bearer · `Innovayse.Auth` (cookie session for the admin SPA, sso builds only) · SkiaSharp (branding images) ·
Serilog · Scalar + Swashbuckle · Mapster · Stripe.net · MailKit · Otp.NET · Fluid.Core ·
DnsClient · Redis (sessions + data-protection key ring) · RabbitMQ · xUnit + FluentAssertions +
Testcontainers.PostgreSql 4.0.0 · Nuxt 4.3 / Vue 3.5 / Pinia / Tailwind / vitest (client,
**yarn**) · Vite + Vue 3.5 / Tailwind 4 / vitest (admin, **npm**).

Central package management: versions live once in `backend/Directory.Packages.props`; a
`.csproj` carries `<PackageReference Include="…" />` with no `Version`.

## Build, test, run

**`Innovayse.Auth` is optional at build time.** A plain clone has no credential for the private
registry, so `backend/Directory.Build.props` defines `InnovayseSso`: true when
`NuGetPackageSourceCredentials_innovayse` is set (CI and deployments that use the external identity provider), false
otherwise, and forceable with `-p:InnovayseSso=true|false`. When true the package is referenced
(`Innovayse.Hostpanel.API` and `Innovayse.Hostpanel.Infrastructure`) and `INNOVAYSE_SSO` is
defined; when false nothing private is restored and the build is `AUTH_MODE=local` only — a
standalone build started with `AUTH_MODE=sso` throws at startup from `AddHostpanelAuthentication`
naming the property. Every file that touches the package is inside `#if INNOVAYSE_SSO`:
`API/Program.cs` (`UseInnovayseAuth`/`MapInnovayseAuth`), `API/Extensions/AuthenticationRegistration.cs`
(`AddInnovayseAuth`, the claims transformation), `API/Auth/SsoSessionClaimsTransformation.cs`
and `SsoModeAuthenticationSchemeTests`. The cookie scheme name is the one constant
`AuthenticationRegistration.SsoCookieScheme` (a placeholder in a standalone build), which
`PolicyRegistration` reads; do not name `Innovayse.Auth.CookieSessionHandler` anywhere else.
`backend/Dockerfile` passes `-p:InnovayseSso=true` to restore and publish when `AUTH_MODE=sso`
is the build arg, so a missing credential fails the build rather than yielding an image that
refuses to boot. The GitHub workflow `.github/workflows/ci.yml` builds the standalone flavour and
runs the Domain, Architecture, CWP, Inecobank and Integration suites, plus the migration addon's PHP tests.

```bash
./dev up hostpanel                # from the workspace root, when working inside the workspace
```

To build without touching the dev stack, **run docker through PowerShell, not Git Bash**,
which rewrites `-w /src` into a Windows path and exits 125. Run it from the repository root:

```powershell
docker run --rm `
  -v "${PWD}\backend:/src" `
  -v "$env:USERPROFILE\.nuget\packages:/root/.nuget/packages" `
  -w /src mcr.microsoft.com/dotnet/sdk:9.0 bash -lc "dotnet build Innovayse.Hostpanel.sln"
```

**A cold full solution build takes about eight minutes** — 7m43s measured, 14 projects, 0
warnings. It prints nothing for minutes at a stretch and **appears to stop after
`Innovayse.Hostpanel.Infrastructure`**; that project is simply the slow one, and `Innovayse.Hostpanel.API`, the
test projects and the rest land minutes later. Only treat it as hung well past ten minutes.
Three attempts in a row abandoned this build as stalled and fell back to a narrower one, losing
the cross-project signal it exists for.

The host package cache covers the private feed for a local build: `nuget.config`'s
`packageSourceMapping` sends only `Innovayse.Auth` to the private registry, and that package is
already cached. Where a credential *is* needed it is
`NuGetPackageSourceCredentials_innovayse=Username=<account>;Password=<token>` — the exact
spelling NuGet reads; a CI job token user name answers 401 with a personal token. The feed is addressed by **numeric project id**, not by the encoded
namespace path, which answers 400 to everything and reports as NU1301.

**`Innovayse.Hostpanel.Integration.Tests` needs Testcontainers**, and that means the docker socket plus
both `-e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` and
`--add-host=host.docker.internal:host-gateway`. Without them the resource reaper cannot start
and every test in the suite fails with a stack trace that looks nothing like a networking
problem. **Do not pass `--no-build` to the integration suite** — it silently produces no output
and exits 0 whether or not the assembly is there. The Domain, Architecture, CWP and Inecobank suites are fine with it.

Containers: `hostpanel-api` (5148), `hostpanel-client` (3000, published 3001),
`hostpanel-admin`, `hostpanel-db`, plus `hostpanel-redis`, `hostpanel-rabbitmq`,
`hostpanel-mailhog` locally. Services address each other by **network alias**
(`hostpanel-api`), never by the bare `api` — that name can also belong to another product's
backend on the shared network, and Docker's DNS round-robins between the two.

## Operational scripts at the repo root (`scripts/`)

- `scripts/check-client-owner-ids.sh [container]` — read-only check of what `clients.UserId` points at
  (the local `AspNetUsers.Id` or the external identity provider's subject), run before and after switching
  `AUTH_MODE`. Defaults to the `hostpanel-db` container; on a remote host pipe it as
  `ssh <host> 'sh -s' < scripts/check-client-owner-ids.sh`.
- `scripts/pack-cwp-plugin.sh` — publishes `Innovayse.Providers.CWP` (Release, net8.0) on the host, strips
  PDB/XML and framework DLLs, adds `plugin.json` and writes `dist/innovayse-cwp.zip`.
- `scripts/check-empty-plugins.sh <test project> ...` — fails when `plugins/` or `plugins-installed/` in a
  built test project's output holds anything. Every backend test job of both pipelines runs it between the
  build and the tests, so the whole suite is proven green with no plugin loaded in the jobs that already
  run it, not in a duplicate set. A test that needs a plugin stages it itself and removes it.

All are `*.sh`, which `.gitattributes` (`*.sh text eol=lf`) pins to LF: the ssh pipe above fails with
"Illegal option" on a CRLF copy.

## CI is on GitLab. Pull requests are on GitHub. Both halves are load-bearing.

`origin` is GitHub; merges happen there. `.github/workflows/mirror-to-gitlab.yml` pushes
**only `main` and `develop`** to the CI mirror, whose `.gitlab-ci.yml`
starts the pipeline. The consequences are not obvious and cost time every few months:

- **A feature branch never gets a pipeline.** Nothing is mirrored, so nothing builds or tests
  it. A pull request into `develop` shows no checks because there are none to show, not
  because they passed.
- The one GitHub job that *does* run on a pull request is
  `.github/workflows/branch-flow.yml`, a required status check on `main` that refuses any
  source branch other than `develop` or `hotfix/*`. It enforces what neither platform has a
  setting for.
- Tests, image builds and both deploys live in `.gitlab-ci.yml`. `deploy:staging` runs on
  `develop` against the staging host; `deploy:prod` is **manual** on `main` against the production host.
- **The plugin SDK package is not published from here.** `backend/src/Innovayse.SDK` is a submodule of
  github.com/innovayse/hostpanel-sdk, whose own workflows build, pack and publish it (a raised `<Version>` on its
  `main`, or a version tag). This repository only builds against the commit the submodule pins. Every checkout
  fetches it: `submodules: true` on each `actions/checkout`, `GIT_SUBMODULE_STRATEGY: recursive` in
  `.gitlab-ci.yml`, and the deploy jobs (`GIT_STRATEGY: none`) run `git submodule sync --recursive` and
  `git submodule update --init --recursive` after every checkout of their deploy directory, the rollback included.
  The SDK project opts out of central package management and pins its two package versions itself, so it
  builds alone; keep those versions equal to the ones in `backend/Directory.Packages.props`.
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

- **`AUTH_MODE` (`sso` | `local`).** `sso` authenticates against the external identity provider and reads every
  account out of it through `/api/service/*` with `SSO_SERVICE_KEY`; `local` is the standalone
  open-source path where hostpanel owns its own user table and registers no SSO scheme at all.
  Only this product implements both, and most "why is there a second way to do this" in the
  auth code is this switch. Under `sso` the API refuses to start without
  `CLIENT_HOSTPANEL_SECRET` rather than come up and hand every visitor an anonymous session.
  That one credential has one variable name: the client portal receives it as
  `NUXT_SSO_CLIENT_SECRET`, fed from `CLIENT_HOSTPANEL_SECRET` in every compose file, so there is
  no separate client-secret variable.
- **`ASPNETCORE_ENVIRONMENT` is set by the deploy job, never by a host's `.env`.**
  `docker/docker-compose.dev.yml` passes it through from `docker/.env`: `Development` is right for a laptop and was what
  deployed hosts silently inherited. A Development API serves developer exception pages.
  `EncryptionKey` is required in every environment, and `AppDbContext` cannot be built without an
  `IEncryptionService`; the encrypted columns (server credentials, SSH agent keys, client-service
  passwords, two-factor secrets) are listed once in `ProtectedColumns`, which also sizes them.
  Every deployed tier (staging, prod, demo) sets `Production`, per `rules/environments.md`; the tier
  is named by the job's `environment:`, never by `ASPNETCORE_ENVIRONMENT`.
- **Demo mode (`Demo__Enabled`).** Set through `DEMO_*` env in `docker/docker-compose.demo.yml`, an overlay
  placed last in the chain (`shared` + `prod` + `platform` + `demo`, project `hostpanel-demo`). It swaps the
  payment, registrar and provisioning adapters for fakes, sends mail to the demo's own mailhog and resets
  data every `Demo__ResetIntervalHours`. Everything stateful is `hostpanel-demo-*` so it can sit beside prod; its db, broker, redis and mailhog live on a demo-private network and only the api, client and admin (compose services `hostpanel-demo-*`, the names the edge proxy uses) also join the dedicated external network `hostpanel_demo_edge`, **never** the shared network, so the demo can neither resolve nor reach any production container (the edge proxy is the one production-side container on both networks; `deploy:demo` creates the network when missing, with the **fixed subnet 172.29.0.0/16**, which the edge's `nginx.conf` denies on every host except the two demo ones, so a demo container cannot reach production by sending a production `Host` header; the subnet lives in `deploy:demo`, `edge-proxy/nginx.conf` and `docker/README.md`, and changes in all three together); `deploy:demo` uses its own `DEPLOY_DIR` (one of the demo deployment's CI variables), never prod's;
  the demo passwords are `:?`-required like any secret. Deployed by the manual `deploy:demo` job; it needs
  `AUTH_MODE=local`. Fake adapters live behind the existing ports, never in a code path guarded by a literal.
- **Settings bind into a sealed `*Options` class naming its own `SectionName`** —
  `BillingOptions`, `CPanelOptions`, `NamecheapOptions`, `NameAmOptions`, `StripeOptions`, each
  in an `Options/` folder inside the feature or integration that owns it.

## Storefront content — in the database, not in the code

Testimonials, partners, server locations, projects and FAQs are rows in the database, each with English, Russian and Armenian
translations, managed in the admin under **Storefront content**. The public endpoints are
`/api/public/{testimonials,partners,locations,projects,faqs}`. The hero stats and badge read `portal.stats.*` and
`portal.hero.badge`; the contact channels read `portal.contact.*`; the site name reads `portal.site.name`. The
client hides any section that is empty, so an empty table is a valid state, not an error. Production runs a
one-time import of the previous static content; the demo does not, because its seed is fixed.

## The frontend: three templates, one live header, and one env-var trap

`client/` is Nuxt 4 with SSR on the public site and `ssr: false` route rules for `/client/**`,
`/cart/**` and `/checkout/**`.

**The header you are looking at is almost certainly not `components/layout/Header.vue`.**
`layouts/default.vue` renders `<component :is="header" />`, and `header` comes from
`useTemplate().slot('header')`, which resolves through `templates/registry.ts`. There are three
templates — `aurora`, `nova`, `classic` — and the active one is decided by, in order: the
operator's `portal.template` admin setting, then `NUXT_PUBLIC_PORTAL_TEMPLATE`, then the
built-in default **`classic`** (`DEFAULT_TEMPLATE`, mirrored by `PortalSettingKeys.DefaultTemplate`); anything unrecognised falls back to `classic` rather than
rendering nothing. `components/layout/Header.vue` is reached only through
`templates/classic/layout/Header.vue`, a two-line wrapper — so on an aurora or nova deployment it is
live code that nothing renders. Editing it and reloading changes nothing, and that has cost
real time. Start from `templates/<active>/layout/Header.vue`.

`nova` deliberately reuses `aurora`'s `Domains.vue` and `Checkout.vue` — the checkout is the
ordering path and a second copy is a second place for a payment bug to live. If a shared aurora
page grows a new section import, add it to `AURORA_SHARED_COMMERCE` in `nuxt.config.ts` or nova
starts downloading the whole `template-aurora` chunk.

**Runtime config in a production image.** A production Nitro build freezes `runtimeConfig`
defaults — `nuxt.config.ts` is not re-evaluated at container start as it is in dev. Nitro's own
escape hatch is reading `NUXT_<KEY>` / `NUXT_PUBLIC_<KEY>` at runtime and overriding the frozen
default, so `docker/docker-compose.prod.yml` spells every one of them that way. **The plain names used
in `docker/docker-compose.dev.yml` are silently ignored on a deployed host**, so the value stays
at its build-time default and no `.env` edit can change it.

**The client image has no build args.** The app-launcher `<script src="…/widget/header.js">` tag is
added by `plugins/header-widget.ts` from `runtimeConfig.public.mainUrl`, so `NUXT_PUBLIC_MAIN_URL`
is plain runtime env. `site.url` is read at runtime by nuxt-site-config from `NUXT_PUBLIC_SITE_URL`
(set in `docker-compose.prod.yml`); the esbuild `drop` depends only on `NODE_ENV`, not on a
deployment. Do not add a `process.env` read outside `runtimeConfig` that differs per deployment.

That widget is a host-provided header widget, not served by this product. It must be HTTPS on an HTTPS
deployment or the browser blocks it as mixed content and the client dashboard's hydration
hangs.

**The `dark` / `light` class on `<html>` has exactly one writer: `client/plugins/color-mode.ts`.**
A second writer lets the operator's chosen mode lose to a stale value and gives a light-mode
visitor a dark flash on load. The resolution rule is in `client/utils/colorMode.ts` and reads,
in order: the visitor's `color-mode` **cookie**, then the operator's `portal.theme.default`
(`light` | `dark` | `system`), then the device. A cookie or a fixed operator mode is rendered
into the HTML on the server; only `system` with no cookie emits an inline head script, because
both template stylesheets declare their dark palette on bare `:root` and a class-less page is
dark until something says otherwise. Do not add `document.documentElement.classList` calls
anywhere, and do not read the mode from anything but `useAppColorMode()`.
`portal.theme.user_toggle = false` hides `UiThemeToggle` and makes `toggle()` a no-op.

**The logo a template renders comes from `useBrandLogo()`, and nowhere else — and a header
renders both wordmarks, swapped by CSS, never one picked in JavaScript.** `light` and `dark` are
the two wordmarks with their fallbacks (each falls back to the other); a header renders
`<img :src="light" class="dark:hidden">` beside `<img :src="dark" class="hidden dark:block">`
when `hasDarkVariant`. The reason is hydration: under the `system` default the server does not
know the mode and renders as dark, and **production Vue does not patch an attribute that
disagrees after hydration** — a `:src` chosen from `isDark` left a light-device visitor looking
at the dark logo, and a `v-if` on the toggle's icon left them looking at the sun. Both were seen
in a browser before the swap. `logoOnDark` is for surfaces that are always dark whatever the
page mode (the sign-in pages, classic's header and footer); `mark` is `portal.logo.mark` with the
same fallback chain; `logo` is the mode-aware pick for browser-only code; and `alt` is
`portal.site.name`, because the logo *is* the site name and the `alt=""` the headers shipped
with told a screen reader the brand was decorative. The rule itself is
`client/utils/brandLogo.ts`, unit-tested. `useSiteIdentity()` is the only source of the site
name; the literal `Hostpanel` survives as its fallback and in the locale copy, and nowhere else
in a `.vue` or `.ts` file.

**Every colour a template ships is a `var()` with the template's literal as its fallback —
a literal on its own is a colour the operator cannot change.** `client/plugins/brand-tokens.ts`
renders one `<style id="brand-tokens">` into `<head>` from `portal.brand.primary` / `.accent`
and the two typefaces: the eleven-step `--brand-primary-N` / `--brand-accent-N` scales (as
`r g b`, for Tailwind's `<alpha-value>`), `--brand-on-primary` / `--brand-on-tint` /
`--brand-on-accent(-tint)` (white or near-black, decided per colour), and
`--font-heading` / `--font-body`. It emits **nothing** when nothing is set. Tailwind's
`primary`/`secondary` scales, the `brand` gradient, aurora's `--ac1/--ac2` and nova's
`--n-brand/--n-accent/--n-on-*` all read those variables with today's literal as the fallback,
which is what makes "unset renders exactly what it did" true; check it by reading the computed
tokens with the rows empty before touching any of them. Text on a filled brand surface is
`text-on-primary` / `text-on-tint` / `text-nova-on-brand` / `text-nova-on-accent`, never
`text-white` or a literal — a brand yellow needs dark text. The ramp lives in
`client/utils/brandPalette.ts` (OKLCH, gamut-mapped by reducing chroma, the picked colour pinned
at 500); `admin/src/lib/brandPalette.ts` is a byte-for-byte copy below its header because the
admin cannot import across the repository (`brandPalette.shared.test.ts` fails on drift), and
`Application/Notifications/Rendering/BrandPalette.cs` is the e-mail renderer's port with the same
reference numbers in `BrandPaletteTests`. Change the client file first; then the other two.

**Third-party scripts come from settings and never from a literal.** The tag-manager container
(`portal.analytics.gtm_id`) loads in `plugins/tracking.client.ts` only when set **and** the
visitor's `cookie-consent` is `all` — `useCookieConsent()` is the one owner of that cookie, the
banner writes it, the loader watches it. The live-chat widget (`portal.chat.provider`,
`.base_url`, `.website_token[.ru|.hy]`) loads in `plugins/live-chat.ts` when all three are set,
without waiting for consent, because it is how a visitor reaches a person. `app.vue` adds no
script at all; vendor ids never live in the repository, they belong in the deployment `.env`.

**Every e-mail renders with `brand.*` and `site.*` in scope.** `TemplateRenderer` adds them
beside the caller's typed model (one `EmailTemplateModel` record per template in
`Notifications/Common/TemplateModels/`, members read in lower snake case, `brand` and `site`
reserved), HTML-encodes every value it prints into a body and none in a subject, and reads them on each
render by `SettingsEmailBrandingProvider` from the same `portal.*` rows, with the logo made
absolute against `ClientBaseUrl`. A stored template that wants the brand uses
`{{ brand.primary }}`, `{{ brand.primary_dark }}`, `{{ brand.on_primary }}`,
`{{ brand.accent }}`, `{{ brand.logo_url }}` and `{{ site.name }}`; the seeded password-reset
layout does, but a row seeded before this existed keeps its literals until an operator re-saves it.

**`PortalSettingKeys` (Domain) is the backend's one list of `portal.*` keys** — the seeder's
`Defaults` and the anonymous endpoint's `Public` set are derived from the same array, and
`AllowedValues` is the vocabulary `UpdateSettingHandler` enforces (`INVALID_SETTING_VALUE`, 400).
The Nuxt BFF's `PUBLIC_KEYS` in `client/server/api/portal/public/settings.get.ts` is a **manual
twin** of that list; it cannot import the class, so a new key is added in both places and in
`nuxt.config.ts`'s `runtimeConfig.public` **and** both compose files' `NUXT_PUBLIC_*` lines.
`PortalSettingKeysTests` fails if `Defaults` and `Public` drift; nothing tests the BFF twin.

### The client's shape (Nuxt exception, conformance C complete)

`client/` is the Nuxt exception the workspace `frontend.md` allows: it has public, crawlable pages
(SEO, SSR) beside the session-gated `/client/**` area, so it builds on Nuxt where the other
products are Vite SPAs. Everything else in `rules/frontend.md` holds in full; the Nuxt specifics
are these, and **no part of the client is exempt any more — the ratchet that once scoped the
rules is deleted and the ESLint conformance block applies to every `.vue` and `.ts` file.**

- **Data flow, as everywhere:** `.vue` → `stores/<area>.ts` → `composables/apis/use<Area>Api.ts` →
  `composables/useApi.ts` (and `apiFetch`) → Nitro BFF (`server/api/portal/**`, every route through
  `internalApiCall`) → the C# API. A `.vue` file never calls `useApi`, `apiFetch`, `$fetch`,
  `useFetch` or an api composable, holds no `catch` and never imports `lib/errorHandler`;
  server-rendered data is `await useAsyncData(key, () => store.loadX(), { watch: [locale] })`.
  A store is the only caller of `apis/`, and exposes `error` and `fieldErrors` for what it refused.
  `useApi.ts` imports no store. The gates: ESLint `no-restricted-imports` and
  `scripts/check-layers.mjs` (auto-imports hide the calls from import rules, so the script reads
  the source for them too).
- **Two hops, one language header.** The browser stamps `X-Language` (from the i18n locale,
  validated by `utils/transportLanguage.ts`) on every call to the BFF; the BFF forwards it to the
  API, and an unknown value becomes `en`. `Accept-Language` is not used anywhere.
- **The signed-in session lives on the server.** The browser holds one opaque httpOnly cookie,
  `portal_session` (a random id), and the readable `authed` hint; the access and refresh tokens stay
  in the session store (`server/utils/portal-session.ts` over `server/utils/session-store.ts`):
  Redis when `NUXT_SESSION_REDIS` is set (compose feeds it `REDIS_CONNECTION`), process memory
  otherwise (unit tests, e2e, bare `nuxt dev`). A route that needs the bearer asks
  `resolveAccessToken`, which renews an expiring token with the refresh token; `internalApiCall`
  refreshes once more on a 401. Never write a token into a cookie: the identity provider's refresh
  token is larger than the 4 KB a browser keeps per cookie and was silently dropped, which ended
  every session with its access token. The Redis keys (`hostpanel:portal-session:<id>` and the
  per-person set `hostpanel:portal-session-subject:<sub>`) are a contract with the API's
  `Auth/PortalSessionRevoker`, which deletes a person's portal sessions on a back-channel logout
  (`PortalSessions__RedisConnection`). A failed silent sign-in returns to the page with
  `?signin=required`, which `LayoutSignInPrompt` turns into a sign-in prompt; an `authuser` query is
  passed to the identity provider as `login_hint`.
- **Sessions are encrypted in Redis, and a renewal is locked.** The stored session (access and refresh
  token) is AES-256-GCM ciphertext (`server/utils/session-crypto.ts`) under `NUXT_SESSION_ENCRYPTION_KEY`
  (`runtimeConfig.sessionEncryptionKey`, 32 random bytes, base64; `SESSION_ENCRYPTION_KEY` in
  `docker/.env.example`). The key is required whenever Redis holds the sessions outside development:
  the `server/plugins/session-store.ts` plugin builds the store at startup, so a missing or malformed key
  stops the server instead of failing the first sign-in. The API's `PortalSessionRevoker` only deletes keys, so it
  does not read the content. Changing the key signs everyone out. A refresh runs under a Redis lock
  (`hostpanel:portal-session-refresh-lock:<id>`, `SET NX` with an expiry), so replicas never present the
  rotating refresh token twice; it ends the session only when the sign-on service refuses the grant
  (400 or 401), and answers 503 and keeps the session on a timeout, a network failure or a 5xx.
  Every call to the sign-on service goes through `server/utils/signon-http.ts` (timeout, typed errors
  with `data.code` and no message, so no internal address reaches the browser); the one exception is
  the callback's code exchange.
- **Cookies and the silent probe.** Every cookie a route or middleware writes takes its options from
  `cookieOptions(maxAge)` in `server/utils/auth-cookie.ts` (httpOnly, Secure outside development,
  SameSite=Lax, path `/`; only the readable `authed` hint is not httpOnly). The silent sign-in probe
  (`server/middleware/sso-silent.ts`) runs only for a document navigation: a GET (not HEAD) with
  `Sec-Fetch-Dest: document` or an HTML `Accept`, from a user agent that is not a known bot, preview
  fetcher, monitor or scripted client (`server/utils/bot-agent.ts`), and never for a page that already
  carries `?signin=required`. Switching account is `GET /api/portal/auth/sso/switch?sub=&redirect=`, which the external
  header widget navigates the browser to, so it must stay a GET. It is safe as one because it never
  changes the local session: it only starts the external sign-on authorize redirect, with a fresh
  state and PKCE verifier bound to this browser in httpOnly cookies, through the provider's own
  `switch-init` page (it runs same-origin there, so its remembered-accounts cookie is sent) with
  the account (`sub`) and a relative authorize address, built with `authorize.get.ts` by
  `server/utils/signon-flow.ts`. The account changes only in `callback.get.ts`, after the state and PKCE proof are
  verified, so a cross-site GET can at most send the visitor through the sign-on page for an account
  they already have. A repeated `sub` or `redirect` key is a 400; only an internal path is kept as
  the place to return to.
- **Errors are chosen in one place.** `lib/errorHandler.ts` (`apiErrorMessage`, `apiFieldErrors`,
  `apiErrorCode`) orders `errors[field]`, then `detail`, then `title`, then the client's own
  `errors.noAnswer`. The BFF writes no sentence (`scripts/` and `tests/server/contract.test.ts`
  fail on a literal `statusMessage:`); a missing or malformed route parameter is a coded 400
  (`requireRouteParam`). `scripts/check-no-legacy-error-fields.mjs` rejects a read of the old
  `error`/`code` pair outside the one allow-listed OAuth call. A refusal passes the backend's
  `Retry-After` and `X-Request-Id` on to the browser (each only in its expected shape).
- **Visitor text is validated before it is used, in one helper each.** Every `[id]` route reads its
  parameter through `requireRouteParam(event, name, kind)` (`types/routeparamkind.ts`: a positive
  32-bit integer by default, a GUID, or an opaque identifier), which validates the shape and returns
  the value percent-encoded, so no route interpolates a raw parameter into a backend path. The
  `/uploads/**` routes read their path through `requireUploadPath` (`server/utils/uploadPath.ts`):
  decoded once, refused when it holds an empty, `.` or `..` segment, a backslash, a control
  character or a second encoding, and re-encoded per segment under a fixed `/uploads/` prefix; the
  generic proxy forwards only conditional and range headers and returns only cache and type headers
  (never a cookie either way). The public catalogue reads its filters through `readCatalogQuery`
  (`server/utils/catalogQuery.ts`: integers, a bounded group list, a three-letter currency), so its
  shared-cache key has a fixed shape; the backend does the filtering.
- **The visitor's address is read from the right.** `server/utils/clientAddress.ts` returns the socket
  address, or, with `runtimeConfig.trustedProxyCount` (`NUXT_TRUSTED_PROXY_COUNT`, `CLIENT_TRUSTED_PROXY_COUNT`
  in the compose files) set to N, the Nth entry from the right of `X-Forwarded-For`, the same reading as
  the API's `RateLimit__TrustedProxyCount`. The leftmost entry is written by the visitor and is never
  used. The default is 0, which believes no header.
- **The BFF holds no business logic and opens no connection on a visitor's behalf.** The nameserver and
  certificate read of a service is the backend use case `GetMyServiceHostingInfo`
  (`GET /api/me/services/{id}/hosting-info`): ownership through the service rule, the domain taken from
  the service row, and the probe behind the `IHostingProbe` port, which refuses IP literals, single-label
  names and any name that resolves to a loopback, private, link-local or metadata address
  (`Infrastructure/Security/PublicHostGuard`). The Nuxt route is a pass-through.
- **One documented composition in the BFF: `server/api/portal/client/me.get.ts`.** In the single sign-on
  mode the signed-in person's email and two-factor state live in the identity provider, not in the API,
  and the provider is called with the person's own token, which only the BFF holds; the route therefore
  combines that profile with `/clients/me` and `/auth/me` (permissions) and adds `hasClientProfile`
  for an identity that has no client record. Moving it into the API would mean the API calling the
  identity provider with the caller's token for a screen's convenience; until the identity profile is an
  API concern, this stays the one exception to "a route forwards one backend call". In local mode the
  route forwards `/clients/me` and adds the permissions.
- **The kit is `components/ui/` (`Ui*`), and screens are composed from it.** `pages/**`, `layouts/**`
  and `templates/*/{pages,sections,layout}/**` carry no raw tag at all; an area's own components
  (`components/<area>/`) compose the kit and may not hold a card-styled `div` or an interactive
  raw element. A new visual need is a new `Ui*` first. Styling is Tailwind classes written out
  literally and theme tokens (`assets/css/ui.css`, `tailwind.config.ts`); per-item colours that
  are data go through `UiAccent`. No `<style>`, no `style=`, no `bg-[#...]`, no `v-html` —
  operator HTML is rendered by `UiRichText` (`utils/ui/richText.ts`, an allow-list tokenizer that
  builds nodes with `h()`; it has an XSS-corpus test). Every field has a `placeholder`, every
  interactive element a `data-testid` (`<area>-<element>`).
- **Types and closed sets.** One concept per file in `types/`, imported with `import type`;
  every closed value set is an enum (`AppLocale`, `TemplateName`, `TicketStatus`, ...), never a
  string union or a repeated literal. The language list is `APP_LOCALES` (`constants/appLocales.ts`),
  derived from `AppLocale`; nothing spells `['en','ru','hy']` out. Server and client share
  `types/` and `utils/transportLanguage.ts` only.
- **Site identity.** The site name is `useSiteIdentity().name` (the operator's `portal.site.name`);
  `NEUTRAL_SITE_NAME` ("Hostpanel") is its fallback when neither is set, never an operator name, and
  no page title, footer or alt text hard codes a brand. The billing portal's address is `BILLING_PORTAL_URL` (`NUXT_PUBLIC_BILLING_PORTAL_URL` in
  the production compose file, `runtimeConfig.public.billingPortalUrl`); every link built from it
  is guarded for the unset case.
- **Locales.** `locales/{en,ru,hy}/<area>.json`, all three in the same change, loaded by
  `plugins/i18n.ts`. `yarn lint:i18n` fails on a key missing from or extra in any language, an
  unused key, or a literal `t('...')` with no `en` entry. A server-decided sentence is never
  copied into a locale file.
- **Tests mirror the source.** `tests/` mirrors `components/ stores/ composables/ lib/ server/utils/`
  one test per file (`tests/coverage-shape.test.ts` fails on a missing mirror; its `KNOWN_GAPS`
  list may only shrink), plus `tests/pages`, `tests/layouts`, `tests/templates`,
  `tests/locales/parity.test.ts`. Real kit and real Pinia (`tests/helpers/mountWithKit.ts`,
  `stubServer.ts`/`stubTransport.ts` for a real HTTP stub under `$fetch`); Nuxt auto-imports are
  stubbed once in `vitest.setup.ts`. `e2e/` is Playwright, one spec per flow, `data-testid`
  selectors only, against the production build with the C# API replaced by
  `e2e/support/stub-backend.mjs` (fixtures in `fixtures.mjs`; `POST /__e2e/script` scripts a
  refusal, `/__e2e/settings` switches the template, `/__e2e/requests` proves what the backend
  received, including `X-Language`).
- **Gates, in `.gitlab-ci.yml` `client:test` then `client:e2e` (all blocking):** `yarn lint`
  (ESLint conformance block + `check-layers` + legacy-field gate), `yarn lint:i18n`,
  `yarn audit:conformance --strict` (every metric zero), `yarn typecheck` (`nuxt typecheck`),
  `yarn test --maxWorkers=2`, `yarn build`, then `yarn e2e`. On this laptop vitest needs
  `--maxWorkers=2`.

### The admin's shape (Vite SPA, conformance B complete)

`admin/` is the workspace's standard Vite SPA (`rules/frontend.md`) with no exception: the operator
back office is session-gated, so it has no crawlable pages. **No part of it is exempt — the ratchet
that once scoped the rules is deleted, and `strictRules` in `eslint.config.ts` applies to every `.vue` and
`.ts` file under `src/`** (the one carve-out is `lib/brandPalette.ts`, a generated byte-for-byte copy).

- **Flat layout, no `modules/`.** `src/views/<Page>View.vue` (routed screens), `src/layouts/`,
  `src/components/ui/` (the kit), `src/components/<area>/` (screen-specific parts composed from the
  kit), `src/stores/<area>.ts`, `src/composables/apis/use<Area>Api.ts`, `src/composables/useApi.ts`,
  `src/composables/use*.ts`, `src/types/<concept>.ts`, `src/lib/` (pure helpers: `errorHandler`,
  `runAction`, `translate`, `format`, `money`, `tones`, `icons`, `reports*`), `src/utils/ui/` (TypeScript
  the kit shares), `src/middleware/auth.ts` (the router guard), `src/locales/`. The API is
  same-origin (`/api/...`), so the only per-deployment value is the base path, and it is runtime:
  Vite builds with a relative `base`, `public/config.js` sets `window.__APP_CONFIG__.basePath`
  (read through `lib/runtimeConfig.ts`), and the prod image's `docker/nginx/admin-entrypoint.sh`
  rewrites `config.js` and `<base href>` from `ADMIN_BASE_URL_PATH` at start. No `VITE_BASE_URL`.
- **Data flow, as everywhere:** view → `stores/<area>.ts` → `composables/apis/use<Area>Api.ts` →
  `composables/useApi.ts` → HTTP. A view never calls `useApi`, an api composable or `lib/errorHandler`
  and holds no `catch`; a store is the only caller of `apis/`, runs every request through
  `runAction(error, task, fieldErrors?)` and exposes `error` and `fieldErrors`. `useApi` is the only
  module that knows transport: base path, CSRF header, `X-Language` (from the active i18n locale, on
  every call), `ApiError` carrying the problem+json body, `buildQuery`. It imports no store, and the
  SSO redirect and end-session address live in `useAuthApi`, not in `useApi`. ESLint
  `no-restricted-imports` enforces the layering.
- **Errors are chosen in one place.** `lib/errorHandler.ts` returns `detail`, then `title`, then the
  client's own `errors.noAnswer`; `errors.signInLoop` is the second client-authored sentence. There
  is no status-to-sentence table. The admin is localised in en/ru/hy like the client — it is no
  longer an English-only panel.
- **The kit is `components/ui/` (`Ui*`), and screens are composed from it.** Views, layouts and area
  components carry no raw `button/input/select/textarea/table/form/dialog/a` (`vue/no-restricted-html-elements`);
  a new visual need is a new `Ui*` with its test first. No `<style>`, no `style=`, no `v-html`
  (`UiIcon` builds SVG nodes with `h()` from a whitelist); Tailwind 4 classes are literals and colours
  are theme tokens in `assets/main.css`. Every input-like has a `placeholder` (`local/require-placeholder`)
  and every interactive element a `data-testid` (`<area>-<element>[-<id>]`). chart.js is only used
  behind `UiChart`; reports share one scaffold (`ReportPage`, `ReportTable`, `ReportQueryPage`).
- **Closed sets are enums or `as const` objects with a derived union** (`AuthMode`, `TicketStatus`,
  `InvoiceBulkAction`, `CancellationType`, `UiTone`, ...); tones for API status words live in
  `lib/tones.ts`. A screen whose feature is not built (affiliates, addons, disputes, offline card
  processing) renders `ComingSoonPage` — never placeholder records.
- **Locales.** `src/locales/{en,ru,hy}/<area>.json`, one file per area, the file name being the first
  key segment (`nav.json` holds `nav.*`); `locales/messages.ts` assembles them with
  `import.meta.glob` and `i18n.ts` loads the result. All three languages change together.
  `npm run lint:i18n` fails on a key missing from or extra in any language, an unused key (a
  `` t(`area.${x}`) `` prefix counts as use of the keys under it) and a literal `t('...')` /
  `translate('...')` with no `en` entry; `tests/locales/parity.test.ts` also checks values and
  `{placeholders}`. A server-decided sentence is never copied into a locale file.
- **Tests mirror the source.** `tests/` mirrors `src/` (components, views, layouts, stores,
  composables, `composables/apis`, lib, middleware, utils), with a real kit, real Pinia and the API
  stubbed at the network level (`tests/helpers/{fetch,routes,mount}.ts`), never a mocked store or
  `useApi`. `tests/coverage-shape.test.ts` fails when a component, layout, view, store, composable,
  lib module or guard has no test importing it. `tests/lint/strictRules.test.ts` pins the lint rules.
  On this laptop vitest needs `--maxWorkers=2`.
- **e2e.** `admin/e2e/`: Playwright, one spec per flow, `data-testid` selectors only (`fieldOf` for
  inputs), against `vite preview` of a production build with the API replaced by `page.route`
  (`e2e/support/stubApi.ts`: stateful session endpoints, recorded calls, `unhandled` for anything
  nobody stubbed; fixtures typed from `src/types/` in `e2e/fixtures/`). Flows cover sign-in (password,
  SSO, sign-out), dashboard, clients (list, create, detail), billing (invoices, quotes, transactions),
  orders/services/domains, servers, support (tickets, announcements, knowledgebase), settings,
  currencies, slides (including the unsaved-changes guard), integrations, plugins, migration,
  reports, an API outage and the language switch.
- **Gates, in `.gitlab-ci.yml` `admin:test` then `admin:e2e` (blocking):** `npm run lint:ci`
  (oxlint + ESLint, non-fixing), `npm run lint:i18n`, `npm run typecheck` (`vue-tsc -b --force`),
  `npm test -- --maxWorkers=2`, `npm run build-only`, then `npm run e2e`. `npm run audit`
  (`scripts/audit.mjs`) prints the raw-element, `function`-declaration, `style=`, `v-html`, `any`
  and missing-placeholder counts, which must be zero.

## Tests

`backend/tests/` has five projects: `Domain.Tests`, `Integration.Tests`, `Architecture.Tests`,
`Innovayse.CWP.Tests`, `Innovayse.Inecobank.Tests`. **There is no `Application.Tests` or
`Infrastructure.Tests`** — use-case tests are `Integration.Tests/<Feature>/<UseCase>Tests.cs`
(Testcontainers Postgres, fakes in `Fixtures/` for external ports only), and adapter tests live in
`Integration.Tests/Adapters/{Auth,Billing,Integrations}`. Do not add either project.
`Integration.Tests/Security/LeakGuardTests` guards against leaking secrets/internals in
responses; `Fixtures/StubHttpServer` is the in-process HTTP fake for external APIs. The admin SPA
has vitest (`npm test`, `admin/tests/`) and Playwright (`npm run e2e`, `admin/e2e/`) — see "The admin's shape".
`Integration.Tests/Persistence/ModelDiffBaselineTests` is the schema guard (the spec's "snapshot unchanged" test): the EF differ's operations between the migrations snapshot and the live model must equal `Fixtures/model-diff-baseline.txt` (`(no differences)`), so an entity change without a migration fails.

**The Integration suite runs in five shards, one at a time, because one `dotnet test` host holds
the EF model and a Postgres container for every test class and grows to several gigabytes, and the
12 GB builder is shared with other products.** Two half-suite shards running together were
OOM-killed on every attempt. The shards (the same split as `.github/workflows/ci.yml`, where they run
in parallel as `Backend integration (1 of 5)` to `(5 of 5)`) are `backend:test:integration:a` to `:e`,
share a `resource_group` so only one runs at a time, and split by namespace with
`--filter "FullyQualifiedName~Integration.Tests.<Ns>."`: A is Adapters, Admin, Audit, Common, Health,
Identity, Security, Shared; B is Auth, Clients, Demo, Scheduling, Time and Billing classes A to K;
C is Billing classes L to Z, Reports, Slides, Faqs, Locations, Partners, Projects, Testimonials; D is
Domains, Support; E is Migration, Notifications, Orders, Persistence, Products, Provisioning, Services.
Billing in one shard took a hosted GitHub runner to its limit, so it is split by the first letter of
the test class name, one `FullyQualifiedName~Integration.Tests.Billing.<Letter>` entry per letter
(`Billing.Currencies` falls under C). **A new top-level folder in `Integration.Tests/` must be added
to one shard in `.gitlab-ci.yml` and in `ci.yml`, or its tests run nowhere;**
`scripts/check-integration-shards.sh` fails the pipeline when a folder is missing or doubled, or when
the letters of a split folder do not cover A to Z exactly once.

## Packages worth knowing

- **Branding images are rendered with SkiaSharp (MIT)**, in
  `Infrastructure/ImageProcessing/SkiaBrandingImageProcessor.cs`, behind the
  `IBrandingImageProcessor` port. Do not use `SixLabors.ImageSharp`: its Apache-2.0 2.1.x line
  receives no fixes and carries high-severity advisories, and 3.x is a split licence this public
  AGPL repository cannot hand to downstream users; `.github/dependabot.yml` ignores that package.
  `SkiaSharp` and `SkiaSharp.NativeAssets.Linux.NoDependencies` (so the Alpine image needs no apk
  packages) must stay on one version. What the safety of an upload still rests on is unchanged: the
  processor sniffs magic bytes first (PNG/JPEG/GIF/WebP/BMP only — Skia would otherwise also try
  ICO, WBMP and raw codecs), reads the header to enforce the edge and pixel ceilings before decoding
  any pixels, refuses SVG, and encodes PNG only. Keep that true. Downscales use mipmapped
  (trilinear) sampling on purpose — Skia's cubic filter aliases a 1024px mark reduced to a 16px
  favicon. A refusal is a `Result` failure (`BRANDING_IMAGE_INVALID` / `BRANDING_IMAGE_TOO_LARGE`);
  the precise cause is logged, never returned.

## Database identifiers

- Every database identifier is PascalCase: tables (plural, named by their `DbSet` property in
  `AppDbContext`, never by a `ToTable` call), columns, primary and foreign keys, indexes and
  constraints. Framework-owned tables (ASP.NET Identity, Data Protection) and the Postgres system
  column `xmin` keep their own names.
- This holds everywhere a name is written: the EF configuration, raw SQL inside migrations, and
  one-off SQL run by hand or from `scripts/`. Raw SQL double-quotes the name (`"SubjectRoles"`), or
  Postgres folds it to lower case.
- `Architecture.Tests/DatabaseIdentifierNamingTests` walks the EF model and fails on any table,
  column, key, foreign key, index, sequence or check constraint whose name is not PascalCase.
- A rename happens only through a migration that renames in place (`RenameTable`, `RenameColumn`,
  `RenameIndex`, and `ALTER TABLE ... RENAME CONSTRAINT` through `Sql`) and keeps every row. Never
  drop and recreate.
- Database identifiers are PascalCase, always, and the architecture guard test enforces it: tables, columns, primary/foreign
  keys, indexes, unique and check constraints, sequences, views and enum type names, for example
  `"TeamMembers"`, `"OrganizationId"`, `PK_TeamMembers`,
  `FK_TeamMembers_Organizations_OrganizationId`, `IX_TeamMembersSortOrder`,
  `CK_TeamMembersSortOrderNotNegative`. Never snake_case or lower case, and never an EF naming
  convention plugin that lowercases (no `UseSnakeCaseNamingConvention`). Raw SQL quotes every
  identifier in PascalCase. Only PostgreSQL built-ins (types like `jsonb`/`citext`, extensions
  like `pg_trgm`, operator classes like `gin_trgm_ops`) keep their own lower-case names. JSON
  stored inside a column keeps the camelCase wire shape. A migration or configuration that emits a
  non-PascalCase identifier is a review failure.

## Debt — recorded, not endorsed

Never spread one of these, and never restructure as a side effect of a feature.

- **Vendor names that remain, on purpose.** Integration slugs (`stripe`, `cwp`, ...) in
  `IntegrationSlugs` / `BuiltInPaymentModules` and the `Integrations/<Vendor>/` adapter folders are
  plugin identifiers: they are stored in settings rows, sent in URLs (`/api/admin/integrations/stripe`)
  and chosen in the admin UI, so renaming them would break stored configuration. They are the only
  place a vendor is named. Persisted *data* is neutral: `Client.CardGatewayCustomerId` and the ledger
  payment method `Card` (the `ConformanceFixes` migration renamed the column and rewrote old
  `Stripe` ledger rows).
- **`ClientRegisteredIntegrationEvent` carries five parameters nothing fills.**
  `RegisterHandler` publishes four arguments and stops, so `IpAddress`,
  `UserAgent`, `DeviceType`, `OperatingSystem` and `Browser` arrive null and `Client.Create`
  writes five mapped, migrated columns from them. Finish it by capturing all five from the
  request, not by trimming the record. Wolverine runs with discovery only — no broker — so the
  event never leaves the process.
- **Migration imports go through an importer registry.** `IMigrationEntityImporter` is the one
  contract: an importer names its `MigrationEntityType` in `EntityType` and imports that kind in
  `ImportAsync`. Every importer is registered in DI; adding an entity kind means adding an importer
  and its place in `MigrationImportOrder.Sequence`, the single place that fixes the order (clients
  first, since invoices, services, domains and tickets reference them); `Arrange` throws unless
  exactly one importer exists per kind in the sequence. `MigrationPullWorker` holds no per-entity code: it fetches the totals, then iterates
  the registered importers in that order and runs each one the job selected (`job.Includes(EntityType)`).
  The contract sits in `Common/Interfaces` but is not a port: the importers are Application code, so
  `PortPlacementTests` lists it under `ApplicationContracts` instead of asking for an Infrastructure adapter.
- **The migration job model is slim.** `ImportBatch` and `PingMigrationJob` are removed;
  `CreateMigrationJobCommand` selects entities with `MigrationEntitySelectionDto`.
- **`Infrastructure/Migration/` cannot exist.** A namespace `Innovayse.Hostpanel.Infrastructure.Migration`
  shadows EF Core's `Migration` base class for every file in `Infrastructure.Migrations` (CS0118).
  The migration subsystem's infrastructure lives in `Infrastructure/Integrations/Migration/`;
  do not "correct" it.
- **The migration source addon is PHP, in `backend/plugins/innovayse_migration/`.** It runs inside the
  source billing system and answers `MigrationSourceClient`. `modules/addons/innovayse_migration/` is
  what is installed: the two entry points the source system calls (`innovayse_migration.php`,
  `api.php`) stay thin and delegate to `lib/` (PHP 8.1, `strict_types`, PSR-4 namespace
  `HostpanelMigration\`, one class per file, docblocks on every class, method and property). One
  `Exporter` per record kind, one `Dataset` filter shared by the totals and the pages, one
  `SourceDatabase` gateway. Its wire members are the C# `*Wire`/`*Record` names in camelCase and change
  together with them. `composer.json`, `phpunit.xml.dist` and `tests/` sit beside `modules/` and are
  never packaged (`composer test` needs `pdo_sqlite` and runs in CI as `migration-addon` in
  `.github/workflows/ci.yml` and `backend:test:migration-addon` in `.gitlab-ci.yml`): `MigrationPluginArchive` zips `modules/` only. The API project copies `modules/`
  into its build and publish output as `migration-plugin/modules` (never under `plugins/`, which
  `PluginLoader` scans), and the archive reads it from `MigrationPlugin:Path` (`MigrationPluginOptions`,
  default `migration-plugin` beside the binaries, blank refused at startup), so the download works on a
  developer machine, in the tests and in the published image alike; a missing folder answers
  `MIGRATION_PLUGIN_NOT_FOUND` (404). The source keeps no product prices on the product row: the
  products exporter reads monthly and annual prices from the source's pricing table in its default
  currency. It has no order items table either: an order's lines are the services, domains and addons
  rows that carry the order id.

## Where things go

| Kind | Path |
|---|---|
| HTTP endpoint | `backend/src/Innovayse.Hostpanel.API/Controllers/<Name>Controller.cs` (or the feature folder that already holds its siblings) — derives `BaseApiController` |
| Command / query + handler + validator | `backend/src/Innovayse.Hostpanel.Application/<Feature>/{Commands,Queries}/<UseCase>/` |
| DTO shared by several use cases in a feature | `.../Application/<Feature>/Common/` |
| DTO used by exactly one use case | that use case's own folder |
| Port | `.../Application/Common/Interfaces/I<Port>.cs` — nowhere else |
| Error code → status | `backend/src/Shared/Controllers/StatusMapping.cs` |
| Sentence a person reads for a refusal | `.../Application/Resources/ErrorMessages*.resx` (en, ru, hy together) |
| Entity, domain event, enum | `backend/src/Innovayse.Hostpanel.Domain/<Area>/` (`Events/`, `Enums/`) |
| EF configuration, migration, adapter | `backend/src/Innovayse.Hostpanel.Infrastructure/` |
| External service client | `.../Infrastructure/Integrations/<Provider>/` |
| Use-case test | `backend/tests/Innovayse.Hostpanel.Integration.Tests/<Feature>/<UseCase>Tests.cs` |
| Options class | `.../<Feature or Integration>/Options/<Name>Options.cs` |
| Portal page / section | `client/templates/<template>/pages/`, `.../sections/` |
| Shared component | `client/components/`, `client/components/ui/` |
| API call | `client/composables/apis/` |
| Nitro BFF route | `client/server/api/portal/` |
| Exported TS type | `client/types/` |
| Admin screen | `admin/src/views/<Page>View.vue` (+ `admin/src/components/<area>/`) |
| Admin store / API call | `admin/src/stores/<area>.ts` / `admin/src/composables/apis/use<Area>Api.ts` |
| Admin e2e flow | `admin/e2e/<flow>.spec.ts` (fixtures in `admin/e2e/fixtures/`) |
| Plan / spec | `docs/superpowers/plans/`, `docs/superpowers/specs/` |

## Gotchas

- **`dotnet watch` in `hostpanel-api` dies with "An item with the same key has already been
  added".** `DOTNET_USE_POLLING_FILE_WATCHER=1` makes the container enumerate the bind mount on
  a loop, and enumerating a directory being written to occasionally returns an entry twice over
  the Docker Desktop mount. It is a mount artefact, not your build. Recover with
  `stop` then `up -d`, never `restart`. The API itself serves fine; only hot reload is lost.
  This is also why `Innovayse.Hostpanel.API.csproj` carries `<Watch Remove="logs/**/*" />` — and
  `<Content Remove>`, so a publish does not ship yesterday's logs.
- **Wolverine logs `Invocation of {Message} failed!` at Error with a stack trace for every
  handler exception**, before its own policies get a say, and 5.31.0 has no knob for it. A
  staff identity with no client row is not a fault, and four such traces per dashboard load
  made a healthy platform read as a failing one. `ControlFlowExceptionLogFilter` drops exactly
  those lines; extend the filter rather than removing it.
- **The two-factor attempt cap and replay guard are per API instance.** `TwoFactorChallengeService`
  and `TwoFactorCodeGuard` keep their counters in `IMemoryCache` with `CacheItemPriority.NeverRemove`,
  so memory pressure cannot evict a counter and reopen the cap; they fail closed. They hold for one API
  instance, which is the supported deployment; running several API instances needs a distributed store
  for these counters first. The password lockout (`SignInLockout:MaxFailedAttempts`, `LockoutMinutes`)
  is kept on the account row and holds across instances.
- **Migrations are applied on startup in every environment except `Testing`**, so every deployed
  environment's schema follows the code.
- **Role seeding is skipped when this deployment does not own its users.** Under `sso` mode
  Identity is not registered, so `GetRequiredService<RoleManager<…>>` threw before the API
  finished starting and the container never came up.
- **`Innovayse.Providers.Inecobank` targets `net8.0`** while everything else targets `net9.0`;
  the MSBuild staging target pins that TFM in its copy paths. Retargeting the provider means
  editing those paths.
