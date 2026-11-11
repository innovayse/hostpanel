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

**This is a public, separately licensed repository.** AGPL-3.0-or-later, except the plugin SDK (`backend/src/Hostpanel.Sdk`), which is MIT (its own `LICENSE`, packed into the package) so a plugin may be free or paid, open or closed; `SdkPackageTests` pins the package metadata. The SDK lives in its own public repository, **github.com/innovayse/hostpanel-sdk**, and is a git submodule at that same path (`.gitmodules`): clone with `--recurse-submodules` (or run `git submodule update --init` after a plain clone), and an SDK change lands as a pull request there first, then as a submodule bump here. `origin` is **github.com/innovayse/hostpanel**. Anything written here
is read by people who only have a clone of this repository, and the repo must build from
a clone of itself alone (with its submodule) — that is why `Innovayse.Auth` is consumed as an optional package (only in an
`InnovayseSso=true` build, see `backend/Directory.Build.props`) rather than a project reference, and why `backend/nuget.config` restricts `auditSources` to nuget.org.

**Read first:** the six rules files beside this one — [clean-architecture.md](clean-architecture.md),
[csharp-style.md](csharp-style.md), [dotnet-format.md](dotnet-format.md),
[xml-docs.md](xml-docs.md), [jsdoc.md](jsdoc.md), [vuejs-style.md](vuejs-style.md) — then this
file. They are part of this repository: change them here.

## This project's own rules — read this before your first review

This backend follows one shape: no primary constructors on handlers or controllers, no
`*Request` records, no exception-only refusals, no feature-local `Interfaces/` folders, no
repositories. What is specific to this project is short:

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
- **Anonymous public storefront feeds.** `/api/public/{faqs,locations,partners,product-groups,products,projects,status,testimonials}` (like `/api/public/slides` and
  `/api/public/demo` before them) are `[AllowAnonymous]` read-only queries: the storefront renders them before anyone has
  signed in, so they cannot carry the usual `[Authorize]` on every action. They stay on `BaseApiController`, dispatch one
  query, return only active, already-localised records and never expose an admin field; writes remain behind `Policies.Admin`.
- **Handler and validator naming is `<UseCase>Handler` / `<UseCase>Validator`.** Use-case names are unique across modules (`ProvisionService` in Provisioning,
  `SetupService` in Services), one use case per state transition (`SuspendService`, `UnsuspendService`,
  `TerminateService`: every route, cron and status change dispatches them, and each checks the state
  before it asks the panel), and a DTO two use cases share lives once in `<Module>/Common/` (one
  `ProductPriceDto`, one `FileDownloadDto` in `Application/Common/` for every export and download).
- **No `*Request` types in Application, API or Domain.** Provider inputs in Domain
  are named for what they carry (`DomainRegistrationOrder`, `DomainRenewalOrder`,
  `DomainTransferOrder`, `ProvisionAccountOrder`, `SuspendAccountOrder`, `TerminateAccountOrder`);
  `NoRequestTypesTests` covers all three layers (the `CancellationRequest` entity is exempt). The wire models inside
  `Innovayse.Providers.*` are third-party-facing contracts and keep their names.
- **Recurring jobs and background handlers return `Task`, not `Result`.** The crons
  (`CheckOverdueInvoicesCron`, `ProcessBillableItemsCron`, `ProcessRenewalsCron`,
  `ReconcileGatewayPaymentsCron`, `SendRenewalRemindersCron`, `SuspendOverdueServicesCron`,
  `AutoTerminateSuspendedCron`, `SyncServerAccountCountsCron`, `DispatchFulfilmentCron`, `DispatchEmailCampaignsCron`) and `ResetDemoData`,
  `CheckDomainExpiries` and `SyncPendingDomainStatuses` are scheduled messages, not dispatched by a
  caller, so nothing would receive a `Result`. So are `RunMigrationPull` (published by
  `MigrationPullQueue` once a job's queued state is saved; the job row records how the pull ended; when two
  deliveries of one pull race, the save that starts the job (`job.Start`) loses with `ConcurrencyConflictException`
  and the worker logs it at Information and returns without touching the job, which the winner runs)
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
  scheduled run is in memory, so a host that is stopping ends the chain and the next start catches up. It stays in
  memory although every other local queue is durable (see "Durable messaging"): the chain re-schedules itself after
  each pass and every start re-creates it from `JobRuns`, so a scheduled run that survived a restart would be
  scheduled a second time and each restart would add a chain.
  `DispatchFulfilmentCron` runs every 30 seconds and delivers the due `FulfilmentRecords` to their plugins
  (`FulfilmentDispatcher`, claim with a lease, back-off, paid then refunded then lapsed per invoice and plugin);
  `DispatchFulfilmentNow` is one extra pass of the same job under the same lease, scheduling nothing, for the
  kick after a commit, so a kick never starts a second chain. `JobSchedule.RestAfterSuccess` keeps the rest of
  an interval job under half its interval, or the 30-second job would run every second beat.
- **Our own API contracts carry no `[JsonPropertyName]`**: the wire name is the property name in camelCase.
  A route id is overlaid on the bound command with `with { ... }`; there are no `*Body` types. The SDK ports are
  vendor-free too (`IHostingPanelApiClient`, `IHostingPanelV7ApiClient`; `NoVendorNamesTests` covers the SDK: its type names, and the text of its comments and strings, which also name no host-internal type).
- **Errors are one `ErrorResponse` body, `application/problem+json`**
  (`Type`/`Title`/`Status`/`Detail`/`Errors`, camelCase on the wire), written by one serializer,
  `Shared/Infrastructure/ErrorBody.cs`, for every surface: `BaseApiController`,
  `ExceptionHandlingMiddleware`, the model-state factory, the auth-event writers and the rate
  limiter. The body carries only those members, and both frontends read `type`/`detail`; do not
  add an `error`/`code` pair beside them.
  `EventStreamResult` and `ServerSentEvent` (the `text/event-stream` writer and its framing) live in `Shared/Infrastructure` beside `ErrorBody` for the same reason: they are the host's own response writers, not controllers or use case code.
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
  SharedKernel/                      BaseEntity (DomainEvents), AuditableEntity, DomainException, IDomainEvent,
                                     SecretMembers, SecretMemberAttribute (Domain/ beside them; no TenantEntity)
  Shared/                            BaseApiController + StatusMapping, Result/Result<T>, ErrorResponse,
                                     ErrorBody, middleware, validation policy, resources, options
  Innovayse.Hostpanel.Domain/        entities, events, Enums/ per area. References the kernel only.
  Innovayse.Hostpanel.Application/   <Feature>/{Commands,Queries}/<UseCase>/, <Feature>/Common/,
                                     Common/Interfaces/ (every port), Common/Errors/, Resources/
  Innovayse.Hostpanel.Infrastructure/ AppDbContext, migrations, <Capability>/ adapters, Integrations/
  Innovayse.Hostpanel.API/           controllers, Program.cs, composition root
  Hostpanel.Sdk/                     the plugin contract third parties compile against (git submodule, own repository)
  Innovayse.Providers.CWP/ CWP7/   provisioning providers (payment gateways are external plugins, none ships here)
backend/tests/                       Domain, Integration, Architecture, CWP
client/  admin/                    Nuxt 4 portal, Vite admin SPA — each owns its Dockerfile (backend/Dockerfile too)
docker/                              compose set (shared·dev·prod·platform), nginx/, scripts/, .env.example — docker/README.md
```

- **SharedKernel holds `Domain/SecretMembers.cs` and `Domain/SecretMemberAttribute.cs`, and has no
  `TenantEntity`** (this product has no tenant entity). The reason for the two files:
  one masking rule shared by every layer. A record that carries a secret prints it
  through the generated `ToString` into logs and exception messages, so Domain, Application
  and Infrastructure all need the same answer to "which members are secret"
  and the same printer. Only the kernel is visible to all of them, and
  `SecretRecordPrintingTests` uses the same `SecretMembers.IsSecret` the printer uses.
- **No repositories.** Handlers use `IHostpanelDbContext` (`Application/Common/Interfaces/`)
  directly, and the DbContext is the unit of work: a handler saves with
  `IHostpanelDbContext.SaveChangesAsync`, which also translates a concurrency conflict to
  `ConcurrencyConflictException`. There is no separate unit-of-work port.
  Domain has no `Interfaces/` folders.
- **Every application port is in `Application/Common/Interfaces/`** (`IPaymentPluginResolver`,
  `IProvisioningProvider`, `IEmailSender`, `IIdentityProvider`, `IUserService`, …) and
  `PortPlacementTests` fails on one anywhere else. Adapters sit in Infrastructure under a
  capability folder (`Storage/`, `Email/`, `Integrations/<Provider>/`, `Auth/`); EF configurations sit in `Persistence/Configurations/`.
  Two documented exceptions in `PortPlacementTests`. A request-scoped adapter lives under `API/` and
  sits in the test's allow-list with what it reads (today `API/Identity/HttpCallerAccessToken`, the `Authorization`
  header, behind `ICallerAccessToken`, and `API/Identity/HttpCallerClientMemo`, the request's `HttpContext.Items`, behind
  `ICallerClientMemo`). `Innovayse.Providers.CWP7` implements
  `IProvisioningProvider` and is not scanned: it is not a plugin, Infrastructure links it and
  `ProvisioningProviderFactory` builds it per server, so it is an Infrastructure adapter in its
  own assembly. The SDK's `IProvisioningPlugin` is only a marker with no operations, so there is
  no SDK contract it could implement instead.
- **The cPanel provider lives in the host for now.** `WhmProvisioningProvider`, `WhmApiClient` and their
  models (`Infrastructure/Integrations/CPanel/Whm/`) implement `IProvisioningProvider` and are built per server
  by `ProvisioningProviderFactory`, like the CWP7 and Plesk providers, and not shipped as a plugin. The reason
  is the same: the SDK has no provisioning contract a plugin could implement (`IProvisioningPlugin` is a marker, and the
  hosting panel contract it replaces is obsolete), the server form, the connection test, the disk usage report and
  the single sign-on and credentials screens all reach a panel through the host's own port, and a plugin cannot
  yet supply those. Moving the provider out is wanted once the SDK carries that contract, and it is tracked in
  #997; until then the provider stays in Infrastructure, takes its numbers from `ProvisioningOptions`
  (`CPanelWhmPort` and `CPanelUserPort`, both checked to be 1 to 65535 at startup by
  `ProvisioningOptionsValidator`), and its HTTP client is registered only through
  `AddCPanelWhmHttpClient`, which the tests resolve too. It does not trust what WHM sends back: a
  single sign-on address is passed on only as an https address on the server's own host (`WhmInputRules`),
  a username or domain is checked before WHM is called, and a reply is capped at `WhmApiClient.MaxResponseBytes`.
- **"Test Connection" runs a live probe where a provider has one.** `IIntegrationConnectionProbe`
  (a port in `Application/Common/Interfaces/`, keyed by integration slug) is implemented per
  integration in `Infrastructure/Integrations/<Provider>/` (today `NameAmConnectionProbe`, which
  logs in with the saved credentials) and every implementation is registered in
  `DependencyInjection`; `TestIntegrationConnectionHandler` picks the one whose `Slug` matches once
  every required field is stored. A probe failure is logged and answered with a fixed sentence —
  the provider's own text never reaches the response. `NameAmClient` reads the e-mail, password,
  API URL and test mode saved on the admin Integrations page over the bound options
  (`IsConfiguredAsync`), so the page configures what requests use.
- **Plugin integrations on the Integrations page (#854, #855, #856).** A plugin's form texts are resolved per request:
  `IntegrationFields.Resolve` maps each `GetSettings()` entry with `ToFieldDefinition(PluginUiTexts)`, so the label, the
  help text (`description`), the `placeholder` and every select option label (`optionLabels`, keyed by the stored value;
  `options` stays the values) come in the admin's `X-Language` from the plugin's locales, English where a locale lacks the
  key, else the text as written. Nothing is resolved at load: the loader's `PluginIntegrationEntry` carries only the
  `RequiredSettingKeys` the list needs. A plugin is tested and shown connected through the SDK's optional `IPluginStatus`
  only: the port `IPluginStatusProbe` (`Infrastructure/Plugins/PluginStatusProbe`) checks the implementation type without
  running plugin code (`Supports`), then goes through `PluginActivator.PrepareAsync<IPluginStatus>` (switched on, configured,
  no pending migration; otherwise `Inactive`, the plugin is not called) and waits at most `PluginApi:StatusTimeoutSeconds`
  (default 5, 1 to 60): the token fires and the host stops waiting at the same moment, even for a plugin that ignores it.
  It fails closed: a throw, a null report or an undefined health is `Failed`, no answer in time is `TimedOut`, both logged by
  plugin id and exception type only (never the message); the plugin's `Detail` is trimmed and cut to 500 characters.
  `PluginConnectionReader` (Application, the one reader) caches the language-neutral result per plugin
  (`PluginStatusCacheKeys`, `PluginApi:StatusCacheSeconds`, default 300, like the hosting panel's server info), and words it
  per read: the plugin's own `Detail` through its locales (`PluginText`) when it reported, else the host's sentence by code
  (`PLUGIN_STATUS_HEALTHY`, `_DEGRADED`, `_REPORTED_DOWN`, `_INACTIVE`, `_TIMED_OUT`, `_FAILED`, sentences in
  `ErrorMessages` en/ru/hy; they are answer sentences, never refusals). The list (`IntegrationListItemDto.Connection`, only
  for a switched-on plugin) and the detail (`Connection`) read the cache; Test Connection (`TestIntegrationConnectionHandler`,
  for a slug that is not a built-in) asks now and replaces the cache entry, answering success for `Connected` and
  `Degraded`; a save of the integration drops the entry. `IntegrationDetailDto.CanTestConnection` is true for a built-in the
  test knows and for a plugin that implements `IPluginStatus`; the admin hides Test Connection otherwise, and the route
  answers 400 `INTEGRATION_NOT_TESTABLE` for such a plugin. The admin card and the status sidebar show the state
  (`integrations.connectionState.*`, tone from `lib/tones.ts`) with the API's sentence.
- **Enums live in `Enums/` namespaces** (`Domain/<Area>/Enums/`, `SharedKernel/Domain/Enums/`,
  `Infrastructure/Email/Enums/`); `EnumPlacementTests` enforces it.
- **Domain refusals are `DomainException(code, message)`** (abstract, in the kernel); the exception
  middleware turns an uncaught one into a body whose status comes from `StatusMapping.For(code)`
  and whose sentence is the localised one for that code. A handler that can foresee a refusal returns
  `Result.Failure` instead.
- **Plugins are loaded reflectively, not linked.** `PluginLoader.DiscoverAndRegister` scans
  `AppContext.BaseDirectory/plugins`. No plugin is built or staged with the host: payment gateways (card, bank) are
  external plugins an operator installs from the Plugins page. A plugin package must never carry its own copy of
  `Hostpanel.Sdk.dll` into the load path: `Assembly.LoadFrom` over it would give the process two distinct
  `IPaymentPlugin` types with the same name, after which every cast in `PluginActivator` fails. The CWP providers are
  deliberately **not** staged, so they cannot activate at runtime by accident.
- **Plugin assembly names.** `PluginAssemblyNames` (the plugin packaging package, "Plugin package checks"; the host's names go in as a `PluginAssemblyOptions`, written once in `Infrastructure/Plugins/PluginHostAssemblies.Options` and used by every caller) reserves `Innovayse.Hostpanel.`, `System.`, `Microsoft.`, the SDK (`Hostpanel.Sdk`, and its former name `Innovayse.SDK`, which a plugin still may not ship) and every
  trusted platform assembly (`PLUGIN_HOST_ASSEMBLY_REFUSED`), with one carve-out: `Innovayse.Hostpanel.Plugins.<Name>` is the
  plugin namespace and official plugins are named that way (`Innovayse.Hostpanel.Plugins.Stripe.dll`). The trailing dot is the
  boundary, so `Innovayse.Hostpanel.PluginsX` and `Innovayse.Hostpanel.Plugins` stay reserved. No host assembly or project may
  start with `Innovayse.Hostpanel.Plugins.` (`PluginAssemblyNamespaceTests`, which reads the prefix from `PluginHostAssemblies.Options`), so a plugin can never shadow a host assembly.
  `PluginAssemblyNamesTests` (in the SDK repository) and `InstallPluginTests` pin the accepted and refused names.
  `Assembly.LoadFrom` binds a name to the first copy loaded, so the same check also refuses a package whose assembly file
  name (case-insensitive) is shipped by another installed, staged or built-in plugin (`PLUGIN_ASSEMBLY_CONFLICT`, 400,
  `PluginAssemblyConflicts`, run right after the host-assembly check). The plugin's own installed and staged folders are
  skipped, so an upgrade never conflicts with itself; the conflicting plugin id goes to the log, not the response.
  `PluginAssemblyConflictsTests` and `InstallPluginTests` pin it.
- **Plugin projects are named `Innovayse.Hostpanel.Plugins.<Name>` (#848).** The .NET plugin convention
  `<Company>.<Product>.<Type>.<Name>` (as in `Serilog.Sinks.Console`): project, folder, assembly and root
  namespace are `Innovayse.Hostpanel.Plugins.<Name>`, its tests `Innovayse.Hostpanel.Plugins.<Name>.Tests`, and a
  plugin in its own repository lives in a repository named `hostpanel-<name>`. The plugin `id` in `plugin.json` never
  changes with a rename, because stored settings and plugin data are keyed by it. The in-tree
  `Innovayse.Providers.*` projects and `Innovayse.Inecobank.Tests` predate the convention and take it as each
  moves to its own repository (#780, #841).
- **The SDK is grouped by area (since SDK 2.0.0; the submodule is at 3.2.0, which keeps what 2.1.0 added, the `Ui`
  view schema, `externalOrigins`, the endpoints capability and the fulfilment `ClientRef`, and adds the plugin
  packaging package, `Hostpanel.Sdk.Packaging`).** Every SDK type lives in
  `Plugins/<Area>/` and its namespace equals the folder: `Payment`, `Provisioning`, `Registrar`, `HostingPanel`, `Hosting`,
  `Fulfilment`, `Identity`, `StoredCredentials`, `Manifest`, `Api`, `Endpoints`, `Host`, `Store`,
  `Lifecycle`, `Slots`, `Ui`, `Enums`. Each interface sits next to the records it uses; there is no
  `Interfaces/` folder (one exists only in Domain, `Application/Common/Interfaces/` and
  Shared) and nothing at the `Plugins/` root. Enums are in `Plugins/Enums/` with explicit values:
  `PluginType` keeps its released values starting at 0, every other enum starts at 1. The abstract base
  classes a plugin derives from live in the `Hostpanel.Sdk.Base` namespace (`Base/`), not in an area. Inputs are `*Instruction` or `*Context`, outputs `*Result`, `*Status`,
  `*Lookup` or `*Session`; never `*Request`. `SdkContractTests` pins every namespace.
- **Seven plugin types, one activator.** The loader registers each plugin under the SDK contract of the
  type its manifest declares: provisioning, payment, registrar, identity (`IClientResolver`),
  fulfilment (`IFulfilmentPlugin`), hosted (`IHostedPlugin`) and app (`IPluginEndpoints`). Every plugin becomes an instance in
  one place, `Infrastructure/Plugins/PluginActivator`: it checks the loaded type really implements the
  contract, that `integration:{id}:is_enabled` is `true` and that every required manifest setting
  (`manifest.GetSettings()`, which falls back to the legacy `fields`) is stored, then builds the plugin with its own configuration. The resolvers only choose:
  `PaymentPluginResolver` (`IPaymentPluginResolver`, one plugin by module id),
  `FulfilmentPluginResolver` (`IFulfilmentPluginResolver`, every enabled fulfilment plugin in id order) and
  `IdentityPluginResolver` (`IIdentityPluginResolver`, the one enabled identity plugin; two or more
  enabled throw `IdentityPluginConflictException` rather than picking one). `HostedPluginRunner` starts
  every enabled hosted plugin (manifest type `Hosted`, or any plugin that also implements
  `IHostedPlugin`) once the host has started and stops them as it begins to stop; an activation, start or
  stop that throws is logged with the plugin id and never stops the host or the other plugins. A plugin
  is enabled from the admin Integrations page, where it is listed (with its type) when its manifest sets
  `showInIntegrations`.
- **Capabilities (#818).** A manifest may list `capabilities`, names from the SDK's `PluginCapabilities`:
  `storedCredentials` (a payment plugin only, the class also implements `IStoredCredentialPlugin`) and `endpoints`
  (any type, the class also implements `IPluginEndpoints`). A **serving plugin** is an app plugin or a plugin of any
  other type that declares `endpoints`; `PluginManifestCapabilityExtensions.ServesEndpoints()`
  (`Infrastructure/Plugins/Extensions`) is the one place that decides it, and the loader (which also registers such a
  plugin under `IPluginEndpoints`), `PluginEndpointResolver`, `PluginUiSource` and the manifest validator all ask it.
  So a `Fulfilment` plugin that sells something and gives the client a screen is one plugin, with one `ClientRef` on
  both sides (#757). `PluginActivator` checks every declared capability against the interface it promises, after the
  enabled switch and before the required settings: one that is not implemented refuses the plugin for **every**
  contract (`PluginActivationOutcome.CapabilityNotImplemented`, logged with the plugin id and the capability), so a
  plugin cannot sell while its screen silently 404s. The admin plugins list shows each declared capability
  (`PluginListItemDto.Capabilities`). **Behaviour change:** this applies to `storedCredentials` too, as #818 asks, so a payment
  plugin that declares it without implementing `IStoredCredentialPlugin` is no longer a plain payment plugin with no
  stored cards: it is not activated at all (no shipped manifest declares it). `IdentityPluginResolver` counts such a
  plugin as enabled for its conflict check, as it does one missing a required setting. The manifest validator refuses
  an unknown capability, one declared twice and `storedCredentials` outside a payment plugin
  (`PLUGIN_CAPABILITY_NOT_ALLOWED`); `docs/en/developers/write-a-plugin.md` documents both.
- **Stored credentials are an optional payment capability.** `IStoredCredentialPlugin` (SDK) is implemented by a payment plugin on top of `IPaymentPlugin`; `IStoredCredentialPluginResolver` resolves it on top of `IPaymentPluginResolver` and answers nothing when the chosen plugin does not implement it. It is not a seventh plugin type and has no manifest type of its own.
- **What the three newer plugin types promise.** *Fulfilment* (`IFulfilmentPlugin`): the host writes a
  `FulfilmentRecords` row in the payment's own transaction and `DispatchFulfilmentCron` delivers it, so delivery is
  **at least once** with back-off; every method must be idempotent on `InvoiceId` (the same paid invoice can arrive
  twice), a throw marks the attempt failed and retries, a normal return marks it done. The host calls
  `OnInvoicePaidAsync` once a paid invoice has a digital line, `OnInvoiceRefundedAsync` after money moved back,
  `OnSubscriptionLapsedAsync` when a renewal invoice passes its grace period unpaid, and `AdviseRefundAsync`
  **before** a refund moves money. Every fulfilment context and the refund advice context carry `ClientRef`, the
  receiving plugin's own `IPluginReferences.ForClient(pluginId, clientId)`, the same value that plugin's endpoint calls
  for that client carry; it is computed per plugin by `FulfilmentDispatcher` and `RefundAdviser`, and
  `FulfilmentContextBuilder` (pure) only places it. *Refund advice* (`RefundAdviser`, the one place that asks): every enabled
  fulfilment plugin is asked, the smallest `MaxRefundableAmount` wins, a cap of zero or less refuses the refund
  (`REFUND_BLOCKED_BY_PLUGIN`), a smaller positive cap reduces it and the plugin's `Reason` goes into the refund
  notes, and a plugin that throws or times out refuses it too (`REFUND_ADVICE_UNAVAILABLE`, fail closed). With no
  fulfilment plugin enabled the refund is unchanged. *Identity* (`IClientResolver`): when one is enabled, every
  client-portal route asks `ResolveAsync(principal)` for the acting client instead of the default owner-subject rule;
  `null` means no client profile. `Policies.Client` is a `ClientPortalRequirement` (`ClientPortalAccessHandler`, scoped):
  a user holding the stored client role passes without a plugin call, anyone else passes when the resolver reports an
  external account, so a member the plugin vouches for reaches the portal without a role and nothing is stored (a plugin
  that stops vouching applies on the next request); with no plugin a roleless user is refused. A signed-in principal the
  plugin refuses resolves to `CallerClientAccess.Refused` (`IdentityRefused`), and `PlaceOrderHandler` answers
  `CLIENT_PERMISSION_DENIED` for it instead of creating a client, while an unauthenticated caller stays
  `CallerClientAccess.None`. The policy is not role-only: a staff member (admin or support, who hold no client role)
  that an enabled plugin vouches for also passes `Policies.Client`, because the policy asks the resolver for anyone
  without the client role. `CallerClientResolver` keeps its answer for the request (`ICallerClientMemo`, kept in `HttpContext.Items`, because the bus runs a use case in its own scope), so the policy, the
  permission filter and the use case ask the plugin once; `ICallerClientResolver.Invalidate()` is used only by
  `PlaceOrderHandler`, after it creates the caller's client row. A plugin that throws, times out, or two enabled
  identity plugins (`IdentityPluginConflictException`) fail closed in that one resolver, logged by exception type
  and plugin ids only: the answer is `CallerClientAccess.Unavailable` (`IdentityUnavailable`), the portal policy
  refuses it, the permission filter, `IClientAccess.Require*` and `PlaceOrderHandler` answer 503
  `IDENTITY_PLUGIN_UNAVAILABLE` with `Retry-After` and create nothing, and so does the portal policy itself, through `IdentityPluginUnavailableResponder` (it reads the request memo, which is stored per subject, and keeps every other refusal a plain 403); `IdentityPluginUnavailableConvention` declares the 503 once for every portal action; the account read (`GetMyAccount`) degrades to no client
  instead, so sign-in is not blocked. The host links the returned `ExternalAccount` (provider plus id) to a client row,
  creating it on the first order, and `IdentityPluginPermissions` maps the rights to client permissions: every
  admitted principal gets the read-only flags (services, domains, open tickets, emails); `CanPurchase` adds
  `PlaceNewOrders` and `ViewPayInvoices`; `CanManagePaymentMethods` adds `ViewPayInvoices` (the payment method
  actions are guarded by it). A plugin can grant nothing else. *Hosted* (`IHostedPlugin`): `StartAsync` runs once
  per process after the host started and `StopAsync` as it begins to stop; return from `StartAsync` once the worker
  runs, do not block on its loop.
- **The core is plugin neutral.** `PluginNeutralCoreTests` fails when a type in the Domain, Application
  Infrastructure or API assembly (base type, interface, attribute, member signature or called method), or one of
  their referenced assemblies, sits under a reserved namespace `Innovayse.<Segment>`. The reserved segments and the
  allow-list are in one place, `ReservedPluginNamespaces` in `Architecture.Tests` (today `Plugins`, `Deployment`,
  `Custom` and `Auth`; allow-listed: `Innovayse.Auth`, the optional external identity provider package, which is
  referenced only inside `#if INNOVAYSE_SSO`). A deployment-specific plugin is reached through an SDK contract
  loaded by `PluginLoader`, never named by the core. The test proves it bites: the same rule runs on a planted type
  in the test assembly and must report it.
  The same rule also runs on source text (#650): `PluginNeutralSourceTests` reads the plugin ids and the entry point
  type names from the in-tree `plugin.json` manifests (`Innovayse.Providers.*`) and fails when a Domain, Application,
  Infrastructure, API, Shared or SharedKernel file names one, apart from the shrinking `LegacyBuiltIn` list (a stale entry
  fails too); it is proven on an in-memory planted source. The host interfaces are held to it by vitest,
  `admin/tests/lint/pluginNeutrality.test.ts` (the slot and plugin page components with no exemption) and
  `client/tests/config/pluginNeutrality.test.ts`. With no plugin installed `EmptyPluginsTests` asserts that every slot index
  and both plugin ui indexes (pages and menus) are empty lists, so the panels show only what the core provides. Both
  panels render the index (the client's side menu, the admin's Add-ons section, #645 and #647) and draw no plugin
  entry while it is empty.
- **The plugin contract at a glance (#650).** Where each part is specified; do not restate it elsewhere, link here.
  - **Manifest v2** (`"manifestVersion": 2`): `settings`, `capabilities`, `locales`, `api`, `ui`, `externalOrigins`. The rules
    are in "Manifest v2" and "One manifest validator" in this section and in "Menu icons and page ids".
  - **View schema** (`PluginViews` in the SDK, version 1): a JSON document `{ "schema": 1, "blocks": [...] }` named by the
    `view` member of a page or slot entry. Block kinds: `header`, `text`, `stat`, `table`, `form`, `action`, `link`, `badge`,
    `empty`, `alert`, `tabs`, `chat`, `progress`, `split`, `preview`. Label members (`title`, `label`, `confirm`, `placeholder`, `text`) are
    localised by the host; route members (`route`, `send`, `history`) name a declared `api` route by method and path. Checked
    at load and install (`PluginViewDocumentReader`, the `PLUGIN_VIEW_*` rules); rendered as text only, never `v-html`.
    View state (#852, SDK 2.2.0, schema stays 1): `visibleWhen` on a block, `resetOnSuccess` in a form's `submit`,
    `followUp` on an action or a form's `submit`; see "View state" under the view document rules.
  - **Slot ids** (`PluginSlots`): `client.dashboard.cards`, `client.service.tabs` (context `serviceId`), `admin.client.panels`
    (`clientId`), `admin.service.panels` (`serviceId`, `clientId`); indexes at `GET api/{client|admin}/plugins/slots/{slotId}`
    (see "Plugin slot index"). The plugin ids `ui` and `slots` are reserved.
  - **Endpoint contract**: an app plugin answers only the routes its manifest `api` block declares, through
    `/api/client/plugins/{id}/{**path}` and `/api/admin/plugins/{id}/ext/{**path}`; every refusal before plugin code is the same
    404 `PLUGIN_ROUTE_NOT_FOUND` (see "Plugin routes: one safe door").
  - **Trust model**: an uploaded package carries a detached Ed25519 `plugin.sig`, checked at install and before load against the
    maintainer and operator keys; the admin setting `plugins.allow_unsigned` admits unsigned packages (audited, off by default,
    never enforced in Development and Testing); `Plugins:TrustedPackageSha256` pins archive digests; built-in plugins are part
    of the image and are not signature-checked (see "Signed plugin packages").
  - **For authors**: `docs/en/developers/write-a-plugin.md` is the guide. The SDK README and a sample plugin belong in the
    `hostpanel-sdk` repository, not in this checkout's submodule.
- **`PluginActivator` takes `IConfiguration` (documented exception to configuration.md rule 6).**
  The plugin SDK's base classes take an `IConfiguration` in their constructor, so the
  activator hands each plugin a configuration built from **its own section only**: the host's
  `integration:{id}` section with the plugin's `integration:{id}:*` settings rows layered over it
  (the rows are filtered by prefix in SQL, never the whole `Settings` table). Connection strings,
  signing keys and other integrations never reach a plugin. The keys keep their full path because
  the SDK base classes read `integration:{id}:{key}`; a relative `IConfigurationSection` would make
  every such read miss. This is the public plugin contract: a plugin still reads its settings through `IConfiguration`
  even though the manifest now declares them typed (see the manifest v2 note below), and this is the one type below the composition root allowed to hold
  `IConfiguration` (allow-listed in `ConfigurationTests`). Nothing else may follow it.
- **Manifest v2 (SDK 1.4.0): `settings` supersede `fields`.** A `plugin.json` may declare typed `settings`
  (text, url, select, secret, number, boolean, textarea); the legacy `fields` list is still read. The host
  reads a manifest's settings in one place only, `PluginManifest.GetSettings()` (the `settings` when it lists
  any, otherwise each legacy field converted): the activator's required check, the integration detail and
  save (`IntegrationFields`), the loader's integration entry and the field mapping
  (`PluginSettingMappingExtensions`) all go through it, and no host code reads `manifest.Fields` directly.
  The one exception is secret classification, `Application/Admin/Common/SettingSecrecy`, the only question
  every integration read and write asks (the general settings endpoints never meet a secret: they are limited to
  `GeneralSettingKeys`, the portal, billing and plugin keys, and refuse any other row with `SETTING_NOT_EDITABLE`): it takes the union of `GetSettings()` and the legacy fields, so a
  secret declared in either list is secret even in a manifest that mixes the two and omits it from `settings`.
  A secret is never returned in clear (`isSecret` and `hasValue` only) and is encrypted on save. A `settings`
  entry with a `type` the host does not define, and a legacy `fields` entry with a `type` the SDK does not
  recognise (the SDK would read it as plain text), are refused when the manifest is read
  (`PluginManifestReader.Deserialize`, the plugin does not load), and anywhere an undefined settings kind still
  reaches the classifier it counts as secret: an unknown type never reads as plain text. A `null` `settings`
  or `fields` list reads as empty, as in the SDK. The wire
  shape of a field definition (`FieldDefinitionDto`) keeps its members; a select carries the values of its options, and a
  plugin setting adds its translated `description`, `placeholder` and `optionLabels` (#854).
- **Two plugin folders: built-in and uploaded (`PluginOptions`, section `Plugins`).** The build's own
  `plugins/` folder (`PluginOptions.BuiltInRoot`) holds the plugins shipped with the build; they load
  first, are listed with `isBuiltIn: true`, and cannot be removed or replaced from the admin panel
  (`PLUGIN_BUILT_IN`, 409). Uploads go to `Plugins:InstallPath` (default `plugins-installed` beside
  the app; mount it on a volume to keep uploads across redeploys), which `PluginOptionsValidator`
  keeps apart from the built-in folder. All plugin file system work sits behind `IPluginPackageStore`
  (`Infrastructure/Plugins/PluginPackageStore`); Application never touches the disk. An upload is
  streamed to a staging folder (`.staging`, which fails the plugin id rule and so is never loaded),
  checked, unpacked there and moved into place with one rename, so a refusal leaves nothing behind.
  The checks: the plugin id rule (`PluginIdentifier` of the plugin packaging package: lower-case letters, digits and
  single hyphens, at most 64 characters) and a resolved path that stays a direct child of the upload
  folder; every entry inside the plugin's own folder; `Plugins:MaxEntryCount` and
  `Plugins:MaxUncompressedBytes` (counted while unpacking; `PLUGIN_ARCHIVE_TOO_LARGE`, 413); an entry that cannot be written into the unpack folder (a file and a folder of one name; the package's `PluginArchiveRule.EntryNotWritable`, mapped by `PluginRuleMap` to `PluginPackageOutcome.ArchiveUnwritable`) is `PLUGIN_ARCHIVE_UNWRITABLE`, 400; no
  assembly named like the SDK, `Innovayse.Hostpanel.*`, `System.*`, `Microsoft.*` or anything in the
  host's trusted platform assembly list (`PLUGIN_HOST_ASSEMBLY_REFUSED`); and, when
  `Plugins:TrustedPackageSha256` lists any digest, the archive's SHA-256 must be one of them
  (`PLUGIN_NOT_TRUSTED`; an empty list, the default, accepts any archive that passes the other
  checks). Every install and removal writes an activity log line with no client (`ActivityLog.ClientId`
  is nullable for system-wide actions), carrying the plugin id and, for an install, the digest. A removal also switches the plugin off and drops any staged upgrade; it keeps the plugin's stored data until an operator purges it (`PluginDataPurgeRequestedEvent`, see "Plugin upgrade, refused plugins and data purge" below and the plugin store under "Plugin routes").
- **One manifest validator for load and install (`Infrastructure/Plugins/PluginManifestValidator`).** A plugin
  is checked against the manifest rules in one place, asked by `PluginManifestReader.TryRead` (startup load,
  `plugins/` and the upload folder) and by `PluginPackageStore` (install); no rule is written twice. `Validate`
  checks the manifest, `ValidateSdkReference` the assemblies beside it. Each rule is a `PluginManifestRule`
  (Application `Admin/Plugins/Common/Enums`), which is also its error code (`PluginPackageErrors.CodeFor`,
  `ErrorMessages` en/ru/hy, default 400): `manifestVersion` must be 1 or 2 (`PLUGIN_MANIFEST_VERSION_UNKNOWN`);
  every `hostAccess` entry is a kind of `PluginHostAccess.All` (today `ssh`, exact case), none twice, and only in a
  version 2 manifest (`PLUGIN_HOST_ACCESS_UNKNOWN`);
  every `capabilities` entry is one the host defines and the plugin type may carry (`PLUGIN_CAPABILITY_NOT_ALLOWED`:
  an unknown or wrong-case name, a name declared twice, or `storedCredentials` on anything but a payment plugin); a plugin that is not a
  serving plugin declares no `api` route and no `ui` menu, page or slot (`PLUGIN_ENDPOINTS_NOT_DECLARED`, because the
  host would accept them and never serve them);
  a client route, page or slot `permission` is `PluginPermissions.Any` or a defined `ClientPermission` name,
  one name, exact case, not `None` (`PLUGIN_PERMISSION_UNKNOWN`; `ClientPermission.All`, the combined flag, is a
  defined name and is accepted); an admin route, page or slot (a slot whose
  `PluginSlot.Area` is admin) takes none (`PLUGIN_ADMIN_PERMISSION_DECLARED`); a route `path` and a page `path`
  (both sit below the plugin's own prefix, so one rule) start with `/`
  and every segment is a lowercase literal (`a-z0-9_-`) or a `{parameter}`, so no `..`, no empty segment, not
  `/` alone (`PLUGIN_ROUTE_PATH_INVALID`); the segment patterns are anchored with `\A` and `\z`, never `$`, which
  would let a trailing newline through; a method and path template is unique within an area, parameter names
  ignored (`PLUGIN_ROUTE_DUPLICATE`; page paths are not checked for duplicates); a `slot` is in `PluginSlots` (`PLUGIN_SLOT_UNKNOWN`) and each `contextKeys`
  entry satisfies `PluginSlot.Offers` (`PLUGIN_SLOT_CONTEXT_KEY_NOT_OFFERED`); a menu and setting `label`, and a
  page and slot `title`, shaped like a text key (dot separated words, no spaces, such as `menu.reports`) must
  exist in the `en` locale, any other value is plain text; a setting `description` and `placeholder` is usually
  help text or an example (`api.example.org`, `v1.0`) that has the shape of a key, so it counts as a key only
  when some locale defines that exact value, and must then be defined in `en` too
  (`PLUGIN_LABEL_KEY_MISSING`); an assembly in the plugin folder whose metadata references the SDK at a
  lower major version than the host's is refused with a rebuild message (`PLUGIN_SDK_ASSEMBLY_OUTDATED`; only an
  older major is refused, a newer minor or a newer major is not checked here), and one the host cannot read
  (`IOException`, `UnauthorizedAccessException`) is refused with `PLUGIN_ASSEMBLY_UNREADABLE` instead of a server
  error,
  read with `System.Reflection.Metadata` so the plugin is never loaded into the default context. A version 1
  manifest has none of these blocks and passes. At load a refused manifest skips only that plugin and logs
  one warning with the plugin id and the rule; at install the response is the rule's localised error and
  nothing is left behind. The manifest-level `sdkVersion` check (`PLUGIN_SDK_VERSION_UNSUPPORTED`) is separate
  and unchanged. **View documents** (the `view` file of every page and slot) are read and checked by
  `PluginViewDocumentReader` (`Infrastructure/Plugins`), at load after the signature check and before any assembly
  loads, and at install on the unpacked files, with the same `PluginManifestRule`s: `ViewMissing`
  (`PLUGIN_VIEW_MISSING`, no such file), `ViewInvalid` (`PLUGIN_VIEW_INVALID`: the path is not package-relative
  `.json`, the file is over the size cap, it is not JSON, or it breaks the structure or link rule below),
  `ViewSchemaUnsupported` (`PLUGIN_VIEW_SCHEMA_UNSUPPORTED`, `schema` is not `PluginViews.SchemaVersion`),
  `ViewBlockKindUnknown` (`PLUGIN_VIEW_BLOCK_KIND_UNKNOWN`, an object with `kind` outside `PluginViews.BlockKinds`),
  `ViewRouteUndeclared` (`PLUGIN_VIEW_ROUTE_UNDECLARED`, a `PluginViewRoute` that no route of the page's or slot's area
  declares), and `LabelKeyMissing` for a key-shaped label under `PluginViews.LabelProperties` that `en` lacks
  (the same `PluginManifestValidator` check as manifest labels).
  - **View state (#852).** Three optional members let a view react to its own data without a reload; names and caps
    are `PluginViews` constants (`VisibleWhenProperty`, `ResetOnSuccessProperty`, `FollowUpProperty`,
    `MaxConditionValues` 20, `MaxFollowUpRefresh` 20). One **condition** shape serves `visibleWhen` and
    `followUp.until`: exactly `{ "route": { "method": "get", "path": … }, "field": "a.b", "equals": … }`. `route` is a
    `GET` route declared in the page's or slot's area (undeclared is `PLUGIN_VIEW_ROUTE_UNDECLARED`, another method
    `PLUGIN_VIEW_CONDITION_INVALID`); `field` is a dotted path (`[A-Za-z0-9_-]` segments, at most 200 characters) read
    like `readPluginField`; `equals` is one JSON scalar or an array of 1 to 20 scalars, and the condition holds when the
    field strictly equals one of them (no number/string coercion). Condition strings are values, never walked as labels.
    - `visibleWhen` (any block, header actions and tab blocks included): the panel hides the block while the condition
      is read, on a failed read, an absent field or no match, and shows it only when it holds; it is read again when the
      block's own `id` is refreshed. Use it to hide a create form once the thing exists.
    - **Chat result card actions.** An action on a chat result card takes the same condition as `visibleWhen`, spelled as the
      event spells it (`equalsAny`), and is held to the same bounds; the card is not checked at install, so the panel
      checks it when it reads the event. See "Reload" under the preview rules below for how it is read.
    - `resetOnSuccess: true` (a form's `submit` only, boolean): after a successful submit every field returns to its
      empty value.
    - `followUp: { "refresh": [ids], "until": <condition> }` (an action block or a form's `submit`): after a successful
      call and its normal `refresh`, the panel reads `until.route` on an interval, refreshes the listed block ids on
      each attempt, and stops once the condition holds (after one last refresh), after the last attempt, or when the
      page is left (late answers dropped); the watch belongs to the view (`PluginViewContext.adopt`), so the block that
      started it hiding itself, as a publish button does once its version is live, does not end it, and when the condition
      holds the sentence the action showed is taken down. A failed read counts as an attempt. `refresh` is 1 to 20 ids, each the `id`
      of a block in the same document. Two blocks with one `id`, at any depth, are refused **at install only**
      (`ViewBlockIdRepeated`, `PLUGIN_VIEW_BLOCK_ID_REPEATED`), because the client keys a block's refresh and its watch on the id;
      the `id` of a tab or of a preview source is not a block id. The reader also checks the display members the panel would otherwise ignore when mistyped (`PLUGIN_VIEW_INVALID`): a header's `compact` (boolean), `titleSource` and `subtitleSource`, an action's `labelSource` and a preview's `note` (each a GET route and, when given, a dotted `field`), and a table's `display` (`table` or `cards`); also at install only.
      **Decision (owner, #976): a rule added after plugins were installed refuses at install and only warns at load.** A host
      upgrade must not take a working plugin, one that may take payments, out of production because the host learned to
      check one more thing. The reader keeps the two apart in its result, not with a flag: `PluginViewRead.Violation` holds
      the rules enforced from the start (refused at install and at load), `PluginViewRead.Advisories` holds the faults of
      rules added later (the repeated id and the display members), and `PluginViewRead.RefusedAtInstall` is the violation, else
      the first advisory. `PluginPackageStore` asks `RefusedAtInstall`; `PluginLoader` asks `Violation` and logs each advisory
      at Warning with the plugin id and the rule. A later tightening goes to `DocumentScan.Advisories`, never to `Malformed`
      or `StateViolation`, which refuse at load. The panel stays safe with a repeated id: every block renders (a block's key
      carries its place), and the limit is that a refresh of that id reaches all its blocks and one follow-up watch per id
      is kept (`View.test.ts`, `PluginLoaderTests`, `InstallPluginTests`). **The bounds are the host's, never the plugin's**:
      `PLUGIN_FOLLOW_UP_MAX_ATTEMPTS` 20 and `PLUGIN_FOLLOW_UP_INTERVAL_MS` 3000 (about a minute) in the client.
    - **Reads are shared inside a view (`readPluginRoute` in `usePluginView`).** A page is one picture of the plugin's
      data: every source, condition and card action of a view that names the same `GET` request (the filled address and
      its query, from `pluginRouteRequest`) takes one answer, and a new request is sent only when the picture is out of
      date, which is when the sum of the view's refresh counts moved, the store's `writes` did (a call that is not a read
      when it ended, a stream when it opened and again when it ended, however it ended) or the picture is older than
      `PLUGIN_VIEW_READ_MAX_AGE_MS` (`constants/pluginViewReads.ts`, 10 seconds, the host's and never the plugin's). A
      stream counts when it opens because a result card is drawn while it is still open: its condition must be read after
      the turn began, or a chat with no `refresh` list would keep a card's action hidden for good on the picture from
      before it. A block that mounts later is answered from the picture as well, and a read that
      failed is not kept, so the next reader tries again. The picture belongs to the view's context and ends with it.
      The follow-up hands the answer it read to the picture it then refreshes (`shareReadResult`), so a poll costs one
      request and the blocks show exactly what the condition saw. This replaced one request per reader per refresh,
      which on a route the plugin answers one call at a time made the last readers' answers arrive after the next
      refresh began, so `latest` dropped them and a header stayed on the old live version for several polls while a block
      answered earlier already showed the new one (#986).
    - Refused at install and load (rules from the start of view state): `ViewConditionInvalid` (`PLUGIN_VIEW_CONDITION_INVALID`: a bad condition, a non-`GET`
      condition route, `visibleWhen` outside a block) and `ViewFollowUpInvalid` (`PLUGIN_VIEW_FOLLOW_UP_INVALID`: a bad
      `followUp` shape or place, a `refresh` id no block carries, `resetOnSuccess` not a boolean or outside a `submit`),
      each with its en/ru/hy sentence, 400.
  - **Action body from page data (#940, no schema change).** `bodySource` on an action block of the client area:
    exactly a `GET` `route` and 1 to 10 `fields` (a camelCase body member, not a `PluginViews.LabelProperties` name, and a
    dotted path). The action has an `id`, calls `POST`, `PUT` or `PATCH`, and a `labelSource` reads the same route, because
    the panel takes label and body from one read so a button cannot send a value other than its label's. Anything else is
    `PLUGIN_VIEW_ACTION_BODY_INVALID` (`ViewActionBodyInvalid`, `CheckActionBody` in `PluginViewDocumentReader`; the member names are the reader's own constants until the SDK's `PluginViews` documents them). In the
    client (`ActionBlock.vue`, `pluginActionBody`) the button is disabled until every member is read as a scalar, the body
    is fixed at the click, and a refused call reads the data and `refresh` again. Absent, an action posts `{}`. A result
    card's action carries a literal `body` instead (`readResultBody`), and a malformed one drops that action.
  - **Layout, split and preview (#910, SDK 3.2.0, schema stays 1).** A client page's `layout` is a `PluginPageLayouts`
    value and the page answers it (`PluginPageViewDto.Layout`, `standard` when absent); another value or any layout on an
    admin page is `PLUGIN_PAGE_LAYOUT_UNKNOWN` (`CheckPageLayouts` in the manifest validator). `split` holds exactly two
    panes of `{ blocks }` and never nests in a split (`PLUGIN_VIEW_SPLIT_INVALID`). `preview` holds 1 to 5 `sources`
    (exactly `id`, `label`, a GET `route`, a dotted `field`) and an optional `size`, and is allowed **only on a client
    page** (`PLUGIN_VIEW_PREVIEW_INVALID`), because no other document gets the narrowed frame policy.
    - **Origins.** `PluginPageViewDto.FrameOrigins`, computed per request by `PluginFrameOriginResolver` from the one
      page reader, empty in the admin panel, never holding the panel's own origin (`ClientBaseUrl`): the manifest's `externalOrigins` that are pure https origins, the domains (as typed at order) of
      the caller's account's `Active` services on products the operator bound to this plugin (#823 rows through
      `IPluginSlotContextReader`, the rule SSH access follows: no manifest fallback, nothing bound means none;
      `IPluginHostFactsReader.ListActiveServiceDomainsAsync`; the account comes from `ICallerClientResolver`, and only with
      `ViewProductsServices`, as in the host API) as `https://d` and
      `https://www.d`, then `PluginPreview:DevelopmentHttpOrigins`. `PluginFrameOrigin` is the one parser, and everything
      that is not a plain DNS name is dropped, because a domain is what a client typed and the text reaches a response
      header. The list is cut at 2048 characters for the proxies' header limits; what falls off shows nothing.
    - **Link origins.** `externalOrigins` of a page (`PluginPageViewDto`) and of a slot widget view is `PluginFrameOriginResolver.LinkOrigins`: the manifest's entries as written (wildcards allowed) followed by the `https` origins of the same list, so a plugin links to the live site of the reader's own bound service without naming its domain. The slot widget handler gets the resolver from DI; the admin panel has no frame origins, so there only the manifest applies.
    - **Browser enforcement.** The panel frames an address only when its origin is exactly on that list and is not the
      panel's own. The edges this repository ships (`docker/caddy/Caddyfile`, `docker/nginx/templates/default.conf.template`, held to one policy by `scripts/check-edge-csp.sh`, which compares the text, and run as they are in front of a stub upstream by `scripts/check-edge-headers.sh` with `edge-headers-assert.sh`, which asserts the response headers: one policy on `/`, the edge policy plus the plugin page's narrow `frame-src` as two headers on `/client/apps/*` (also under a language prefix), the upload policy on `/uploads/`, the admin upstream's policy passed through alone, and every location of the admin image's own configuration (`docker/nginx/admin.conf` with `snippets/admin-csp.conf`) sending exactly one; the plugin page's narrow policy there comes from the stub upstream, not from the portal middleware, so the script proves that the edges pass it and not that the portal sends it; the same script runs in the GitHub `edge-csp` job and the GitLab `edge:test:headers` job) keep `frame-src 'self' https:` (operator content on public pages, and domains cannot be
      listed statically). An operator's own edge may send that same policy as `Content-Security-Policy-Report-Only` while it is reviewed (#938), and a report-only policy does not intersect with an enforcing one, so on such a deployment, and on a deployment whose edge sends no policy, the header below is the only one enforced. `client/server/middleware/plugin-frame-csp.ts` adds a second header to the document of
      `/client/apps/<plugin>/**` (a language prefix names the same page), `Content-Security-Policy: frame-src <origins>` or
      `'none'` on any failure. Policies intersect. A document that got the API's origins also carries `Server-Timing:
      plugin-frames`, the one response header script can read (secure contexts only, which localhost and https are). A CSP belongs to
      the document, so the renderer loads the page once in full when it arrived by a router move from another document, or
      when its document lacks that entry, which is the document a visitor opened signed out and then signed in inside.
    - **Reload.** A refresh of the block's `id` draws a new frame once its read answered, because a site's address does
      not change with its content; nothing about the sandbox changes for it. With a `note` only a changed note draws one, because a follow-up reads the block every few seconds. A chat's `refresh` also applies after a result
      card's action, whose `until` (`route`, `field`, `equalsAny`) keeps those blocks reading while work it started runs on.
      A card action (a call, a link or a resend) may also carry `visibleWhen`, the view condition in the event's spelling
      (`route`, `field`, `equalsAny`); both conditions go through one reader, `readResultCondition` in `utils/plugins.ts`,
      which holds an event to the view's own bounds (`constants/pluginCondition.ts`: a GET route the page can fill, a dotted
      field of at most 200 characters, 1 to 20 scalars). A card is not checked at install, so a `visibleWhen` the reader
      cannot accept drops the action whole, where a malformed `until` only loses its follow-up. A condition whose route names a
      parameter the page lacks is dropped the same way, with a `console.warn` in a development build only
      (`warnUnfilledConditionRoute`). `PluginChatCardAction` draws
      each action and evaluates the condition with `usePluginCondition`, which takes the block ids whose refresh reads it
      again: a block names its own id, a card action the chat's `refresh` list. With no `refresh` list the condition is read
      once, when the card is drawn. `PluginChatCardActions` is the row under a card: it hides itself while every action is
      hidden and the card has no details switch, and never removes it, because a hidden action stays mounted to read its
      condition again and bring the row back.
    - **Presentation members, no schema change.** The view schema allows extra members on a block, and these are the
      panel's: a header's `compact`, `titleSource`, `subtitleSource` and any block kind in its `actions`; an action's
      `labelSource`; a table's `display: "cards"`; a tabs block's `actions`; a preview's `note` (an object of sentences by
      source id). A chat `result` event may carry `details` (lines behind a switch), and an action `{ label, resend: true }`.
    - **Sandbox decision.** `allow-scripts allow-forms`, **never `allow-same-origin`**: a client chooses a service's
      domain, so it could name the panel's own host, and that pair is an escape from the sandbox on a shared origin. The
      opaque origin also means a framed site's session can never be driven from the panel. Accepted cost: a page that
      needs its own cookies or storage degrades in the preview.
    - **http only in Development.** `PluginPreviewOptionsValidator` refuses to start with `DevelopmentHttpOrigins` set
      outside Development (a blank entry counts as unset, which is what `PLUGIN_PREVIEW_DEV_HTTP_ORIGIN`, a dev compose key only,
      passes when empty), and with anything but an http origin on a loopback host (a browser blocks other http frames in
      an https panel as mixed content). This is a refusal on purpose, not a branch in the flow.
    - **Element selection (#933, no schema change; the members are the reader's own constants until the SDK's `PluginViews` documents them).** A preview's
      `selection` (exactly `sources`, a POST `route`, and a `release` of a GET `route` and a dotted `field`) and a chat's `selection: true` are off when absent; anything else, or a chat selection with no preview selection in the document, is `PLUGIN_VIEW_SELECTION_INVALID` (`ViewSelectionInvalid`). The sandbox does not change. A frame's message is a claim: `PreviewBlock.vue` takes it only from its frame's
      `contentWindow` (origin is `null` and proves nothing), `parsePluginSelectionMessage` admits only the exact versioned shape, the release the plugin reports, and an id of `PLUGIN_SELECTION_ID_PATTERN`; a toggle needs the mode on and a source listed in `sources`, so a live source can do nothing. One preview per document may declare a selection, because a claim names a release and ids and not a preview (a second is `ViewSelectionInvalid`). Each selection is its preview's own (`usePluginSelection`, opened only by a preview that declares one; any other neither listens, posts nor clears). A `ready` naming a release not read yet is held, one at a time, until the read answers. Chips hold only what the plugin's route answered, lookups run one at a time for the newest wish, and a new release empties them. A resend after that is refused, not sent without its claim, since a plugin reads a message with no selection as free to change anything: `ChatBlock.vue` says the page changed and keeps the text in the field. Host-to-frame messages go to target `*` because an opaque origin cannot be named, so they carry only the mode and ids. The plugin re-validates on its server.
  - **Chat events (#911, SDK 3.2.0).** `PluginChatEvents`: `step`, `result`, `issue`, sent as ordinary plugin events and
    rendered by the client's chat block; `error` and `done` stay reserved (`PluginEventStream`). The host forwards their data
    unread: the client renders text only and takes a result's actions through the same link rule and route door as
    other blocks. Stop aborts the fetch, which cancels the plugin through `RequestAborted`; no event and no route of its own.
  - **Structure.** The root is an object with an integer `schema` and a `blocks` array. Every entry of a `blocks`
    array (the root's or a tab's) and of a block's `actions` array is an object with a string `kind`; a block's `tabs`
    is an array of objects whose `blocks`, when present, follow the same rule. A block the panel would skip or crash on
    is refused at install, not rendered blank. The parser's `MaxDepth` is 32 (root counted), and a member name that
    appears twice in one object is refused (the localiser would throw on it when the page is served).
  - **Links.** The URL-like members `href`, `url`, `src`, `image` and `to` (names compared ignoring case, at any depth)
    are each either host-relative (one leading `/`, never `//`) or an absolute `https` URL with a host and no user info;
    neither may hold a backslash or a control character, and a host-relative link may not reach the host API (`/api` or
    under `/api/`, any case, checked on the raw text and once more after one percent-decoding). Anything else
    (`javascript:`, `data:`, `http:`, protocol-relative) is `PLUGIN_VIEW_INVALID`. `icon` must be a plain icon name
    (`\A[a-z0-9-]{1,64}\z`), never a URL, and `target` and `formAction` are refused outright. The contract for both
    panels: every string of a view is rendered as text only, never through `v-html`, a link is built only from these
    checked members, and an absolute link opens with `rel="noopener noreferrer"`.
  - **No links on the way.** A folder between the plugin folder and the view file, or the file itself, that is a
    symlink or junction is `PLUGIN_VIEW_INVALID`; the check is on the path, accepted for the reason the signature check
    gives (only the host writes the plugin folder). A file that is not seekable (FIFO, device) is refused too.
  - **Size cap (`Plugins:MaxViewBytes`, `PluginOptions`, bytes, default 262144, 1 to 4194304, validated on start by
    `PluginOptionsValidator`).** The length is read from the same open handle that is then read, before anything is
    read, and the read itself is bounded (never more than the cap plus one byte), because the file may grow between the
    two; over the cap is `PLUGIN_VIEW_INVALID`, at load and at install alike.
  - **Violation details** carry only the package-relative path and, for a parse or I/O fault, the exception type name;
    never an absolute host path, an exception message or document text, because the detail reaches the install response.
  - **Kept in memory.** The parsed documents live in `LoadedPlugin.Views` (`PluginViewDocuments`), so serving a page
    reads no file. A localised copy is cached per path and language (`PluginViewDocuments.Localised`, key
    `LocalisedViewKey`), always: the language is first normalised by `PluginUiTexts` to a supported one (`en`, `ru`,
    `hy`, anything else becomes `en`), so the cache holds at most documents times three and a caller cannot grow it.
- **Plugin rules added with SDK 3.0.0.** Short notes; the code and its tests are the detail.
  - **`endpoints` capability on a non-App plugin serves callbacks only.** Only the public callback routes
    (`api.callbacks`) are opened; client and admin routes, pages and slots stay App-only for now. A plugin that declares the
    capability but whose class does not implement `IPluginEndpoints` is refused (`PluginManifestRule.EndpointsNotImplemented`).
  - **Plugin-wide `IPluginStore` by constructor.** A plugin may take `IPluginStore` in its constructor for data it needs
    outside a routed call. It is scoped to the plugin id and no client, and each operation runs in its own unit of work.
  - **`RefundInstruction.AlreadyRefundedMinor`.** What the host already recorded as refunded through the gateway order before
    this `RefundId`. A gateway with no idempotency key compares its refunded total with it to tell a retry from a new refund.
  - **Plugin assembly namespace.** `Innovayse.Hostpanel.Plugins.<Name>` is the one carve-out from the reserved
    `Innovayse.Hostpanel.` prefix, and a file name already shipped by another plugin is refused with `PLUGIN_ASSEMBLY_CONFLICT`
    (see "Plugin assembly names" above).
  - **Callback door.** Only an allow-list of request headers (plus non-refused `X-*` ones) reaches a plugin on a callback, never
    the caller's credentials. A raw reply is accepted on a callback only, with a media type from an allow-list (markup and
    script refused) and a size cap, and is sent with `nosniff` and `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`.
- **Plugin icon (#845).** The manifest's optional `icon` is a package-relative `.svg` or `.png` path. `PluginIconReader`
  (`Infrastructure/Plugins`, an adapter over the packaging package's `PluginIconChecker`) checks it at load (`PluginLoader`) and at install (`PluginPackageStore`, on the unpacked
  files): plain lowercase path segments with no `..`, no link on the way, at most 256 KiB read from one bounded handle,
  a PNG signature plus `IHDR`, an SVG parsed as XML with DTDs prohibited and no resolver, refused when it holds
  `script`, `foreignObject`, other embedding elements, an `on*` attribute, a non-fragment `href`, a non-fragment
  `url(...)`, `@import`, a processing instruction, or an `animate`/`set` aimed at `href` or an event handler. A missing
  file is `PLUGIN_ICON_MISSING` (`PluginManifestRule.IconMissing`), anything else `PLUGIN_ICON_INVALID`
  (`IconInvalid`); details carry the package-relative path only. The checked bytes and a strong tag live in
  `LoadedPlugin.Icon` (`PluginIconDto`); `IPluginRegistry.GetIcon` serves them with no file read.
  `GET /api/admin/plugins/{id}/icon` (`GetPluginIconQuery`, admin only) answers through
  `BaseApiController.InlineImage`: declared type, `X-Content-Type-Options: nosniff`, `Content-Security-Policy:
  default-src 'none'; style-src 'unsafe-inline'`, `Cache-Control: private, max-age=300, must-revalidate`, `ETag` (304 on
  a match); an unknown plugin and a plugin without an icon both answer `PLUGIN_ICON_NOT_FOUND` (404).
  `PluginListItemDto`, `IntegrationListItemDto` and `IntegrationDetailDto` carry `iconUrl` (the endpoint path, `null`
  without an icon), the integration DTOs also `isPlugin`.
- **Menu icons and page ids (#643).** A menu `icon` is a plain name, `\A[a-z0-9-]{1,64}\z`
  (`PLUGIN_MENU_ICON_INVALID`, `PluginManifestRule.MenuIconInvalid`); the panels map a name to a built-in icon only and
  never load it as a URL. A page id is a `PluginPageIdentifier` (plugin packaging package: `\A[a-z0-9][a-z0-9_-]*\z`, at most 64
  characters, not the reserved `by-path`) and unique within its area (`PLUGIN_PAGE_ID_INVALID`,
  `PluginManifestRule.PageIdInvalid`), so every page the installer accepts is reachable by the page lookup.
- **Plugin upgrade, refused plugins and data purge (#649, #785).** An upload of a plugin id that is already installed
  with a different version is an upgrade: it passes the same checks as a fresh install (signature, manifest rules, size
  limits), is unpacked into `.staged/{id}` of the upload folder (a name that fails the plugin id rule) and answers
  `requiresRestart: true` with `stagedVersion`; the installed files are not touched. `PluginUpgradeApplier.Apply` swaps the
  staged folder in at the next startup, before `PluginLoader` scans, and writes `.upgrades/{id}.json` (from and to version)
  **before** the swap, atomically (temp file plus rename); an unreadable record is logged with its file name and rebuilt
  from the name; a still pending record keeps its first from version; startup sweeps `.staging` and moves a version a crash
  left set aside back. `PluginMigrationRunner` (`IHostedLifecycleService.StartingAsync`, before the web server accepts a
  request, and `IPluginMigrationRetry.RetryAsync` from `SaveIntegrationConfigHandler` when a plugin is saved enabled) then
  calls the SDK `IPluginMigrations.MigrateAsync` of each upgraded, enabled plugin, writes the activity line `Migrated
  Plugin Data` and only then deletes the record. The hook has a time limit (`Plugins:MigrationTimeoutSeconds`, validated;
  the token is cancelled and the host stops waiting). While a record exists, or after a failed or timed out attempt,
  `PluginRegistry.IsMigrationPending` is set and `PluginActivator` returns `PluginActivationOutcome.MigrationPending`, so
  the plugin is neither called nor started (the migration alone activates it through `ActivateForMigrationAsync`). A
  throwing hook, a switched-off plugin or a plugin that did not load keeps the record and the gate; it is tried again at
  the next start and on enable or settings save. The hook takes no versions, so they only reach the log and the activity
  line. A plugin that is loaded is never overwritten in place: any upload for it, equal version included, is staged; a not
  loaded plugin of the same version is replaced at once. An older version, one older than the already staged upload, or
  one compared with an unparseable installed version is refused with `PLUGIN_DOWNGRADE_REFUSED` (409) unless the request
  carries `?force=true`; staging, a forced downgrade and a refused downgrade are audited. Versions are strict semantic
  versions (`PluginVersion` of the plugin packaging package, `PluginManifestRule.VersionInvalid`, `PLUGIN_MANIFEST_VERSION_INVALID`).
  Settings and the plugin store are never touched by an upgrade. A refused plugin claims its id (a same-named upload never
  loads or overwrites its refusal). The data purge looks at the package folder (`IPluginPackageStore.HasPackageFolder`) and
  counts slot widget bindings as kept data. `PluginLoader` keeps every plugin it refused for a broken manifest rule
  (`RefusedPlugin`: folder name, `PluginManifestRule`, detail; built-in and upload folder alike) in `PluginRegistry`
  (`IPluginRegistry.GetRefusedPlugins`); `GET /api/admin/plugins` lists it with `status: "Refused"`, `refusedRule` and
  `refusedDetail`, never as loaded, and it can be removed like any upload. A removed plugin that still keeps data is listed
  with `status: "Removed"` so the operator can purge it. Needs no database migration.
- **Plugin package checks come from `Hostpanel.Sdk.Packaging` (SDK 3.1.0).** The checks a plugin package must pass are written
  once, in the SDK repository's second package (MIT, versioned in step with `Hostpanel.Sdk`, project reference at
  `backend/src/Hostpanel.Sdk/Hostpanel.Sdk.Packaging`): the signature format, signing and verification (`PluginSignatureFile`,
  `PluginSignaturePayload`, `PluginSigner`, `PluginSignatureValidator`, `TrustedKeyId`), `PluginIdentifier`,
  `PluginPageIdentifier`, `PluginVersion`, `PluginAssemblyNames`, `PluginArchiveGuard`, `PluginIconChecker` and the structural
  manifest rules (`PluginManifestChecker`). A plugin author runs the same code the host installs with. The host keeps its
  policy and nothing else: `PluginSignatureVerifier` (built-in plugins exempt, Development and Testing relaxed, the admin
  `plugins.allow_unsigned` setting, the trusted key list), `PluginAssemblyConflicts`, built-in ids, the view document rules
  (`PluginViewDocumentReader`), the entry point and endpoints checks and every `PLUGIN_*` code. The package reports its own rule
  enums; `PluginRuleMap` (`Infrastructure/Plugins`) maps them to the host's `PluginManifestRule` and `PluginPackageOutcome` by
  name, in one place and never by a cast, and `PluginPackageErrors` maps a package `PluginSignatureRule` to its code.
  `PluginManifestValidator` and `PluginIconReader` are thin adapters that call the package and map the answer. The domain
  does not reference the packaging package (`KernelPurityTests` allows the kernel only): the plugin id rule is checked by the
  validators of the use cases that take a plugin id, and the domain refuses only an empty id. `PluginPackagingRulesTests` fails when a host layer declares a type the package owns. The signing tool
  (`backend/tools/Innovayse.Hostpanel.PluginSigner`) references the package, not the host.
- **Signed plugin packages (`Infrastructure/Plugins/PluginSignatureVerifier`, section `PluginSigning`).** An uploaded
  package carries a detached Ed25519 signature, `plugin.sig` at its root, checked in one place by both paths, like the
  manifest validator: at install (`PluginPackageStore`, on the unpacked files, before the move into place) and at load
  (`PluginLoader`, **before any assembly of the plugin is loaded**, because loading can already run its code). A refusal
  is a `PluginSignatureRule` (Application `Admin/Plugins/Common/Enums`, a sibling of `PluginManifestRule`), which is also
  its error code (`PluginPackageErrors.CodeFor`, en/ru/hy, 400): `Unsigned` (`PLUGIN_UNSIGNED`, no `plugin.sig`),
  `Malformed` (`PLUGIN_SIGNATURE_MALFORMED`, the file is not a signature, is over 4 KB, or a file name holds a control
  character), `UnknownKey` (`PLUGIN_SIGNATURE_KEY_UNKNOWN`, the key id is not trusted), `Invalid`
  (`PLUGIN_SIGNATURE_INVALID`, a file was changed, added or removed after signing). At install the response is the rule's
  localised error and nothing is left behind (`PluginPackageOutcome.SignatureRefused`); at load only that plugin is
  skipped and one warning carries its id and the rule.
  - **The format** (`PluginSignatureFile`, `PluginSignaturePayload` of the plugin packaging package; the signing tool, a plugin author's pipeline and the host all use this one code, and `PluginSignatureGoldenTests` pins that the host works on the payload the package builds).
    `plugin.sig` is `{"keyId":"...","signature":"<base64 of the 64-byte Ed25519 signature>"}`. The signature covers
    this UTF-8 text (no byte order mark, every line ends in `\n`): the line `innovayse-plugin-signature-v1`, the
    line `key <keyId>`, then one line per file, `<lower-case hex SHA-256> <path>`, where `<path>` is relative to the
    package root with forward slashes, sorted by path with ordinal comparison. Every file under the root is listed
    (sub-folders too) except `plugin.sig` at the root; folders are not listed. The key id is inside the payload, so a
    signature cannot be presented under another id. A key id is `[a-z0-9][a-z0-9._-]{0,63}` (`TrustedKeyId`). A path
    holding a control character or a lone surrogate (no UTF-8 form; the payload is encoded strictly), and any file or
    folder that is a link (symlink or junction, never followed), cannot be signed and is `Malformed`. The check is on the
    path, not on the opened handle, so a path swapped for a link between the check and the open would be followed; this is
    accepted because the package root is created by the host (a staging folder it extracts the archive into, or an install
    folder only its own process writes, and a zip entry is never written as a link), so no one else can write there. The
    signing tool runs on the publisher's own folder and has no such guarantee.
    `PluginSignaturePayload.Header` carries the version, so a change of format is a new header, not a silent clash.
  - **Trusted keys** are public keys only (`PluginSigningOptions`, sealed, `SectionName = "PluginSigning"`, validated
    at startup by `PluginSigningOptionsValidator`): `MaintainerKeys` ships in `appsettings.json` (the Innovayse key
    `innovayse-2026`; its private half is never in a repository or on a host, tests generate their own keys per run) and `OperatorKeys`
    is the operator's own (`PluginSigning__OperatorKeys__0__KeyId` and `__PublicKey`, the base64 of the 32-byte raw
    key). Two lists because configuration arrays merge by index: one list would let an operator's first entry replace
    the maintainers' first. The trusted set is both lists; a key id may appear once. A private key is never in the
    repository, in configuration or on the host.
  - **The policy** (`PluginSignatureVerifier.Check`). A plugin staged inside the image (`PluginOrigin.BuiltIn`, the
    build's `plugins/` folder, such as the payment provider) is part of the host and **is never signature-checked**: it
    is exactly as trustworthy as the image it shipped in, and the existing `Plugins:TrustedPackageSha256` pin stays
    what it was, an archive digest allow-list for uploads. An uploaded plugin (`PluginOrigin.Uploaded`) is checked
    wherever signatures are enforced, which is every environment except Development and Testing
    (`PluginSignatureVerifier.Create`, the same environment test `DeployedSecretsValidator` uses); in Development an
    unsigned package loads. Where enforced, an unsigned, tampered or unknown-key package is refused unless the admin
    setting `plugins.allow_unsigned` (`PluginSettingKeys`, a `plugins.*` row seeded `false` by `PortalSettingsSeeder`,
    validated by `UpdateSettingHandler`) is on; then it is admitted with a warning naming the plugin and the rule.
    Turning it on is audited: `UpdateSettingHandler` writes a system-wide activity log line `Changed Setting - Setting:
    plugins.allow_unsigned - From: false - To: true` in the same save (`PluginSettingKeys.IsAudited`).
  - **When the setting is read.** At install `InstallPluginHandler` reads the row for each upload, so a change applies to
    the next upload at once. At startup the loader needs it before the container exists, so
    `AllowUnsignedSettingReader` reads that one row with a plain Npgsql query in `AddInfrastructure`; a database that
    cannot be reached, a missing table (the first start), a missing row or any other failure reads as off, the safe
    answer; the warning logs the setting name and the exception type only, never the connection string or host. Plugins
    already installed therefore follow a change of the setting at the next restart.
  - **Known limitation: no downgrade protection.** The signature proves who published the files and that they are
    unchanged, not that they are the newest. An older signed version of a plugin can be installed again, and nothing in
    the signature stops it, so a version with a known flaw stays installable for as long as its key is trusted. Removing a
    trusted key is the only revocation today; a signed version floor or a revocation list is not built.
  - **Signing** (`backend/tools/Innovayse.Hostpanel.PluginSigner`, outside `src/` and the solution, so the image does not
    build it): `dotnet run --project backend/tools/Innovayse.Hostpanel.PluginSigner -- keygen <private-key-file>`
    creates a key (the file holds the base64 of the 32-byte private key) and prints the public key;
    `... -- sign <package-dir> <private-key-file> <key-id>` writes `plugin.sig`; `... -- public-key <private-key-file>`
    prints the public key again. The tool refuses a key file inside a git working tree. Sign the folder that will be
    zipped, after its last change. The future plugin repositories (#780) sign in their release pipeline with the same tool.
  - **Admin.** The plugins page shows the `plugins.allow_unsigned` switch (`PluginSigningCard`) with a visible warning,
    saved through the settings store like every other settings row.
- **Plugin registry (#866).** The admin panel can browse and install plugins from a remote catalogue the operator points it
  at. Off by default and no address is shipped: `PluginRegistryOptions` (sealed, section `PluginRegistry`, validated on
  start by `PluginRegistryOptionsValidator`) has `Enabled` (false), `BaseUrl` (absolute `https`, no user info, query or
  fragment; `PluginRegistry__BaseUrl`, compose `PLUGIN_REGISTRY_BASE_URL`, with `PLUGIN_REGISTRY_ENABLED`; the compose
  files give them no default), `Timeout` (one call, a download included; 1 s to 5 min, default 30 s) and `MaxDownloadBytes`
  (default 50 MiB, at most 100 MiB). While it is off every registry route answers 409 `PLUGIN_REGISTRY_DISABLED`.
  - **Where it sits.** Port `IPluginRegistryClient` (Application) and one adapter, `Infrastructure/Plugins/Registry/PluginRegistryClient`
    (typed `HttpClient`, `*Wire` records beside it, `internal`). `IPluginPackageInspector` reads what an archive claims
    (plugin id, version, signing key id) and answers `IsOperatorKey`. Use cases (`Admin/Plugins`): `ListRegistryPluginsQuery`
    (`GET /api/admin/plugins/registry?q=&category=&page=&pageSize=`), `CheckPluginUpdatesQuery`
    (`GET .../registry/updates`) and `InstallRegistryPluginCommand` (`POST .../registry/{id}/install`, body `{version}`,
    demo restricted). One type each for a plugin (`RegistryPluginDto`) and a page (`RegistryPluginPageDto`): the client fills
    the registry's members, the handler sets `installed`, `installedVersion` and `updateAvailable` (`PluginUpdateRule`, a
    newer semantic version than the installed one and than any staged upload).
  - **What is sent to the registry.** Only `GET {BaseUrl}plugins?q&category&page&pageSize`, `POST {BaseUrl}plugins/latest` with
    the ids of the installed, uploaded plugins (`{"ids":[...]}`), `GET {BaseUrl}plugins/{id}/versions/{version}` and the
    package address that reply names. No cookie, no token, no host name, no setting, no client or user data; an `Accept`
    header per request. The update check sends plugin ids, so an operator who does not want
    that leaves the registry off.
  - **Network rules.** Every request, redirect hop and package address must have the registry's scheme, host and port; a
    redirect is followed by hand (GET only, three hops) and only inside that origin, otherwise `PLUGIN_REGISTRY_PACKAGE_REFUSED`.
    `PluginRegistryConnector` (the handler's connect callback) resolves the name itself and refuses loopback, link-local
    (the cloud metadata address), unspecified, multicast and the other known metadata addresses, IPv4-mapped forms
    included; private ranges stay allowed. JSON replies are read with a 1 MiB bound; a package is streamed to a temporary file
    in the plugin `.staging` folder with a bounded write (never held in memory) and the file is deleted when the stream is
    disposed or on any failure. Free text from the registry is cleaned by `UntrustedText.Clean` (control characters to
    spaces, trimmed; name and author 200, description 2000 characters), the same cleaner `InstallPluginHandler` uses for
    its audit lines; an icon address is kept only when it is on the registry's host.
  - **Error codes** (`HostpanelErrorCodes`, resx en/ru/hy, `StatusMapping`; no upstream text ever reaches a response, the log
    gets the exception type only): `PLUGIN_REGISTRY_DISABLED` 409, `PLUGIN_REGISTRY_UNAVAILABLE` 502 (unreachable, an error
    status, a 404 anywhere but a release lookup), `PLUGIN_REGISTRY_TIMED_OUT` 504, `PLUGIN_REGISTRY_RESPONSE_INVALID` 502,
    `PLUGIN_REGISTRY_PLUGIN_NOT_FOUND` 404 (a 404 on the release lookup only), `PLUGIN_REGISTRY_PACKAGE_REFUSED` 502 (off-host
    address or redirect, over the size limit, a package of another plugin or version), `PLUGIN_REGISTRY_CHECKSUM_MISMATCH` 502,
    `PLUGIN_REGISTRY_NOT_TRUSTED` 403. The install also answers every refusal of `InstallPluginHandler` (manifest,
    signature, size, built-in, downgrade).
  - **Trust model.** Three checks, none replaces another. (1) The operator's policy, the setting `plugins.registry_trust`
    (`PluginRegistryTrust`, seeded `VerifiedOnly` by `PortalSettingsSeeder`, every change audited; an unknown stored value
    reads as `VerifiedOnly`): `VerifiedOnly` installs only entries the registry marks verified, `AnyRegistryPublisher` any
    listed publisher, `OwnKeysOnly` only a package whose `plugin.sig` key id is in `PluginSigning:OperatorKeys` (the
    maintainers' shipped keys do not count). (2) The checksum: the package must hash to the SHA-256 the registry published,
    its `plugin.json` must name the plugin id and version that were asked for, both read with the code the store and the
    loader use (`PluginManifestReader`, `PluginSignatureFile.Parse`, from a stream bounded at the cap plus one byte), and an
    archive with two entries differing only in case for either file is refused. (3) The signature: `PluginPackageUpload.RequireSignature`
    makes the package store check `plugin.sig` against the trusted keys **in every environment**, Development and Testing
    included, and the `plugins.allow_unsigned` setting does not override it; a registry package is never admitted unsigned.
    The registry is a catalogue, not a root of trust: a compromised registry can offer a package but cannot make the host
    install one a trusted key did not sign. A refusal before the install (trust, checksum, another plugin or version) is
    audited as `Refused Plugin From Registry` with a fixed reason; a success as `Installed Plugin From Registry` or `Staged
    Plugin Upgrade From Registry` with the id, version and digest the store reports. An install of an installed plugin is
    an upgrade like any upload: staged, applied at the next restart, downgrades refused.
- **Registry instance identity for restricted plugins (#1022).** A registry plugin whose `distribution` is `HostOnly` (read from the
  listing row and the plugin page; missing or unknown reads as `Download`) is asked for with this installation's identity, never with a
  licence key. The identity is one JSON document, `RegistryInstanceRecord` (`Application/Admin/Plugins/Common`): the Ed25519 key pair, the
  public address (`ClientBaseUrl`), the instance id and challenge the registry gave, the state (`RegistryInstanceState`: NotRegistered,
  Pending, Verified, Revoked, Error), the last failure code, the failure count and the next attempt. It lives in the settings row
  `plugins.registry_instance` (`PluginSettingKeys.RegistryInstance`, in `SecretSettingKeys.StandaloneKeys`), stored through
  `ISecretSettingCipher` by `RegistryInstanceStore`; the row is outside the settings catalogue, so no list or update reaches it, and no
  response carries a key (`RegistryInstanceDto` has the id, the address, the state and the sentence only). There is no table and no migration.
  - **One enrolment.** `RegistryInstanceEnrollment` (scoped, one at a time per process) does register then verify through the ports
    `IRegistryInstanceApi` (the two unsigned calls, `Infrastructure/Plugins/Registry/RegistryInstanceApi`) and `IRegistryInstanceKeys`
    (Ed25519 over the BouncyCastle library the package signature check already uses; no new dependency). It is idempotent: a Verified
    identity answers without a call, a failed attempt waits `RegistryInstanceBackoff` (30 seconds doubling to one hour) unless forced, and a
    revoked identity is left alone unless forced. The registry refuses one key twice, so when it asks for a fresh start (challenge failed
    with `INSTANCE_CHALLENGE_VALIDATION_FAILED`, unknown or not pending instance, key already registered) the pair is generated again and the
    two calls are made once more. Nothing is saved while the registry is off (`PLUGIN_REGISTRY_DISABLED`) or when `ClientBaseUrl` is not a
    public https address (`RegistryInstanceAddress`, `PLUGIN_REGISTRY_INSTANCE_ADDRESS_INVALID`). Three callers: the recurring job
    `EnrollRegistryInstanceCron` (started by `AdminScheduledJobsStartup`, every `ScheduledJobs:EnrollRegistryInstanceIntervalSeconds`, default 60;
    the back-off kept on the identity decides how often the registry is asked), the handlers that need a restricted plugin
    (`InstallRegistryPluginHandler`, `GetRegistryPluginHandler`), and `RetryRegistryInstanceCommand` (`POST /api/admin/plugins/registry-instance/retry`,
    forced, demo restricted). `GET /api/admin/plugins/registry-instance` reports the state (`GetRegistryInstanceQuery`).
  - **The challenge.** `GET /.well-known/hostpanel-registry/{id}` (`RegistryChallengeController`, anonymous because the registry has no
    credential for a new installation) answers exactly the stored challenge for that id as `text/plain` with `Cache-Control: no-store`
    (`BaseApiController.HandlePlainText`), and `PLUGIN_REGISTRY_INSTANCE_NOT_FOUND` (404) for any other id. The edge proxy must route the prefix to
    the API (`docker/caddy/Caddyfile`, `docker/nginx/templates/default.conf.template`).
  - **Signing.** `RegistryCallSigning` is the one definition of the signed text: `HOSTPANEL-REGISTRY-V1`, method, request target as sent
    (path and query, `?ticket=` included), lower-case hex SHA-256 of the body (the empty string: no signed call has a body), timestamp and
    nonce, joined with a line feed and no trailing one; `RegistryCallSigningTests` pins it against a vector made by an independent Ed25519
    implementation. `IRegistryRequestSigner` (`RegistryRequestSigner`) signs only while the identity is Verified and hands out the four
    header values, never the key. `PluginRegistryClient` signs the version list, the release details, the ticket request and the final fetch of the
    ticket address, on every hop (a fresh nonce each), and sends no `X-Licence-Key` on them; the ticket address must be on the registry's origin
    and carry the ticket. `POST plugins/latest` stays unsigned and its empty package address for a restricted plugin is not invalid.
  - **Refusals.** A 401, 403 or 429 of a signed call is read for the registry's error code in `type` only (bounded read) and becomes
    `PLUGIN_REGISTRY_INSTANCE_SIGNATURE_REJECTED`, `_CLOCK_SKEW`, `_REPLAY_REJECTED`, `_NOT_VERIFIED`, `_REVOKED`,
    `PLUGIN_REGISTRY_DOWNLOAD_TICKET_REFUSED` or `_INSTANCE_LIMIT_REACHED` (resx en/ru/hy, `StatusMapping`); a revocation or an unverified answer is
    reported to the identity (`ReportRefusalAsync`) so the admin page tells the truth. No registry text reaches a response or a log line.
- **Plugin routes: one safe door for every serving plugin (#639, #818).** A serving plugin (`PluginType.App`, or any type
  declaring `endpoints`; `IPluginEndpoints`)
  answers HTTP-shaped calls the host forwards, never a request or a token. Two thin catch-all controllers bind the request
  and dispatch once: `ClientPluginCallsController` (`/api/client/plugins/{id}/{**path}`, `Policies.Client`) and
  `AdminPluginCallsController` (`/api/admin/plugins/{id}/ext/{**path}`, `Policies.Admin`; `ext` keeps it apart from the
  administration routes `install`, `restart` and `DELETE {id}`). Each has one command, `CallClientPluginCommand` and
  `CallAdminPluginCommand` (folders `Application/Plugins/Commands/`), carrying the plugin id and a `PluginHttpInput`
  (method, path, query, the unread body stream, content type and length) that `API/Plugins/PluginHttpInputReader` builds, the
  same way for both. The part both areas share is `PluginCallRunner` (`Application/Plugins/Common`); each handler adds
  only its area's authorisation. Nothing about a plugin is trusted: the manifest was validated at load, and a route is
  looked up in it, never guessed.
  - **The order is the contract, and authorisation sits before plugin code.** `PluginCallGate` (`Application/Plugins/Common`)
    decides it, for both areas and for `PluginRateLimitFilter`, with a `PluginCallGateOutcome`. A client call: (1) the
    caller's account from `ICallerClientResolver` (`IdentityUnavailable`, then no account, whatever the plugin);
    (2) memory only, no I/O: the plugin is loaded and is a serving plugin (an app plugin,
    or another type that declares `endpoints`), and the manifest declares that method and path in
    that area (`PluginRouteMatcher`: segments compare ignoring case, a parameter takes one non-empty segment, a literal
    beats a parameter, a trailing slash matches nothing); (3) the route's permission; (4) only then the plugin's switch and
    settings (`IPluginEndpointResolver.PrepareAsync`, the `PluginActivator` gates, without constructing the plugin), the
    one database read. **An unknown plugin, a plugin that does not serve (another type without `endpoints`, or one whose declared
    capability is not implemented), a disabled one, an unconfigured one, an undeclared
    route, a wrong method and a route the caller lacks the permission for all answer the same 404 `PLUGIN_ROUTE_NOT_FOUND`,
    byte for byte**, and a refusal at step 2 or 3 never does the settings read, so neither the body nor the timing tells an
    installed plugin from an absent one; the real reason is only in the Debug log, as the enum, never the client's path.
    (5) `RunAsync`: the streaming route refusal, the body, the audit line, then the plugin is built and called. (#828 moved
    the permission check ahead of the enabled check and turned its 403 into this 404.) The rate limit filter measures only
    calls the gate admits, so a 429 cannot reveal a disabled or unconfigured plugin either.
  - **Client area.** The caller, the account and the permissions come from `ICallerClientResolver`, the single source of
    truth the permission attribute uses (owner, sub-user, identity plugin member). `IdentityUnavailable` answers 503
    `IDENTITY_PLUGIN_UNAVAILABLE`; a route permission not held answers the 404 `PLUGIN_ROUTE_NOT_FOUND` above, not 403; a caller who acts
    for no single account answers 404 `CLIENT_PROFILE_NOT_FOUND`, because the SDK promises a client area plugin a client.
    A route `permission` is a defined `ClientPermission` name (that flag is needed), `PluginPermissions.Any` (no
    permission, but still a client) or **absent, which means `ClientPermission.All`: a route that does not say who may
    call it is for the account holder alone**, like every portal action that must carry a decision.
    The controller is `[AvailableToEveryClientUser]` because the permission is dynamic (the matched route's), so
    `ClientPortalPermissionTests` is satisfied and the use case enforces it. `PluginCaller.Permissions` lists the single
    flags held, never `None` or `All`. Admin area: `Policies.Admin` is the whole check; the plugin gets no client, no
    permissions and an opaque user reference. An admin call carries no `PluginCall.Context` yet (slot context is a later
    issue).
  - **Unsafe methods need `X-Requested-With: XMLHttpRequest` or a bearer token.** The rule is one class, `API/Auth/CsrfRule`,
    shared by `LocalSessionCsrfMiddleware` (cookie requests, local mode) and `[RequireRequestedWith]` on both plugin
    controllers, which states it for these routes whatever credential the request carries (403 `CSRF_HEADER_MISSING`).
  - **Body cap (`PluginApiOptions`, section `PluginApi`, `MaxBodyBytes`, default 1 MiB, 1 to 64 MiB, validated on start).**
    A declared `Content-Length` over the cap is refused without reading; a body without one is read in chunks and refused
    the moment it passes the cap: 413 `PLUGIN_REQUEST_TOO_LARGE` before the plugin is called. The plugin sees the whole
    body or none of it. **Memory cost:** the body is read once by `PluginBodyReader` into one buffer and the plugin is handed a slice of it (no
    second copy), so a call holds up to `MaxBodyBytes` (up to 64 MiB) for as long as it runs. The buffer is never sized
    from a declared length beyond 64 KiB, because a declared length is only a claim (a caller who declares the whole cap and
    sends nothing must not make the host allocate it); it grows by doubling as bytes really arrive, so a chunked or large
    body briefly holds the old and the new array at once (up to about 3 times the body at the last growth step). Size
    `MaxBodyBytes` for the number of concurrent plugin calls a deployment expects, not only for the largest body.
    **Two guards keep the runner the only reader.** `[PluginBodyLimit]` sets the web server's per request body limit
    (default about 28.6 MiB, which may be below the cap) to `PluginApiOptions.ServerBodyLimit` (the cap, one read chunk of 8 KiB and 1/64 of the cap for chunk framing, which the
    server counts) on both plugin controllers, after authorisation: the runner stops at the first read that passes the cap, which
    is never more than one chunk beyond it, so it still answers its own 413 `PLUGIN_REQUEST_TOO_LARGE` first, while a
    request the runner never reads (unknown plugin, undeclared route, refused caller) is bounded by the server at
    that same size instead of being drained without limit. `[NoFormBinding]` removes every MVC value provider except the
    route's and the query string's, because MVC otherwise reads and buffers a urlencoded or multipart body before the
    action, past the cap, and leaves the plugin an empty body. Architecture tests pin `[RequireRequestedWith]`,
    `[PluginBodyLimit]`, `[NoFormBinding]` and the policy on both controllers; `PluginKestrelBodyTests` proves the limits on
    a real Kestrel socket (`KestrelBodyHost`: the same guards, `PluginHttpInputReader` and `PluginBodyReader` behind probe
    controllers), since the in-memory test server has no limit and the API's own entry point cannot be put on a socket by the
    test factory (the JasperFx run command disposes the host once a real server starts). **Those tests prove the two
    filters and the reader, not the whole pipeline through `PluginCallRunner`**: the probe controllers stand in for the
    doors, so the order of the runner's steps is covered by the in-memory call tests, and the reader and
    `PluginApiOptions.ServerBodyLimit` also have direct unit tests (`PluginBodyReaderTests`, `PluginApiOptionsTests`).
  - **Audit.** A call that is not a GET writes one activity log line before the plugin runs, through `IActivityLogger`:
    `Called Plugin Route - Plugin ID: {id} - Area: {Client|Admin} - Route: {METHOD} {declared template}`, the client for a
    client call and none for an admin call, stamped with the caller by the logger. Never the body, never the concrete
    path. A call refused before the plugin (404, 403, 413) writes nothing.
  - **Replies** (`PluginReplyDto : IForwardedReply`, written by `BaseApiController.HandleForwarded`, the only place a
    status the use case did not choose reaches the wire). `Json` and `NoContent` pass through with the plugin's status
    (camelCase body). `Problem` becomes the standard `ErrorResponse` (`application/problem+json`) with the plugin's status,
    the title of that status and its sentence (cut to 500 characters; the localised `PLUGIN_REPORTED_PROBLEM` when it gave
    none). **The code is written `plugin.{pluginId}.{code}`**: host codes are SCREAMING_SNAKE with no dot, so a plugin can
    never answer with a host code and a frontend that branches on one is never fooled; a code that is not 1 to 64 letters,
    digits, `_`, `.` or `-` is a plugin fault. **A plugin that throws** (or whose constructor throws, or returns no reply
    or a malformed problem) answers 502 `PLUGIN_FAILED` with no message and no stack, logged with the plugin id; only the
    caller's own cancellation propagates. **Streaming (#641).** A `Stream` reply is only accepted from a route that declares `streaming`
    (`PluginCallRunner.MapStream`); from any other route it is a plugin fault, 502 `PLUGIN_FAILED`, and the stream is
    never enumerated. A streaming route may still answer with JSON, no content or a problem, and a refusal or a throw
    before the first event is the ordinary reply with its own status. An accepted stream is a `PluginReplyDto` carrying a
    `Shared/Models/StreamedReply` (events plus `PluginApi:StreamHeartbeatSeconds`, default 15, 1 to 300);
    `BaseApiController.HandleForwarded` hands it to `HandleStream`, which returns `Shared/Infrastructure/EventStreamResult`:
    `text/event-stream`, `Cache-Control: no-cache`, `X-Accel-Buffering: no`, headers flushed at once, each event framed by
    `ServerSentEvent.Format` (`event`, `id`, one `data` line per line of data) and flushed, `: keepalive` after each
    silence of the heartbeat, and the plugin's enumerator opened with `HttpContext.RequestAborted`, so a disconnect cancels
    the plugin (`PluginReply.Stream` documents the `EnumeratorCancellation` the plugin needs). A plugin that throws after
    the first event ends the stream with an `error` event (`code` `PLUGIN_FAILED`, localized `message`, `partial`) and a
    `done` event (`PluginEventStream`, vocabulary `PluginStreamEvent`); nothing of the exception is written. The
    `plugin-stream` lease of `PluginRateLimitFilter` is registered for disposal with the response, so it is held for the
    whole stream. **A stream has a silence limit, not a length limit**: `PluginCallRunner.MapStream` gives the reply a
    `Shared/Models/StreamIdleLimit` (the call limit, the `error` plus `done` events that tell the client, and a log callback),
    and `EventStreamResult` ends the stream when the producer has sent no event for that long, counted from the start or from
    its last event. The host's own `: keepalive` heartbeat does not count as an event, so a heartbeat below the limit never keeps
    a silent plugin alive. The plugin is cancelled through its token, the client gets `error` with `PLUGIN_TIMEOUT` and `done`,
    and the `plugin-stream` lease is released with the response. A first event that arrives as the wait runs out is used, not cut.
    **The status is checked again by the host**, not trusted to the SDK factories: a `Json` reply must be 200 to 203 with a
    body, `NoContent` 204, a `Problem` 400 to 599 except 401 and 407; anything else (a reply built by reflection or by a
    plugin compiled against another SDK) answers 502 `PLUGIN_FAILED`, logged with the plugin id. **A plugin problem
    sentence is the plugin's own text**: the host cuts it to 500 characters but does not localise it, so it may be in any
    language, and only the host's own sentences (title, `PLUGIN_REPORTED_PROBLEM`, every other code) are in en, ru and hy.
  - **Route values are untrusted input.** The path is matched segment by segment against the declared templates after the
    server has decoded it once. A `..` or `.` segment is never resolved into another route (an undeclared shape is 404), an
    empty segment matches no parameter, and a parameter takes one segment whose text reaches the plugin unchanged: `..`,
    `%2e%2e` (still the literal text), `a%2Fb` (the server leaves `%2F` encoded, so it arrives as those five characters, not
    as a `/`), a backslash or a NUL character are all just text in `RouteValues`. A plugin must never use one as a file
    name, a path, an address or a query fragment without its own validation. The method is the real method: `HEAD` and
    `OPTIONS` are not mapped (404 or 405, no plugin code) and a method override header is ignored.
  - **Signals that remain, and why they are accepted.** The 404 that hides whether a plugin exists is the same body for
    unknown, disabled, another type and undeclared, but a caller can still tell the 404 from a 403 `CLIENT_PERMISSION_DENIED`
    or 413 and so learn that an enabled plugin declares that route; and the time to answer differs slightly between a
    refusal and a call that reaches plugin code. Both are accepted: the route list of an enabled plugin is not a secret (it is in
    the plugin manifest the administrator installed), the 403 is only given to a caller who is already signed in as a client,
    the permission must be answered before plugin code runs, and hiding the difference would mean running plugin code or adding
    sleeps for no one's benefit.
  - **A time limit per plugin call (#792).** `PluginApi:CallTimeoutSeconds` (default 30, 1 to `MaxCallTimeoutSeconds`) is the
    limit of every plugin route today; `MaxCallTimeoutSeconds` (default 600, 1 to 3600, never below the default limit) is the
    ceiling a per route limit will be capped at (`PluginApiOptions.CallTimeoutFor(int?)`) once the plugin SDK route record has a
    field to ask for one; until then no route can ask. The two are validated together at startup. `PluginCallRunner` gives the
    plugin a token that fires at the limit. Anything the plugin does after it fired, a throw or a reply it still returns, is
    `PLUGIN_TIMEOUT` (504), with no text of the plugin; the plugin id is logged, and the audit line of an unsafe call, written
    before the plugin runs, stays as written. A plugin that ignores the token is abandoned after `PluginApi:AbandonWaitSeconds`
    (default 5, 1 to 60) past the limit, so the request and its scope end. Its work still runs, so the abandoned calls are
    counted per plugin in the singleton `PluginAbandonedCalls` until the work ends, and while a plugin holds
    `PluginApi:MaxAbandonedCallsPerPlugin` of them (default 8, 1 to 1000) new calls to it are refused at once with
    `PLUGIN_TIMEOUT`, without calling the plugin.
  - **Opaque references (`IPluginReferences`, `Infrastructure/Plugins/PluginReferences`).** `PluginCaller.ClientRef` and
    `UserRef` are the URL-safe Base64 of an HMAC-SHA256 over `kind`, the plugin id and the host id, under a key derived
    (HKDF-SHA256, purpose label `innovayse-hostpanel-plugin-references-v1`) from the encryption key the host already
    requires. Stable for one plugin and one subject, different for every other plugin, never a host id, and not reversible
    or computable by a plugin because it does not hold the key. A keyed hash was chosen over a stored mapping because it
    needs no table, no migration and no write on the first call; the price is that references live as long as the
    encryption key, and rotating it gives every plugin new references. The identity plugin member's `UserRef` is their
    subject's, and their `ClientRef` is the account's client's, the same one the holder has.
  - **Host facts and the plugin store (#642).** `IPluginCallServices` is implemented by `HostPluginCallServices` (scoped) and
    builds, for each call, the `PluginCall.Host` and `PluginCall.Store` the SDK requires from one
    `PluginCallBinding` (built only through `ForClient(pluginId, clientId, clientRef, permissions)` or `WithoutClient(pluginId)`, so its reference is null exactly when its client is). **The binding is the one source of the client reference**:
    the area's handler builds it once (`CallClientPluginHandler`: the client from `ICallerClientResolver`, its
    `IPluginReferences.ForClient` reference and the caller's permissions; `CallAdminPluginHandler`:
    `PluginCallBinding.WithoutClient`), and `PluginCallRunner.RunAsync(target, binding, userRef, input, ct)` builds the
    `PluginCaller` from it (`ClientRef`, the held permission names) and hands the same binding to the services. Nothing else
    derives a reference, so `caller.ClientRef`, the store scope and the facts are always about one client.
    *Host API* (`Infrastructure/Plugins/PluginHostApi`, mapping in `Infrastructure/Plugins/Extensions/`): bound to the call's client, no method takes an id.
    It reads other modules' rows only through the port `IPluginHostFactsReader` (`Infrastructure/Plugins/PluginHostFactsReader`,
    like `IPluginSlotContextReader`), never a `DbSet`. **What it shows follows the caller's permissions** (security.md,
    visibility before capability), decided before anything is read: invoices only with `ViewPayInvoices`, services only with
    `ViewProductsServices`, and the profile's sign-in email only with `ModifyMasterProfile` (a sub-user without it never sees
    the address in the portal either); the name, currency and language go to every client caller. An admin call carries no
    client and gets no facts. The profile is the company name when set, else "First Last"; email and language come from the
    identity provider and are null when the owner has no identity account; currency comes from `IPayerCurrencyResolver`.
    Services and invoices are newest first (`CreatedAt`, then `Id`, descending), at most `PluginHostLimits.MaxListed` (50, cut
    in SQL), and invoices pass `Invoice.ClientVisibleFilter`, so a draft is never shared. A call whose client row is gone gets a
    null profile and empty lists. Domain statuses map to SDK statuses by name in an exhaustive switch; an unknown value throws, and so does a draft invoice, which the filter keeps from ever reaching it.
    `IssueSshAccessAsync` and `RevokeSshAccessAsync` are served by `PluginSshAccessService` (see "SSH access to a
    client's hosting (#758)" below).
    *Store* (`Infrastructure/Plugins/PluginStore`): table `PluginDataEntries` (aggregate `PluginDataEntry`: `PluginId`,
    `ClientRef`, `Key`, `Value` text, `CreatedAt`, `UpdatedAt`; unique `IX_PluginDataEntries_PluginId_ClientRef_Key`, nulls not
    distinct) and table `PluginDataScopes` (aggregate `PluginDataScope`: `PluginId`, `ClientRef`, `KeyCount`, `StoredChars`,
    `Revision`, xmin concurrency token; unique `IX_PluginDataScopes_PluginId_ClientRef`, nulls not distinct). A scope is per
    client in the client area and plugin wide (null `ClientRef`) on an admin call. A key is 1 to 200 characters of letters,
    digits, `.`, `-`, `_` and `:`; a value is text up to 65,536 characters (`PluginDataEntry.MaxValueLength`), empty allowed,
    null refused. A contract breach throws `DomainRuleException` `PLUGIN_STORE_KEY_INVALID` or `PLUGIN_STORE_VALUE_INVALID`
    (`Domain/Plugins/PluginStoreErrors`, sentences in en/ru/hy); the runner answers it like any plugin throw, 502
    `PLUGIN_FAILED`, and logs it, so these two codes never reach a caller and have no `StatusMapping` row.
    - **Quota, one rule.** `PluginApi:MaxStoreKeysPerScope` (keys, default 1000, 1 to 100,000) and
      `PluginApi:MaxStoreCharsPerScope` (characters of every key and value, default 4 Mi, from one full key and value to
      1 Gi), both on `PluginApiOptions`, validated on start, read as one `PluginStoreQuota`. `PluginDataScope.Admit` is the only
      place either is checked: a write that would take the scope past either measure is refused with `DomainRuleException`
      `PLUGIN_STORE_QUOTA_EXCEEDED`; a write that shrinks or keeps the size is always allowed, even in a scope over a lowered
      quota. The refusal fails closed (nothing is written, whether or not the plugin catches it), and the store raises the
      call's `PluginStoreQuotaSignal`, host state the plugin cannot set: when the plugin then throws, the runner answers 402
      `PLUGIN_STORE_QUOTA_EXCEEDED` (its own exact `StatusMapping` row, the 402 every quota refusal answers; en/ru/hy) instead of
      `PLUGIN_FAILED`, so only a refusal the host made carries that code. Inside a stream that already started the refusal ends
      the stream like any other throw.
    - **Writes and races: last writer wins.** Every `SetAsync` and `RemoveAsync` loads the scope row and the entry tracked,
      asks `Admit`/`Release` (each bumps `Revision`, so every write of a scope changes the scope row) and saves once. The scope
      row's xmin token therefore serializes the writes of one scope, which keeps the counters exact; a first write of a scope or
      a key meets the unique index instead, which `AppDbContext.SaveChangesAsync` translates to `ConcurrencyConflictException`
      like the gateway payment reference. On that conflict the store detaches what the attempt tracked and repeats the whole
      operation from a fresh read, at most five times, then lets the conflict propagate (answered `PLUGIN_FAILED`). So two
      writes of one key both succeed and the one that commits last is the value kept (there is no compare-and-set; a plugin
      that needs one keeps a version in its value); a remove that lost to another remove answers `false` (absent), and a write
      after another call removed the key creates it again. The save flushes only the store's change because `PluginCallRunner`
      saves its audit line before it builds the plugin, so nothing else is staged while plugin code runs.
    - **Purge is an operator action, not part of the removal.** `RemovePluginHandler` (Admin) refuses an invalid id and a
      built-in plugin as before. When the package store removed the files, or they were already gone but the plugin has a
      switch row, it switches the plugin off (`integration:{id}:is_enabled` set to `false` through
      `Setting.SwitchOffRemovedPlugin`), stages the activity line and saves once; files gone and no switch row answers
      `PLUGIN_NOT_FOUND`. The stored data stays. `DELETE /api/admin/plugins/{id}/data` (`PurgePluginDataCommand`) deletes
      it: refused with `PLUGIN_STILL_INSTALLED` (409) while the package is on disk, `PLUGIN_NOT_FOUND` when the plugin
      keeps no data; otherwise `Setting.RequestDataPurge` raises `PluginDataPurgeRequestedEvent` (with the request moment),
      which #802's interceptor writes to the outbox in the same transaction as the audit line (`Purged Plugin Data`).
      That event is the one purge step: the store purge is its handler, and any other module that keeps rows per plugin id
      adds its own handler of it. `PluginActivator.PrepareAsync` reads the switch on every call, so no new call reaches a
      still-loaded plugin from the removal on; a call already running at that moment can still finish its writes.
      `Plugins/Events/PurgeRemovedPluginDataHandler` (returns `Task`, so it is a queued chain with a durable inbox and the
      queued retry policy) goes scope by scope and deletes the entries last written at or before the removal
      (`UpdatedAt`, else `CreatedAt`), in batches of 500 through the context (no bulk delete), releasing each from its
      `PluginDataScope` and deleting the scope once it is empty. A retry may run hours later, after the same id was
      installed and switched on again, so rows written after the removal (by that new installation, or by a call that was
      already running) are kept with their scope; the scope's xmin token makes the purge and such a write conflict
      instead of interleaving, and the purge then re-reads the scope. A second delivery finds nothing old and stops. The
      answer is `requiresRestart: true` when files were removed and `false` when they were already gone (the admin store
      never lowers a banner it already shows).
    - No purge for clients: a client is never deleted, only closed (`ClientClosedEvent`), and a closed client keeps reading
      their history, so a plugin's per-client rows are kept like the rest of that history. The only path that removes clients
      is the demo reset, which truncates every model table, the store tables included. Rows are keyed by the opaque
      `ClientRef` (no client id column), so a future client erase would compute `IPluginReferences.ForClient` per plugin that
      has rows.
  - **SSH access to a client's hosting (#758).** `IPluginHostApi.IssueSshAccessAsync(serviceId, label)` and
    `RevokeSshAccessAsync(serviceId, keyId)` are served by `PluginHostApi`, which only hands the call's
    `PluginCallBinding` to `PluginSshAccessService` (Application `Services/Common`: the keys are the Services module's table) and turns a refusal into the SDK's
    `PluginHostAccessException(PluginHostAccessFailure)`; nothing in Infrastructure dispatches a use case. The checks run
    in this order and nothing is generated or sent before all pass: the call is a client-area call
    (`PluginCallBinding.Area`, set only by the two factories) whose caller holds `ViewModifyPasswords` (what the portal's
    own SSH key routes require, so a plugin never gives a sub-user more than the portal would), whose plugin's manifest
    declares `hostAccess: ["ssh"]`, and the label is 1 to 100 characters (`NotAllowed`); the service is the binding's
    client's (`NotFound` for a foreign and a missing one alike); its product is bound to the plugin (`NotAllowed`); SSH is
    `Available` on it (`Unavailable`); it holds fewer than `SshLimits.MaxKeysPerService` keys (`LimitReached`); the
    plugin's issue budget for the client is not spent (`ServerUnavailable`, fail closed: `IPluginSshIssueLimiter` takes
    from `PluginRateLimiter`'s `ssh-keys` tier, `RateLimit:SshKeys`, keyed per plugin and `client:{id}`); then
    `ISshIdentityGenerator` makes an Ed25519 pair and `SshKeyInstaller` installs the public half exactly as a client's key
    (`ServerUnavailable` when the agent fails).
    - **Bound products come from the operator's slot widget binding (#823)**, the one host-owned record of which
      products a plugin serves; #758 keeps no binding of its own. A product is bound when an operator binding row of one
      of the plugin's widgets names it and the product still exists. A manifest's own `productIds` never count for SSH:
      they are the author's claim, and a plugin listing every product id would otherwise reach all hosting before anyone
      bound it. Nothing bound means no hosting.
    - **The key limit is exact under concurrency:** the installer's save also moves `ClientServices.SshKeyRevision`
      (`ClientService.RecordSshKeyAdded`), so it updates the service row, whose xmin token makes two adds of one service
      conflict; the loser counts again after a short jittered pause, at most three tries, then answers
      `SSH_SERVER_UNAVAILABLE`. A lost race of the same key on `IX_SshKeys_ClientServiceId_Fingerprint` is mapped by
      `AppDbContext` to the same conflict and, counted again, answers `SSH_KEY_ALREADY_INSTALLED`; any other database
      error propagates. A retry detaches only the key and the service row it staged (`IHostpanelDbContext.Detach`), never
      what else the request staged.
    - **Availability is asked twice more:** right before the key pair is made, and after the install. A plugin switched
      off or removed in between gets `NotAllowed`; a key already installed by then is revoked through `SshKeyRevocation`,
      so no key outlives its plugin even when the removal's revoke pass ran before the key existed.
    - The key row carries `SshKeys.IssuedToPluginId`; the client sees it among the service's keys and may revoke it like any
      other, and the plugin may revoke only a live key issued to it (`SshKey.IsIssuedTo`, otherwise `NotFound`), through
      the same `SshKeyRevocation` the client's revoke uses, even after its product was unbound and on an inactive service.
    - **The issue budget is per process**, like every `PluginRateLimiter` tier: several API instances each keep their own
      window, so a plugin may issue up to the budget times the instance count. It is a flood guard; the per-service key
      limit, held by the service row's concurrency token, is the bound that holds across instances.
    - **Switching a plugin off does not revoke its keys; removing it does.** There is no domain event for a switch
      (the integration save writes a settings row like any other), and switching off is meant to be reversible: a
      switched-off plugin is not activated, so it can neither be called nor issue or revoke a key, and the keys it was
      issued stay on the client's **SSH Keys** tab, where the client can revoke each at any time. An operator who wants
      them gone removes the plugin.
    - **Removing the plugin revokes every live key issued to it.** `Setting.SwitchOffRemovedPlugin` and
      `CreateSwitchedOffForRemovedPlugin` raise `PluginRemovedEvent` in the removal's save, and
      `Services/Events/RevokeSshKeysOnPluginRemovedHandler` dispatches `RevokePluginIssuedSshKeysCommand`; a key the agent
      does not confirm stays `RevokeFailed` with the reason and the handler throws, so its durable queue retries until
      every key is off.
    - Issue and revoke each write an activity line on the client (`Issued SSH key ... to plugin {id}`, `Plugin {id} revoked
      SSH key ...`). **The private key is never stored, logged or put in an exception**: it exists only in the returned
      `PluginSshAccess`; the tests assert no column of the database and no log line contains it. `HostKeyFingerprint` is
      the agent's pinned fingerprint only when the customer-facing SSH address is the agent's own.
  - **Status codes.** `PLUGIN_ROUTE_NOT_FOUND` 404, `PLUGIN_REQUEST_TOO_LARGE` 413 (the `_TOO_LARGE` suffix),
    `PLUGIN_FAILED` 502 and `PLUGIN_STORE_QUOTA_EXCEEDED` 402 (exact `StatusMapping` rows); each has its sentence in
    en/ru/hy. `PLUGIN_STREAMING_NOT_SUPPORTED` went with #641.
  - **Rate limits (#640).** `API/Plugins/PluginRateLimitFilter` is a resource filter on both doors, marked
    `[PluginRateLimit(PluginArea)]`. It matches the route and measures the caller against `plugin-default`,
    `plugin-strict` or `plugin-heavy` (the manifest route's `RateLimit`, default when none) with
    `Shared/Infrastructure/PluginRateLimiter`, keyed by plugin and client account (client calls) or administrator
    (admin calls). A streaming route also takes a `plugin-stream` concurrency lease, held until the response is
    disposed. Budgets are `RateLimit:PluginDefault|PluginStrict|PluginHeavy|PluginStreamPermitLimit`. A refusal is the
    middleware's own 429 (`RateLimitingExtensions.WriteRejectionAsync`). The limiter is a no-limiter in Testing, and
    tests derive a host with an enabled `PluginRateLimiter`. The client account is resolved before the plugin budget
    is checked, so a refused call still costs one lookup, which the global limiter bounds. A refused stream call has
    already spent its tier permit.
  - **The 429 headers are the limiter's own (#874).** A refusal carries `Retry-After`, `X-RateLimit-Policy`, `-Limit`,
    `-Remaining` and `-Reset`, all written by `RateLimitingExtensions.WriteRejectionAsync` and nowhere else. Exposing the policy
    name and the budget is acceptable because both are published (the policy names and defaults are in the API reference and
    the configuration reference) and name no plugin, provider or account; they let a caller pace itself. The leak guard
    allows the four `X-RateLimit-*` names on a 429 only, so a header of that name copied onto any other response, a
    provider's own limit headers for one, is removed like any vendor header (`LeakGuardTests`). CORS does not expose them to
    a browser script on another origin, and no `Access-Control-Expose-Headers` is to be added for them without a reason.
  - A streamed call ends when the plugin ends, when the client leaves, or when the producer is silent for the call limit
    (see the stream silence limit above).
  - **A streamed call owns its own dependency scope (#851).** A stream is read by the response after the use case has
    returned, and Wolverine disposes the use case's scope, with the `AppDbContext` the store and the host API write through,
    as soon as the handler returns; every streamed call that touched its store then read a disposed context. So
    `PluginCallRunner` opens an `IPluginCallScope` (`IPluginCallScopeFactory`, `Infrastructure/Plugins/PluginCallScopeFactory`,
    a scope of the root container) for every call to a route that declares `streaming`, before the plugin is built, and takes
    the plugin (through that scope's `IPluginEndpointResolver`, which checks the switch and settings once more; a plugin
    switched off since the gate answers the same 404 `PLUGIN_ROUTE_NOT_FOUND`), its store and its host API from it. When the
    plugin answers with a stream the scope travels with it as `StreamedReply.Owner`, and `EventStreamResult` disposes it once
    the stream ended, failed or the client left; a producer step abandoned after the settle wait keeps it until that step
    ends, so it is never disposed under running plugin code. Any other outcome of a streaming route (JSON, problem, refusal,
    timeout, throw) disposes it before the runner returns. Every route that does not declare `streaming` runs on the use
    case's scope as before. `PluginStreamScopeTests` proves both doors, the store and the host API between events, and the
    disposal after the end and after a disconnect.
- **Plugin slot index: widgets for a verified context (#644).** The answer is `PluginSlotIndexDto` with
  `PluginSlotWidgetDto` items. **Nothing of a plugin is built or called**: the index is read from manifests, so a foreign
  id cannot reach plugin code.
  - Endpoints: `GET /api/client/plugins/slots/{slotId}` (`ClientPluginSlotsController`) and
    `GET /api/admin/plugins/slots/{slotId}` (`AdminPluginSlotsController`). The query names the record the page shows
    (`serviceId`, and `clientId` on an admin slot). Use cases: `GetClientPluginSlotQuery` and `GetAdminPluginSlotQuery`
    (`Application/Plugins/Queries/`), sharing `PluginSlotIndexBuilder`. Rows of other modules are read through
    `IServiceOwnership` and `IPluginSlotContextReader`, never from the handler.
  - Widget view (#646): `GET /api/client/plugins/slots/{slotId}/widgets/{pluginId}/{widgetId}?serviceId=`
    (`GetClientPluginSlotWidgetQuery`) answers `PluginSlotWidgetViewDto {pluginId, id, title, view, externalOrigins}`, the
    widget's view document with its labels resolved, read from the views kept at load (no plugin is built). It is served
    only for a widget the index would list for the same caller and context: both use cases share `ClientPluginSlotGate`
    (slot, caller, context) and `PluginSlotIndexBuilder`. Unlike the index it resolves the caller first and folds every
    refusal (unknown or admin slot, no account, missing or foreign service, a widget that is unknown, hidden, not
    permitted, filtered out by product or of a plugin that is off, a missing view) into one 404
    `PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND` with the same body, as the page endpoint does; only `IDENTITY_PLUGIN_UNAVAILABLE` (503)
    differs. The client reads each widget on its own, so a slow or broken one never holds up the page or the others.
  - Order of checks, which is the contract: the slot (unknown or of the other panel is `PLUGIN_SLOT_NOT_FOUND`), the
    caller's account, the context, then the index. A client slot's service must be the caller's own
    (`SERVICE_NOT_FOUND` for foreign and missing alike, one query that also yields the product); an admin context must
    exist (`SERVICE_NOT_FOUND`, `CLIENT_NOT_FOUND`) and a client named beside a service must be its own. A slot that needs
    a record and gets none is `PLUGIN_SLOT_CONTEXT_REQUIRED` (400, the default arm of `StatusMapping`).
  - Filters: the plugin is an enabled, configured serving plugin (the `IPluginEndpointResolver` gates); the operator has not
    hidden the widget; the caller holds its `permission` (`PluginClientPermissions`, the rule a route uses: absent means
    the account holder alone, `Any` means none, anything unparsable fails closed; an admin slot takes none); a widget
    with `productIds` is listed only when the slot's service orders one of them. Titles resolve through `PluginText`.
    The order is `order`, then plugin id, then widget id, ordinal.
  - Manifest rules: a slot widget id matches `[a-z0-9][a-z0-9_-]*` and is unique per slot within a plugin, and the
    plugin id is a `PluginIdentifier`.
  - Hide setting: `plugins.hidden_slot_widgets` (`PluginSettingKeys.HiddenSlotWidgets`, seeded empty) holds comma
    separated `slot:plugin:widget` entries. Empty entries are ignored, and the validator and the reader share one grammar.

- **Plugin UI index and pages (#643).** Four read endpoints serve what a plugin adds to a panel:
  `GET api/client/plugins/ui` (`Policies.Client`, `[AvailableToEveryClientUser]`) and `GET api/admin/plugins/ui`
  (`Policies.Admin`) answer the index `PluginUiDto { Menus, Pages }` (menus `{pluginId, id, label, page, icon, order}`,
  pages `{pluginId, id, path, title}`; both empty with no available plugin); `GET api/{client|admin}/plugins/ui/{pluginId}/pages/{pageId}`
  answers `PluginPageViewDto {pluginId, id, path, title, view, externalOrigins}` (`externalOrigins`: the manifest's
  declared origins as written, empty when none, which the panels need to render a link to an absolute `https` address).
  A fifth, `GET api/{client|admin}/plugins/ui/{pluginId}/pages/by-path?path=` (`GetPluginPageByPathQuery`), finds the page
  by its panel path: both lookups go through `PluginPageReader` (one order of checks, below), and
  `PluginPagePathMatcher` picks the page: the path starts with `/`, is at most 512 characters, holds no empty, `.` or `..`
  segment, no backslash and no control character (otherwise no page), segments compare ignoring case, a parameter takes
  one segment, and the page with most literal segments wins (manifest order on a tie). **Availability is the same gate
  as the routes:** `IPluginUiSource` (`Infrastructure/Plugins/PluginUiSource`) lists a plugin only when it is loaded, a
  serving plugin (#818) and passes `PluginActivator.PrepareAsync` (enabled and configured), so a page shows exactly when its routes
  answer. The answer is computed for every plugin in one pass and kept for 10 seconds by `PluginAvailabilityCache`
  (singleton), used by the index and both page lookups alike; `IPluginAvailabilityInvalidator` drops it after the save
  of `RemovePluginHandler` and `SaveIntegrationConfigHandler`, so a
  switch shows at once. The slot index (#644) does not use the cache yet; it still asks `IPluginEndpointResolver` per
  plugin.
  **Permissions:** a client page is listed or served only when `PluginClientPermissions.IsHeld(held, page.Permission)`,
  the one rule the route door and the slot index also use (`Any` needs none, a defined name needs that flag, **absent
  means `ClientPermission.All`, the account holder only**, anything unparsable fails closed); the admin area holds
  `All` and so has no filter. The viewer is resolved by `PluginUiViewer.PermissionsAsync`, the way the route door
  resolves its caller (`ICallerClientResolver`). A menu is listed only beside a visible page.
  **Order:** `PluginUiSource` is the ordering contract: it lists plugins by manifest id (ordinal), so neither load order
  nor instance changes it, and the index keeps that order without sorting again; pages keep manifest order within a plugin, and menus are ordered by
  `Order`, then plugin id, then menu id (ordinal).
  **Labels:** `PluginUiTexts.For(manifest)` resolves a menu label and a page title through `PluginText.Resolve` (one
  rule for menus, pages, slots and views), and `Localise` every label of a view, in the request language
  (`X-Language`, so `CurrentUICulture`, normalised to a supported language, otherwise `en`), English when that locale
  lacks the key, otherwise the value itself as plain
  text. `PluginText` compares locale codes ignoring case, as the manifest validator does, so `EN` reads like `en`.
  **Index answers:** 503 `IDENTITY_PLUGIN_UNAVAILABLE` when the identity plugin cannot say who a client caller is, 404
  `CLIENT_PROFILE_NOT_FOUND` when the caller acts for no single account, otherwise the filtered index.
  **Page answers (the order is the contract):** the viewer is resolved first, so the lookups run for every caller alike.
  503 `IDENTITY_PLUGIN_UNAVAILABLE` stays, because an outage says nothing about the page. **Every other refusal is the
  same 404 `PLUGIN_PAGE_NOT_FOUND` with an identical body**: a malformed `pluginId` or `pageId` (no 400, which would
  say which ids are well formed), an unknown plugin, a disabled or unconfigured one, a plugin
  that is not an app plugin, an undeclared page, a missing view, a client caller who acts for no account and one who
  does not hold the page's permission. A page never answers 403 or `CLIENT_PROFILE_NOT_FOUND`, so the endpoint is no
  oracle for which plugins and pages exist. The route door (#639) differs on purpose: it still answers 403
  `CLIENT_PERMISSION_DENIED` after the 404, a signal accepted and explained under "Signals that remain".
  **The plugin ids `ui` and `slots` are reserved** (`PluginIdentifier.Reserved`): the routes
  `api/{area}/plugins/{id}/{**path}` would otherwise swallow `plugins/ui` and `plugins/slots`, and a plugin with either id
  would shadow the index or the slot index. `PluginIdentifier` asks two
  questions: `IsWellFormed` (the shape: lower-case words joined by single hyphens, at most 64 characters) and `IsValid`
  (`IsWellFormed && !IsReserved`, whether a plugin may take the id). An install of a manifest with a reserved id is
  refused with `PLUGIN_MANIFEST_ID_INVALID` (`PluginManifestRule.PluginIdInvalid`), and the loader skips a reserved
  folder or manifest id with one warning. Listing, removal (`RemovePluginValidator`, `PluginPackageStore`) and
  `Setting.SwitchOffRemovedPlugin` / `CreateSwitchedOffForRemovedPlugin` check only the shape, so a plugin installed as
  `ui` or `slots` before the id was reserved stays listable and removable, and its data is still purged. #647 (admin plugin pages)
  builds on these shapes.

## CQRS — Wolverine, conforming

Wolverine 6.61.0. `Extensions/MessagingRegistration.cs` registers discovery over the API and
Application assemblies; handlers are never registered by hand and never called directly. One
folder per use case under `Application/<Feature>/{Commands,Queries}/<UseCase>/`.

- **Handler code is generated at image build time and loaded, never compiled, in production.**
  `MessagingRegistration.TypeLoadModeFor` picks `Static` everywhere except Development (`Auto`) and the
  Testing environment of the integration suite (`Dynamic`). The API Dockerfile runs `codegen write`, which
  compiles with Roslyn and so needs the `WolverineFx.RuntimeCompilation` package (Wolverine 6 moved the
  compiler out of core), then restores and publishes the API with `OmitRuntimeCompilation=true`, which the
  API csproj honours by leaving that one reference out: the published image carries neither the package
  nor the scripting and Visual Basic Roslyn assemblies it adds (about 9 MB). The core Roslyn assemblies
  (`Microsoft.CodeAnalysis*`, `BuildHost-*`) stay in the image regardless, because
  `WolverineFx.EntityFrameworkCore` depends on `Microsoft.CodeAnalysis.CSharp.Workspaces`,
  `Microsoft.CodeAnalysis.Workspaces.MSBuild` and `Microsoft.EntityFrameworkCore.Design` itself; dropping them
  would mean hand-listing a transitive closure of a package that may load it. A plain restore or build leaves
  the property unset, so Development, the tests and `dotnet watch` keep the package.
  Since Wolverine 6.30 a `Static` host refuses to start (`MissingPreBuiltTypesException`, naming the chains)
  when a handler chain's generated type is missing, where earlier versions started healthy and lost the
  messages of that type; `Production.AssertAllPreGeneratedTypesExist` is set in `Static` mode as well, for the
  environment check over every code collection. `WolverineStartupTests` pins both, and starts the current
  Wolverine on a message store schema created by 5.41 (`Fixtures/wolverine-5.41-schema.sql`).
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
- **Durable messaging.** Messages are persisted in PostgreSQL (`opts.PersistMessagesWithPostgresql`) in
  Wolverine's own schema `wolverine` (incoming, outgoing, dead-letter and node tables, lower-case names that are
  Wolverine's and exempt as framework-owned). The connection string is `ConnectionStrings:DefaultConnection`, read
  while Wolverine is configured; a missing one refuses startup with the key's name. The schema is created and
  updated by Wolverine at host start (`AutoBuildMessageStorageOnStartup = CreateOrUpdate`, set explicitly), in
  every environment including `Testing`, and **not by an EF migration**: mapping Wolverine's tables into
  `AppDbContext` would make the EF model own a framework schema whose shape changes with the Wolverine version and
  put framework tables under `ModelDiffBaselineTests` and `DatabaseIdentifierNamingTests`. EF migrations run as
  before. **The deploy database user needs `CREATE` on the database** (not only on `public`), because Wolverine
  creates the `wolverine` schema itself at the first start; without it the host refuses to start on that first boot. `opts.UseEntityFrameworkCoreTransactions()` registers the `IDbContextOutbox` the domain-event interceptor
  uses, **without `Policies.AutoApplyTransactions()`**: that policy would add a second `SaveChangesAsync` and an
  explicit transaction around every handler that takes the context, changing the unit of work of handlers that
  already save themselves (some more than once on purpose), while the interceptor's outbox already covers every
  save, inside a handler or not (startup and hosted services, seeders). `DurableQueuePolicy`
  (`Shared/Infrastructure`, an endpoint policy) makes a local queue durable only when a chain it serves is delivered
  from a queue (`QueuedDelivery.IsQueued`, the same rule the retry policy uses): events and commands published for
  later get a database inbox, while the queues of request/response use cases, which are only invoked inline, stay in
  memory. Making every queue durable gave each host several hundred idle durable receivers and their timers, and three
  integration shards that fit in 5 GB on develop ran out of memory. `IRecurringJob` messages and `ResetDemoDataCommand` go to
  the buffered in-memory queue `recurring-jobs`, because those chains re-schedule themselves and are re-created on
  every start, so a durable scheduled run would add a chain per restart (scheduled messages on a buffered queue stay
  in memory; `AlwaysMakeScheduledMessagesDurable` is left off). Failures on a queued chain follow
  `DurableDeliveryRetryPolicy` (`Shared/Infrastructure`, beside `RecurringJobTimeoutPolicy`), which applies only to
  chains delivered from a queue: not `IRecurringJob`, and a handler that returns no response. Request and response
  chains reached by `InvokeAsync` are left out on purpose, because Wolverine would run the cooldown retries inline
  there too and a controller's command must answer once. A `ValidationException` or `DomainException` goes straight
  to the dead letters (a retry changes nothing); anything else is retried with cooldowns of 1 s, 5 s and 30 s, then
  rescheduled durably after 5 min, 30 min and 2 h (this survives a restart), then dead-lettered in
  `wolverine`'s dead-letter table. The values are documented constants on the policy, structure rather than
  per-tier configuration. The durability mode is the default `Balanced`: a rolling deploy briefly runs two
  containers on one database, and `Balanced` assigns inbox ownership per node where `Solo` would let both recover
  the same envelopes. **Handlers of one event are separated** (`opts.MultipleHandlerBehavior =
  MultipleHandlerBehavior.Separated` in `MessagingRegistration`): each handler of an event gets its own durable local
  queue and its own chain (`HandlerChain.ByEndpoint`, which `DurableDeliveryRetryPolicy` reaches too), so a failing
  handler is retried and dead-lettered alone and never re-runs a sibling that already succeeded. Every handler
  reached through a durable queue must still be idempotent on its own, because its own retry, a recovery after a
  crash or a second delivery runs it again. Production code never `InvokeAsync`s an event: an event is published
  (the outbox, or `PublishAsync`), since an inline invoke of a separated event would not reach its per-handler
  queues. Tests publish an event through `Fixtures/MessageDispatch.PublishAndWaitAsync` (an extension on
  `IServiceProvider`), which publishes with Wolverine's message tracking and waits until every handler's queue has
  finished, ignoring the `recurring-jobs` schedule. The demo reset truncates the EF model's
  tables only and leaves the `wolverine` schema alone: truncating the inbox under a running durability agent would
  break its ownership bookkeeping, and a durable message about a truncated row finds nothing. The integration test
  factory supplies the connection string with `UseSetting`, not `ConfigureAppConfiguration`, because Wolverine
  reads it from the `WebApplicationBuilder` before `Build()`, and configuration added through
  `ConfigureAppConfiguration` arrives only after it. Wolverine builds its schema in each factory's own database at
  host start. A test change that adds a new top-level folder in `Integration.Tests/` still needs that folder added
  to a shard (see "Tests").
- **No identity on a message** (`NoIdentityOnMessagesTests`); handlers read it from
  `ICurrentUserService` (`Shared/MultiTenancy/`).
- **Client portal permissions are enforced on the server, per action.** Every action behind `Policies.Client` carries
  exactly one of `[RequiresClientPermission(ClientPermission.X)]` or `[AvailableToEveryClientUser]`
  (`API/Authorization/`; `ClientPortalPermissionTests` fails otherwise). The filter asks `IClientAccess`, which reads the
  caller from `ICurrentUserService`: the owner of a client record holds every permission, a linked user holds the
  stored grant, anyone else holds none, and a refusal is 403 `CLIENT_PERMISSION_DENIED`. The action-to-permission
  table is in the remarks of `RequiresClientPermissionAttribute`. The use cases behind the portal resolve the
  account the caller acts for through `ICallerClientResolver` (owner, linked user or identity plugin member), so a
  linked user with no record of their own acts on the account they are linked to.
- **Architecture tests** (`Architecture.Tests`): `ControllerBase`, `ExplicitConstructor`,
  `NoRequestTypes`, `NoIdentityOnMessages`, `PortPlacement`, `EnumPlacement`, `NoVendorNames`, `PluginNeutralCore`,
  plus resource coverage, kernel purity, SDK contract and use-case shape. Application types carry
  no vendor names (`GetControlPanelSsoUrl`,
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
  (`InvoiceCreditExtensions` in Billing) is one save. Every change names its `CreditMovementType` (`ClientCreditChange`) and the
  port writes it to the client ledger in that same save: the `Transaction` that already holds the money is stamped
  (`CreditMovement`, signed `CreditChange`, `BalanceAfter`), a movement that moved no cash becomes a credit-only entry
  (`Transaction.CreateCreditMovement`, no money in or out, left out of the cash views by `Transaction.MovedMoneyFilter`), and an
  entry of the history cannot be edited or deleted (`Transaction.EnsureEditable`). A credit top-up is an `Invoice` with
  `IsCreditTopUp` (`Invoice.CreateCreditTopUp`); every path that settles an invoice calls `CreditPaidTopUpAsync`
  (`CreditTopUpExtensions`) in the settling save, so the balance rises once, and a top-up is refunded by hand only.
  A credit note (`CreditNote`, `CreditNoteItem`, `CreditNoteStatus` Draft/Issued/Applied/Voided, `CreditNoteMethod`
  AccountCredit/GatewayRefund) reverses part of a paid invoice at the invoice's own tax rate and is not invoice credit. It is numbered
  `CN-0001` from its own sequence through `INumberSequence` (`NumberKind.CreditNote`), taken as the last step before the save and reused
  across a retried save; a database sequence cannot promise there are no gaps, and the numbering is a cautious default not checked
  against any local law (documented in `docs/en/staff/billing.md`). The cap lives on the invoice (`Invoice.CreditableAmount`,
  `CreditNotedAmount`, `EnsureCreditable`): the charge before credit less every refund and every account credit note, and every
  account credit note also lowers `RefundableAmount`, so the same money never goes back twice. `CreditNotedAmount` changes the invoice
  row, so two notes racing on one invoice fail the row's concurrency check and the loser is retried on what the winner saved. An
  AccountCredit note moves the balance through `IClientCredit` (`CreditNoteIssued`, voided as `CreditNoteVoided`) in the same save; a
  GatewayRefund note dispatches `RefundInvoicePaymentCommand` first (one refund, listed once) and records the note under its reference,
  and cannot be voided. A credit top-up and an unpaid invoice take no note. A note is never deleted. `IncomeExtensions` reads payments
  and refunds only, so a note adds no income of its own (`CreditNoteIncomeTests`). Its PDF goes through the same renderer and render
  limit as an invoice (`IInvoicePdfRenderer.RenderCreditNoteAsync`, labels in `CreditNotePdfLabelSets` for en/ru/hy, Armenian
  fonts as for invoices). The renewal read models belong to their owners and Billing
  reaches them over the bus: Services owns `GetRenewableServices`, `GetServiceBillingStates`,
  `ListOverdueSuspendedServices`, `ListUndatedRenewingServices` and `ExtendServiceRenewal`; Domains owns
  `GetRenewableDomains`, `GetDomainRenewalStates` and `GetTldRenewPrice`. The setup token kept
  among the settings is issued, checked and retired through `ISetupTokenStore`. Provisioning counts as part of the
  Services module (both drive `ClientServices`). One documented writer exception: the migration importers
  (`Application/Migration/Importers`, with `MigrationClientResolver` and `MigrationProductLookup`) write
  many modules' tables directly, because they are a bulk import of legacy data across modules that runs as
  one job with its own log; the reason is written on `IMigrationEntityImporter` and the entry is marked
  `CrossModuleAccess.Writer` in `CrossModuleReadModels`. No other writer joins it without an owner decision.
- **Domain events are dispatched through the outbox, in the same transaction as the change.** `BaseEntity.DomainEvents`
  collects them and `Infrastructure/Persistence/DomainEventDispatchInterceptor.cs` writes them to Wolverine's
  PostgreSQL outbox (`IDbContextOutbox`, enrolled on the context) on the context's current transaction, so an event
  exists if and only if its state change committed. When a tracked entity has pending events
  (`BaseEntity.HasDomainEvents`, so a deferred factory is not evaluated before the insert assigns ids) and no
  transaction is open, the interceptor begins one and owns it: after the save it takes and clears the events,
  publishes them, commits and then flushes them to the local durable queues (if the process dies between the commit
  and the flush, the durability agent recovers them from the stored envelopes). Inside a caller's explicit transaction
  the events are flushed when that transaction commits and dropped with it on a rollback. A failed save rolls back an
  owned transaction and leaves the events on the entity, so a retried save still carries them. A publish failure
  throws and fails the save instead of being logged afterwards: the write and its announcement are one unit.
  Synchronous `SaveChanges` is not intercepted; nothing saves synchronously. `IDomainEventGate` is unchanged: it
  suppresses dispatch for bulk writers (`MigrationPullWorker`, `DevDataSeeder`; tests use
  `Fixtures/SuppressedDomainEventGate`), whose events are taken and discarded with no transaction opened and nothing
  written.
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
  only trigger idempotent commands and log a refusal or failure for staff instead of throwing. Each runs on its own
  queue (handlers are separated, see "Durable messaging"), so one of them failing never re-runs the others.
  `Plugins/Events/PurgeRemovedPluginDataHandler` deletes a removed plugin's store rows written up to the operator's purge request on
  `PluginDataPurgeRequestedEvent`; it is idempotent and writes only the Plugins module's own tables (see the plugin store under "Plugin routes").
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

**A client group's discount is taken once, when the order is placed, and renewals keep that price.** `PlaceOrderHandler`
reads the discount of the ordering client's group (the default group for a client the order creates) and takes it off
every product line through `PriceDiscount.Apply`, so the order line, the invoice line and the service's recurring amount
all carry the discounted price; a domain line keeps its registry price. `InvoiceItem.DiscountPercent` snapshots the
percent on the invoice line and is cleared only when staff edit the unit price. Renewal invoices bill the service's
recurring amount, which already is the discounted price, and never read the group again, so moving a client to another
group or changing a group's discount touches no existing invoice and no existing service, and a renewal is not
discounted a second time.

**A manual bank transfer wait is bounded and never loses a card session's money.** `Invoice.RequestBankTransfer` leaves the hosted
session on the invoice (the choice is refused only inside `GatewaySessionWindow`). While the transfer waits
(`IsBankTransferPending`), `ReconcileGatewayPaymentsCronHandler` asks the gateway about that session like an open invoice's, without the
24 hour limit, and a settled invoice stays in the sweep for the stalled window after its `PaidAt` as well as after the session start (a
transfer confirmed late). `CompleteGatewayPaymentHandler` settles a pending transfer with a full card payment (`Invoice.WithdrawBankTransfer`,
then `Settle`), and a payment that finds the invoice already settled goes to the balance through the hand-back path. The wait itself ends
by `RejectBankTransfer`, by `LapseBankTransfer` (the daily `CheckOverdueInvoicesCron` dispatches `LapseBankTransferCommand` for a wait older
than the `expiry_days` field of the bank transfer integration, 14 when unset or not a positive number) or by the card payment; each returns
the invoice to Overdue when its due date has passed, otherwise Unpaid, so the overdue and suspension flow applies again. The method is
listed and can be chosen only while at least one bank detail or the instructions are set (`BankTransferSettings.HasPayerDetails`), and
`MyInvoiceDto.IsBankTransferPending` is how the portal tells a real transfer from an imported `PaymentPending` invoice. An account credit
note is refused while a gateway refund of the invoice is in flight (`Invoice.EnsureCreditable(total, now)`), and an unrecorded refund's
amount is held out of `CreditableAmount`. The open top-up invoices per client are capped by `billing.credit.topup_max_open`.

**Renewal invoicing is serialised per client.** `RaiseClientRenewalInvoicesHandler` opens a transaction and takes
a transaction-scoped advisory lock (`IHostpanelDbContext.LockForTransactionAsync`, key
`RaiseClientRenewalInvoicesHandler.LockKeyOf(clientId)`) before it reads the duplicate guard, and commits after the
last invoice and credit change. Two callers for one client (the daily sweep, a staff "Generate renewal invoice" click,
another instance) therefore run one after the other, and the second one finds the first one's invoice. Staff reach the
same use case through `POST /api/billing/client/{clientId}/renewal-invoices` (`Manual` on the command only adds an
activity log line). The admin Upcoming Renewals page (`/billing/renewals`) lists services and domains inside a 30, 60 or
90 day window (`RenewalWindow`), whether or not they renew automatically, switches `AutoRenew` through
`SetServiceAutoRenew` (domains use the existing `SetAutoRenew`), and lists domain renewal lines left claimed
(`ListClaimedDomainRenewals`). `ReleaseDomainRenewalClaim` clears `RenewalAttemptAt` through `Invoice.ReleaseRenewalClaim`
after staff checked the registrar, with an activity log line; the line stays unapplied, so the next sweep renews the
domain again, which is why the page asks for confirmation. The confirmation dialog is a warning in so many words: a release
of a renewal the registrar already carried out renews the domain twice and is charged twice, so staff check the registrar
first (`billing.renewals.releaseMessage`, and `docs/en/staff/currencies.md`). `ClaimedDomainRenewalDto` carries the client's name.

**Renewal invoice lines are not unique per period, on purpose.** The indexes on `InvoiceItems`
`(ClientServiceId, RenewalPeriodStart)` and `(RegisteredDomainId, RenewalPeriodStart)` stay non-unique: a
cancelled invoice's renewal line must not block raising that period again. Double raising is prevented
by two things instead: the single runner of each recurring pass (the `IJobLease` lease, so one instance
at a time runs `ProcessRenewalsCron`), and the `AnyAsync` guard in `RaiseClientRenewalInvoicesHandler`,
which looks only at invoices that are not `Cancelled` and refuses a target that already has an open
renewal line, a paid line not yet applied, or a line for the same period. A unique index would need a
filter on the invoice status, which lives on another table, so it cannot be expressed as an index.

**A plan change is one request, one proration rule, one completing use case (#79).** `ServiceChangeRequest`
(`ServiceChangeRequests`) is Pending, Approved, Completed or Rejected, and only its own methods move it. The cost of a
mid-cycle move is worked out in one place, `ServicePlanChangeProration` (Domain, pure): `|new - old|` times the days left
of the cycle over the cycle's length, the cycle being `RenewalPeriod.MonthsIn` back from `NextRenewalAt`, a started day
counting as a day, rounded by `CurrencyCode.RoundToMinorUnits`, never negative; a plan at the same price is refused. The
options the client sees and the quote the request is made from both come from `ServicePlanChangeQuoter`, so the figures
cannot differ. An upgrade is approved at once and its charge is an invoice raised through `CreateInvoiceCommand`; the
request keeps the invoice id (an index, no foreign key, the invoice is Billing's) and `ServiceChangeInvoicePaidHandler`
completes it on `PaymentReceivedEvent`. A downgrade is approved at once only when `billing.plan_change.auto_approve_downgrades`
is on, otherwise by staff. `CompleteServiceChangeCommand` is the only place a change is applied, from automatic approval,
staff approval and the payment event alike: it asks the panel for the new package first (`ChangePackageCommand`, only for a
service holding a hosting account), and only then stages the plan, the price, the `PlanChangeCredit` of a downgrade, the
Completed status and the activity line in one save. A refusal by the panel therefore changes and credits nothing and leaves
the request Approved, and approving it again repeats the call; the request's `xmin` token stops two runs crediting twice.
Rejecting an approved upgrade cancels its unpaid invoice first. The Services handlers never touch the `Invoices` table:
they raise, read and cancel invoices through the Billing use cases.

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
  new gateway payment or stored-method save (`StartGatewayPayment`, `BeginSaveStoredMethod`) and is not
  billed: `RaiseClientRenewalInvoices` refuses it and `ProcessRenewalsCron` leaves it out. The refusal is
  403 `CLIENT_SUSPENDED`. Settling a payment that is already in flight (`CompleteMyGatewayPayment`,
  `CompleteOrderGatewayPayment`) is never blocked, so money already taken is recorded.
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
no longer exists. Today the Reseller role can sign in and can do what any signed-in user can; the only staff
route it reaches is `GET /api/admin/version` under `Policies.Staff` (Admin or Reseller), which returns the
running version and names no client data. The admin panel itself still opens only for an Admin. Introducing reseller-owned clients is a separate design (a `ResellerId` on `Clients` and a scope on
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
- **`DisableCcProcessing`.** The stored-method flows refuse an account with it on, with 403
  `CLIENT_CARD_PAYMENT_DISABLED`: `BeginSaveStoredMethod` (saving a method) and
  `ChargeInvoiceStoredMethod` (charging one). The check is
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
- **Mail sent from a message is sent once per recipient.** `SendEmailCommand` takes an optional `IdempotencyKey`.
  Every handler that mails in reaction to a message passes `EmailIdempotencyKey.ForMessage(message, templateSlug)`
  (SHA-256 over the message type, its JSON and the slug); `SendRenewalReminder` passes `ForParts(invoice id,
  threshold)`. Per address (the main recipient and each contact copy) the handler keys the row with
  `ForRecipient(key, address)` and stores it on `EmailLogs.IdempotencyKey` (64 lower-case hex, unique index
  `IX_EmailLogs_IdempotencyKey` filtered on not null) beside `EmailLogs.State` (`EmailDeliveryState`: `Sending`,
  `Sent`, `Failed`; `Success` always equals `State == Sent`). The order is **claim, send, outcome**: the row is
  claimed in `Sending` and saved before the relay is called (`EmailLog.Claim`, or `Reclaim` of a row whose
  `MayResend` allows it), then `MarkDelivered` or `MarkUndelivered` is saved. A `Sent` row is never sent again; a
  `Failed` row is retried; a `Sending` row is trusted to be in flight for `Notifications:DeliveryClaimSeconds`
  (30 to 3600, default 300) and only then reclaimed. Two deliveries racing for one row meet the unique index or the
  row's concurrency token, `AppDbContext` raises `ConcurrencyConflictException`, and the loser skips that address.
  What remains: a crash after the relay accepted the message and before the outcome is saved leaves the row in
  `Sending`, and the next delivery resends it once after the lease. Synchronous mails sent inside a request (email
  change, invitation, verification, password reset) pass no key and are not deduplicated: the person is waiting
  for exactly that mail, a second request is meant to send a fresh one (often with a new token), and no queue
  retries them.
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

### Email campaigns

A campaign (`Campaigns` module, tables `EmailCampaigns`, `EmailCampaignSegments`, `EmailCampaignRecipients`) is a subject, an HTML body and
segments: every active client, owners of a product, clients in a country, clients with a service that renews within 1 to 365 days,
members of a client group (the value is the group id), or clients who have not signed in for 1 to 3650 days.
`CampaignAudience` is the one place a segment becomes a query, so the count an admin sees (`POST /api/admin/email-campaigns/audience-count`)
and the recipients fixed at the start of sending never disagree; it is on the cross-module read allow-list for that reason. A client is
in the audience only when it is active and `MarketingOptIn` is true, and a client matched by several segments counts once. One address is mailed once:
the audience is resolved to sign-in addresses (clients with none are not reached) and clients that share an address, compared without case or
surrounding spaces, make one recipient, the one with the lowest client id; the count is the number of those mails.

- **Segment shapes.** The segment as the admin API sends and answers with it is one DTO, `CampaignSegmentDto` (`Campaigns/Common/`), used by
  the create and update commands, the audience count query, `EmailCampaignDto` and `CampaignAudience`. `CampaignSegmentDefinition` in Domain is
  only the input `EmailCampaign.Create` and `Update` take and check; `CampaignSegmentExtensions` (`Campaigns/Extensions/`) is the one place the
  two meet (`ToDefinitions` going in, `ToDtos` coming out of the stored `EmailCampaignSegment`). Domain never learns the wire shape.
- **Last sign-in.** A client record stores no sign-in time. It lives in the identity store (`IdentityAccount.LastLoginAt`, read through
  `IIdentityProvider`), so the "not signed in for N days" segment is answered by `CampaignAudience` in batches from the identity accounts of the
  opted-in active clients, not in SQL: the last sign-in, or the creation of the client record for one who never signed in, is at least N days old.
  A sign-on provider tracks sign-ins to itself and not to this product, so where the deployment does not own its accounts the time is unknown and
  `EmailCampaignSegmentValidator` refuses the segment with `CampaignSegmentSignInUnavailable` (400); the admin form does not offer it there.

- **Sending.** `DispatchEmailCampaignsCron` (every `ScheduledJobs:DispatchEmailCampaignsIntervalSeconds`, 30 by default) starts the
  scheduled campaigns that are due (`StartEmailCampaign` fixes the recipients, one per mailbox) and sends one batch of pending recipients of every sending campaign (`DeliverEmailCampaign`). "Send now" is a
  schedule with no time. A mail goes through `SendEmailCommand` with the `email-campaign` template (the admin's HTML printed raw above a
  footer with the unsubscribe link, seeded in en, ru and hy at startup) and a per-recipient idempotency key, so a replay never mails twice;
  the outcome is read back from the email log (`GetEmailDelivery`). A refused mail is tried up to three times, then the recipient is Failed.
  The consent is read again right before each send, and a client who withdrew it is skipped as unsubscribed. Just before the mail leaves, the
  recipient is given a random unsubscribe token and only its SHA-256 hash (`UnsubscribeTokenHash`) is saved, so the token exists in the mail alone;
  the email log keeps the body with the link masked (`SecretMembers.Conceal`), and the mail carries a `List-Unsubscribe` header with the page address
  (not `List-Unsubscribe-Post`: one-click needs a bare-POST endpoint, which the portal's CSRF check does not allow). The campaign template is seeded at
  startup only. If a reader unsubscribes while a mail is leaving, the sender's save of that recipient loses (`ConcurrencyConflictException`), the
  rest of the batch is read again by the next pass, and nothing fails.
- **Unsubscribe.** The mail links to the portal page `/unsubscribe?token=...`, which posts the token to the anonymous
  `POST /api/public/unsubscribe` (rate limited with the strict tier). The hash of the token names one recipient row; the client's
  `MarketingOptIn` is set to false (`WithdrawClientMarketingConsent`) so every later campaign skips it, and the recipient is marked
  Unsubscribed. When the sender saved the recipient first, the recipient is read and marked again (three attempts), and the answer is 204 even if
  every attempt loses, because the consent is already withdrawn. Following the link again answers the same 204; a token that names nobody answers 404 `CAMPAIGN_UNSUBSCRIBE_LINK_NOT_FOUND`.
- **Open and click tracking.** The mail built for one recipient (`DeliverEmailCampaign`, through `CampaignTrackingLinks.Track`) carries
  a one-pixel image and every trackable link of the campaign body rewritten to `{ClientBaseUrl}/api/public/campaign-mail/click/{token}/{index}`;
  the token is the recipient's tracking token, a secret of its own (`TrackingTokenHash`, unique, issued by `IssueTrackingToken` beside the
  unsubscribe token in the same pre-send save and under the same pending-only rule), never the unsubscribe token: a pixel address travels
  further (image proxies, logs, forwarded mail) and must not be enough to unsubscribe the reader. The email log masks it in the tracked
  addresses (`EmailCampaignTemplateModel.RecipientToken`). The anonymous `PublicCampaignTrackingController` is held to the global rate
  limit only, not the strict tier, because image proxies of large mail providers fetch from few addresses. It serves `GET .../open/{token}` (a no-store 1x1 GIF, 404
  `CAMPAIGN_MAIL_LINK_NOT_FOUND` for an unknown token) and `GET .../click/{token}/{index}` (302, no-store). **No open redirect:** the
  destination is never read from the request. `RecordEmailCampaignClick` re-reads the stored body of the recipient's campaign (immutable
  once it leaves draft) and takes the link at `index` from `CampaignBodyLinks.Extract`, the same reader the mail was rewritten with, so
  the index always names the link it replaced and only absolute http or https links the admin wrote are ever an answer; mail, tel,
  script, data, relative and scheme-relative links are neither rewritten nor redirected to, and an index past the last link is a 404.
  The recipient counts opens and clicks (`OpenCount`, `FirstOpenedAt`, `ClickCount`, `FirstClickedAt`, written by `RecordOpen` and
  `RecordClick` through `CampaignMailTracker`, which re-reads and repeats a save that loses to the sender or the unsubscribe page).
  `EmailCampaignStatsDto` adds `Opened`, `UniqueOpened`, `Clicked`, `UniqueClicked` and the rates `OpenRate` and `ClickRate` (unique
  readers over the mails delivered, that is recipients with a `SentAt`, from 0 to 1). The pixel counts a mail client that loads images
  on its own (image proxies, prefetch) as an open, so opens are an upper bound.
- **Preview.** `GET /api/admin/email-campaigns/{id}/preview` renders the stored template in the default language with a sample link.

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
- **Demo stored charges.** The demo payment plugin implements `IStoredCredentialPlugin` in memory; the last two digits of the amount in minor units pick the outcome: 02 declined, 03 action required, 04 pending, 05 money taken and then `TimeoutException`, 06 `TimeoutException` before anything is taken, anything else succeeded. The first result per idempotency key is final: a repeat answers it and never throws (a 06 key records nothing, so it throws again). The book is `DemoStoredCredentialState`, a singleton shared by every resolve and cleared by the demo reset.
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

.NET 9 · Wolverine 6.61.0 (WolverineFx.Postgresql, WolverineFx.EntityFrameworkCore) · EF Core 9 + Npgsql 9 · PostgreSQL 17 · ASP.NET Identity (local
mode) + JWT bearer · `Innovayse.Auth` (cookie session for the admin SPA, sso builds only) · SkiaSharp (branding images) ·
Serilog · Scalar + Swashbuckle · Mapster · MailKit · Otp.NET · Fluid.Core ·
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
runs the Domain, Architecture, CWP and Integration suites, plus the migration addon's PHP tests.

```bash
docker compose -f docker/docker-compose.shared.yml -f docker/docker-compose.dev.yml -f docker/docker-compose.platform.yml up   # start the Hostpanel stack inside a platform checkout
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
and exits 0 whether or not the assembly is there. The Domain, Architecture and CWP suites are fine with it.

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
- `scripts/check-duplicate-gateway-payments.sh [container]` — read-only list of the ledger rows that would stop the
  unique gateway payment reference. Every `Transactions` row has a `Source` (`GatewayPayment`, `GatewayRefund`,
  `Manual`, `Import`; `TransactionSource` in Domain, set by the creating path and never changed), and a filtered
  unique index on `PaymentMethod` and `TransactionId` covers the `GatewayPayment` rows only, so manual entries
  and imports may repeat a reference. The `AddTransactionSource` migration backfills `Source` from the rows
  themselves and stops, naming the rows, when two gateway payments share a reference, so this script must be run
  before every production deploy that carries the migration, and any row it lists resolved first. The backfill reads
  only the rows: a staff entry typed under a name that is also a gateway (with a reference and a description a gateway
  path writes) is classified as a gateway row, and counts toward a duplicate. A second gateway payment saved under one reference surfaces as `ConcurrencyConflictException`
  (`AppDbContext` translates the violation), which the settlement handlers already answer as a replay.
- `scripts/pack-cwp-plugin.sh` — publishes `Innovayse.Providers.CWP` (Release, net8.0) on the host, strips
  PDB/XML and framework DLLs, adds `plugin.json` and writes `dist/innovayse-cwp.zip`.
- `scripts/test-install-backup.sh [install.sh]` — runs `install.sh` against a fake `docker` and `curl` (no Docker, no
  network) and checks the upgrade backup (a failed, empty, truncated or table-less dump, a stopped database, mode 600
  and 700, retention that keeps the oldest backup of each version, UTC names) and `--rollback` (confirmation, the
  newest backup of another version, the safety copy, restore before the compose files change, a failed restore, the
  state printed after a failed stop, download, pull or start, a rename blocked by a background worker, the exact
  messages after a half swap, a repeated rollback that moves forward, the early refusal of a database named
  postgres, the bounded per version retention, and that the volume name in the installer is the one in the compose
  file). Needs bash and GNU `stat`. With
  `HOSTPANEL_TEST_POSTGRES=1` it also dumps, restores and restores a corrupt dump against a real postgres container
  (the GitHub `install-backup` job sets it; the GitLab `installer:test:backup` job runs the fake-docker part only).
- `scripts/check-empty-plugins.sh <test project> ...` — fails when `plugins/` or `plugins-installed/` in a
  built test project's output holds anything. Every backend test job of both pipelines runs it between the
  build and the tests, so the whole suite is proven green with no plugin loaded in the jobs that already
  run it, not in a duplicate set. A test that needs a plugin stages it itself and removes it.

All are `*.sh`, which `.gitattributes` (`*.sh text eol=lf`) pins to LF: the ssh pipe above fails with
"Illegal option" on a CRLF copy.

A Held stored charge claim (the gateway took money the invoice could not record) is resolved by an operator through a manual payment or by cancelling the invoice; only a hosted payment start and an automatic charge treat it as blocking.

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
- **The plugin SDK package is not published from here.** `backend/src/Hostpanel.Sdk` is a submodule of
  github.com/innovayse/hostpanel-sdk, whose own workflows build, pack and publish it (a raised `<Version>` on its
  `main`, or a version tag). This repository only builds against the commit the submodule pins. Every checkout
  fetches it: `submodules: true` on each `actions/checkout`, `GIT_SUBMODULE_STRATEGY: recursive` in
  `.gitlab-ci.yml`, and the deploy jobs (`GIT_STRATEGY: none`) run `git submodule sync --recursive` and
  `git submodule update --init --recursive` after every checkout of their deploy directory, the rollback included.
  The SDK project opts out of central package management and pins its two package versions itself, so it
  builds alone; keep those versions equal to the ones in `backend/Directory.Packages.props`.
- `.gitlab-ci.yml` inlines its templates. This repo cannot see the shared templates they
  came from, so the hidden jobs are copied in. Changing one here does not change those.

**`deploy:prod` builds the images on the production host** — it consumes no registry images
and declares `needs: []`, because the build jobs want a `builder` runner this project's prod
host does not have. It dumps the database with `pg_dump -Fc` into `backups/` **before**
anything is rebuilt and fails the deploy if the dump fails: the API applies pending EF
migrations on startup, so `up -d` is the moment the schema changes, and a deploy that cannot be
undone should not start. The last ten dumps are kept.

`nginx` is deliberately left out of both deploys' service lists. It publishes 80/443, which on
these hosts belong to the host's shared reverse proxy; naming it would fail the whole deploy.
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
  Every deployed tier (staging, prod, demo) sets `Production`; the tier
  is named by the job's `environment:`, never by `ASPNETCORE_ENVIRONMENT`.
- **Demo mode (`Demo__Enabled`).** Set through `DEMO_*` env in `docker/docker-compose.demo.yml`, an overlay
  placed last in the chain (`shared` + `prod` + `platform` + `demo`, project `hostpanel-demo`). It swaps the
  payment, registrar and provisioning adapters for fakes, sends mail to the demo's own mailhog and resets
  data every `Demo__ResetIntervalHours`. Everything stateful is `hostpanel-demo-*` so it can sit beside prod; its db, broker, redis and mailhog live on a demo-private network and only the api, client and admin (compose services `hostpanel-demo-*`, the names the edge proxy uses) also join the dedicated external network `hostpanel_demo_edge`, **never** the shared network, so the demo can neither resolve nor reach any production container (the edge proxy is the one production-side container on both networks; `deploy:demo` creates the network when missing, with the **fixed subnet 172.29.0.0/16**, which the operator's edge denies on every host except the two demo ones, so a demo container cannot reach production by sending a production `Host` header; the subnet lives in `deploy:demo`, the edge's configuration and `docker/README.md`, and changes in all three together); `deploy:demo` uses its own `DEPLOY_DIR` (one of the demo deployment's CI variables), never prod's;
  the demo passwords are `:?`-required like any secret. Deployed by the manual `deploy:demo` job; it needs
  `AUTH_MODE=local`. Fake adapters live behind the existing ports, never in a code path guarded by a literal.
- **Settings bind into a sealed `*Options` class naming its own `SectionName`** —
  `BillingOptions`, `NamecheapOptions`, `NameAmOptions`, each
  in an `Options/` folder inside the feature or integration that owns it.

## Storefront content — in the database, not in the code

Testimonials, partners, server locations, projects and FAQs are rows in the database, each with English, Russian and Armenian
translations, managed in the admin under **Storefront content**. The public endpoints are
`/api/public/{testimonials,partners,locations,projects,faqs}`. The hero stats and badge read `portal.stats.*` and
`portal.hero.badge`; the contact channels read `portal.contact.*`; the site name reads `portal.site.name`. The
client hides any section that is empty, so an empty table is a valid state, not an error. Production runs a
one-time import of the previous static content; the demo does not, because its seed is fixed.

## Knowledge base: category by id, slug, text per language

An article belongs to a category by `CategoryId` (a foreign key, `Restrict`: a category that holds articles, or sub-categories,
is refused with `KBCATEGORY_IN_USE`). Its title and body are `KbArticleTranslations` rows, one per language, written like the
FAQ: the default language `SupportedLanguages.Default` (`en`) is required, `en/ru/hy` are the only languages.

- **Which language a reader gets.** The request's language, else the *fallback*, else the first language the article has in
  ordinal order. The fallback is the configured `DefaultLocale` reduced to its two letters (`en-US` is `en`) when that is one of
  `en/ru/hy`, otherwise `en` (`KbLanguage.Fallback`). The requirement to write English and the fallback are two different
  things: the first never depends on `DefaultLocale`. Search looks into every language.
- **Slugs** (`KbArticles.Slug`, `KbCategories.Slug`). Lower case Latin letters and digits joined by single hyphens, at most 150
  characters, unique, and never purely numeric (a number in that place is the id). An article slug is also never `articles` or
  `categories`, the literal segments of the same route. The rules live in `KbSlugRules` and again as check constraints and
  unique indexes, so no writer can store a slug that breaks them. A slug is built once, from the English title or the category
  name, and never changes, so a link to a renamed article keeps working. `SlugGenerator` only builds the candidate;
  `KbSlugAllocator` makes it unique by trying `-2`, `-3` and so on until a value is free, and `KbSlugSaver` is the one place
  that saves a new article or category and asks the allocator again when the unique index refuses the save because another
  request took the value (the create use cases and the importer all go through it). The shape is written for two engines: .NET
  patterns anchor with `\A` and `\z` (its `$` also matches before a trailing newline), the database check constraints use
  `^` and `$` (PostgreSQL's `$` only matches at the end), and a test pins that both refuse `foo\n`.
- **Routes.** `GET /api/knowledgebase/{id:int}` and `GET /api/knowledgebase/{slug}` share one segment. ASP.NET Core matches
  literals and `regex` constraints without regard to case, so `Articles` is the list and `CATEGORIES` the categories; the slug
  route's pattern refuses a purely numeric segment (otherwise a number matches both routes and the request fails as
  ambiguous), and a number too large for an id matches no route. The handler lower cases the slug, so `Foo-Bar` finds
  `foo-bar`, and the answer carries the canonical slug.
- **Data migrated from free text.** `AddKbTranslationsAndSlugs` merges category spellings that differ by case or edge spaces,
  assigns slugs one row at a time in id order (each checked against those already given, so `Foo`, `Foo`, `Foo-2` end as `foo`,
  `foo-2`, `foo-2-2`), and stores each existing text as one translation in the language its own letters show (Armenian `hy`,
  else Cyrillic `ru`, else `en`), because SQL cannot read `DefaultLocale`. Its `Down` is not exact: it restores `Category` as the
  category's name, `Title` and `Content` from the English translation (else the first language), and loses the other languages
  and the slugs.

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
`templates/classic/layout/ClassicHeader.vue`, a two-line wrapper — so on an aurora or nova deployment it is
live code that nothing renders. Editing it and reloading changes nothing, and that has cost
real time. Start from `templates/<active>/layout/<Active>Header.vue` (`AuroraHeader.vue`, `NovaHeader.vue`,
`ClassicHeader.vue`).

`nova` deliberately reuses `aurora`'s `pages/AuroraDomains.vue` and `pages/AuroraCheckout.vue` (see
`templates/registry.ts`) — the checkout is the ordering path and a second copy is a second place for a
payment bug to live.

**Runtime config in a production image.** A production Nitro build freezes `runtimeConfig`
defaults — `nuxt.config.ts` is not re-evaluated at container start as it is in dev. Nitro's own
escape hatch is reading `NUXT_<KEY>` / `NUXT_PUBLIC_<KEY>` at runtime and overriding the frozen
default, so `docker/docker-compose.prod.yml` spells every one of them that way. **The plain names used
in `docker/docker-compose.dev.yml` are silently ignored on a deployed host**, so the value stays
at its build-time default and no `.env` edit can change it.

**The client image has no build args.** The app-launcher `<script src="/widget/header.js">` tag is
added by `plugins/header-widget.ts` when `runtimeConfig.public.mainUrl` is set, so `NUXT_PUBLIC_MAIN_URL`
is plain runtime env. `site.url` is read at runtime by nuxt-site-config from `NUXT_PUBLIC_SITE_URL`
(set in `docker-compose.prod.yml`); the esbuild `drop` depends only on `NODE_ENV`, not on a
deployment. Do not add a `process.env` read outside `runtimeConfig` that differs per deployment.

That widget is a host-provided header widget, written elsewhere, but it is **served from this portal's own origin**:
`server/routes/widget/[file].ts` relays `header.js` and `header.v2.js` from `MAIN_APP_URL` (five minutes' cache, the last copy kept when the host is unreachable, only those two names). The script builds every
address for switching account, adding an account, its configuration and its launcher from the origin it was
served from; loaded from the host's origin it sent the switch through the host's sign-in client and callback, so
the visitor landed on a page that does not exist here and kept the old account. Served from here, those
addresses are this portal's own: `GET /api/portal/auth/sso/switch` and `GET /api/portal/auth/sso/authorize`
(which reads the page to return to as `returnTo` or, as the widget spells it, `redirect`). The three other
addresses the widget asks of its origin are answered by `server/routes/api/portal/public/config.get.ts` (the
sign-on address), `.../apps.get.ts` (the launcher list, read from the host, empty when it is unreachable) and
`server/routes/account.get.ts` (a redirect to the host's account centre). All of them answer 404 unless the
widget is enabled (`isHeaderWidgetEnabled`). The host's address must be HTTPS on an HTTPS
deployment or the browser blocks the script as mixed content and the client dashboard's hydration
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

### The client's shape (Nuxt)

`client/` is a Nuxt app, where `admin/` is a Vite SPA: it has public, crawlable pages
(SEO, SSR) beside the session-gated `/client/**` area. The frontend rules in this folder
([vuejs-style.md](vuejs-style.md), [jsdoc.md](jsdoc.md)) hold in full; the Nuxt specifics
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
- **A route that serves a file answers `GET` and `HEAD`, and only those.** A Nitro route file named `*.get.ts`
  is registered for `GET` alone and h3 does not fall back to it for `HEAD`, so a `HEAD` of `/robots.txt`,
  `/site.webmanifest`, `/widget/*` or `/uploads/**` never reached the handler and answered a missing file where
  there was one (link checkers, uptime monitors and proxy revalidation probe with `HEAD`). A file route outside
  `server/routes/api/` is therefore written without a method suffix and exported through
  `defineReadOnlyEventHandler` (`server/utils/readOnlyEventHandler.ts`): `GET` and `HEAD` run the one handler (the
  server drops the body of a `HEAD`), every other method (`OPTIONS` included, which the page used to answer) is a
  coded 405 (`PortalErrorCode.MethodNotAllowed`) with `Allow: GET, HEAD` before the handler runs. Both file proxies,
  `/uploads/**` and `/uploads/branding/**`, ask the file server for a `HEAD` as a `HEAD`, so answering one never pulls
  the file, and both pass the file server's own `Content-Length` on, so a `HEAD` and a `GET` of an upload tell the same size
  (a length is dropped for an answer the fetch layer decoded, since the body would not match it). A file route's
  refusals keep the BFF's coded-error convention (`createError` with `data.code`, no sentence): the C# `ErrorResponse`
  needs a localised `detail`, which the BFF must not author, and every code the BFF writes itself has one entry in
  `PortalErrorCode`. `tests/server/contract.test.ts` fails on a suffixed or unwrapped file route (`account.get.ts`, a
  redirect, is the one listed exception), and `tests/server/nitroRouter.test.ts` sends real requests through Nitro's own
  route scan and an h3 router to prove a `HEAD` reaches each route (and that a `*.get.ts` file would not).
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
- **Plugin pages in the client area (#645).** A page a serving plugin (#818) adds lives at `/client/apps/<plugin>/<path>`
  (`pages/client/apps/[plugin]/[...page].vue`; `/client/**` is `ssr: false`, so it is read in the browser like every
  client page). The page is found by its address, not by an id the client guesses: the store asks
  `GET /api/portal/client/plugins/<plugin>/page?path=/…` (`server/api/portal/client/plugins/[plugin]/page.get.ts`, the
  address checked by `requirePluginPagePath`, `/` alone being the plugin's root page), which forwards to the API's
  lookup `GET /api/client/plugins/ui/{plugin}/pages/by-path` under the reserved `ui` segment; the API matches the page
  templates and answers the view (with `externalOrigins`) or the one 404 `PLUGIN_PAGE_NOT_FOUND`, whose own sentence
  the page shows. The parameter values are read from the address by the template the API answered
  (`utils/plugins.ts`), and fill the `{param}` segments of the `{ method, path }` route references of the view.
  - **Data flow.** `stores/plugins.ts` (index, page, `callRoute`, `streamRoute`) → `composables/apis/usePluginsApi.ts`
    → `useApi.ts`. The sidebar's plugin links come from the store's index read (`GET /api/client/plugins/ui`, already
    filtered by permission and labelled in `X-Language`), ordered by `order`, a page that takes parameters never
    linked; the layout reads the index again when the reader switches language. The BFF does not compose the index
    into the widget's profile menu (`profile.get.ts` stays one call). A call the page cannot make (an unfilled
    parameter, a method the host does not call) sends nothing and shows nothing: the client writes no sentence for it,
    and the API refuses such a view at install. Each block keeps its own result; an older answer than the latest read
    of a page or a block is dropped.
  - **Calls and streams.** JSON calls go through `server/api/portal/client/plugins/[plugin]/routes/[...path].ts`, a
    stream through `.../stream/[...path].post.ts`; both read the path with `requirePluginPath` (each segment decoded
    once, refused when empty, `.`, `..`, a backslash, a control character or a second encoding, and re-encoded) and
    forward to `ClientPluginCallsController`. The stream is opened with native `fetch` (`internalApiStream`) and handed
    to the browser unbuffered (`X-Accel-Buffering: no`) with the route's own headers only, never an upstream header or
    the token; a 2xx that is not `text/event-stream` is refused with 502 `PLUGIN_STREAM_INVALID` and no sentence, the
    backend call is aborted when the browser leaves, and a browser that left before the answer ends it quietly. The
    browser reads it with `apiStream` (fetch + `ReadableStream`, never `EventSource`, which cannot send the CSRF and
    language headers) and `readPluginEventStream`, which normalises line ends after joining chunks so a split `\r\n`
    stays one line end. Stop, leaving the page and a lost connection end it; a lost connection shows `errors.noAnswer`.
  - **Rendering.** `components/plugin/*` render the schema 1 blocks with the kit; every plugin string is interpolated
    as text, never `v-html`. A link renders only through `pluginLinkTarget`: a host-relative path (one leading `/`,
    never `//`, and never `/api` or below once resolved and decoded once, in any case), or an `https` URL without user
    info on one of the plugin's `externalOrigins`, opened with `noopener noreferrer`; anything else is not rendered.
  - **Workspace, split, preview, chat events (#910, #911).** `layouts/client.vue` drops the gutter and hides the side menu
    on a wide screen while the page on screen declares `workspace`. `SplitBlock` stacks on a phone. `PreviewBlock` frames
    through `UiPreviewFrame`, the one `<iframe>` (see "Layout, split and preview"), and `pluginFrameAddress` admits an
    address. `server/middleware/plugin-frame-csp.ts` writes the document's `frame-src`, read from the page of that address,
    so the document's scope is the exact page: `pluginDocumentLoaded` compares the navigation entry's path with the page on
    screen, and any router move onto a preview, inside one plugin too, loads it once in full (one session flag per page
    keeps it from looping). `ChatBlock` renders `step`, `result` (`UiToneCard`) and `issue` inside the reply, and Stop
    aborts the fetch. A reply's own text goes through `UiMarkdownText` (`utils/ui/markdownText.ts`): the Markdown subset
    becomes known tags through the same node tree and `renderRichNode` as `UiRichText`, never `v-html`, and a Markdown link
    stays text, because an address renders only through `pluginLinkTarget`.
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

### The admin's shape (Vite SPA)

`admin/` is a standard Vite SPA with no exception to the frontend rules ([vuejs-style.md](vuejs-style.md),
[jsdoc.md](jsdoc.md)): the operator back office is session-gated, so it has no crawlable pages. **No part of it is exempt — the ratchet
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
- **Plugin pages and the Add-ons menu (#647).** A page a serving plugin adds to the admin lives at
  `/apps/<plugin>/<path>` (`views/PluginPageView.vue`, route `apps/:plugin/:page*`); the store asks the API's
  lookup by path (`GET /api/admin/plugins/ui/{plugin}/pages/by-path`) and shows its `PLUGIN_PAGE_NOT_FOUND`
  sentence in a not-found state. Data flow: `stores/pluginUi.ts` (index, page, `callRoute`, `streamRoute`, the
  `writes` count the shared reads follow) → `composables/apis/usePluginUiApi.ts` (calls go to
  `/api/admin/plugins/{id}/ext/{path}`) → `useApi` (`request`, and `stream`, the one server-sent event reader,
  `fetch` with the CSRF and language headers, never `EventSource`). `components/plugins/view/` renders the
  schema 1 blocks with the admin kit, mirroring the client's design (header, text, stat, table, form, action,
  link, badge, empty, alert, tabs, chat with steps, cards and issues, progress, split), with `visibleWhen`,
  `resetOnSuccess`, `followUp` and the shared reads of `composables/usePluginView.ts`; `preview` and an
  action's `bodySource` are a client page's alone (refused on an admin page at install) and draw nothing. Pure
  rules live in `lib/pluginView.ts`: the path filling, the link rule (`pluginLinkTarget`: host-relative, never
  `/api`, or https on the page's `externalOrigins`), the chat event readers and the follow-up bounds (the host's).
  A reply's Markdown goes through `UiMarkdown` with `links` off, so an address renders only through the link
  rule. The sidebar adds an Add-ons section before System from the index's menu items (`withAddons` in
  `lib/navigation.ts`, a plugin entry carries its API label instead of an i18n key and a built-in icon by name),
  hidden while no plugin declares one, and reads the index again on a language switch.
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

`backend/tests/` has four projects: `Domain.Tests`, `Integration.Tests`, `Architecture.Tests`,
`Innovayse.CWP.Tests`. **There is no `Application.Tests` or
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
  `Infrastructure/ImageProcessing/BrandingImageProcessor.cs`, behind the
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

- **No card or bank provider ships in the host.** Card and bank gateways are external payment plugins; the host carries
  no gateway SDK, options, catalogue entry or built-in module. The default Content-Security-Policy names no gateway
  origin: a plugin whose checkout loads a script or calls an API from another origin has that origin added by the
  operator through `EDGE_CLIENT_CSP_SOURCES` (the manifest's `externalOrigins` only feeds link rendering, not the policy). There is no on-site card gateway port any more: card
  payments start through the redirect flow (`StartGatewayPayment` and `StartMyGatewayPayment`, then the matching complete
  use case), and methods stored for later charges go through `IStoredCredentialPlugin` (the `api/me/stored-payment-methods`
  endpoints). The embedded flow (`create-payment-intent`, `confirm-payment`, `POST api/me/invoices/{id}/pay`,
  `api/me/payment-methods`) is gone; a ledger row recorded as `Card` with no hosted session behind it is refunded by a
  manual refund (`INVOICE_NOT_GATEWAY_PAID` for a Gateway refund). The plugin's stored `integration:<slug>:*` settings and
  the payment rows are untouched and read by the plugin under the same slug. Manual bank transfer is built into the host (module `bank_transfer`, settings under the `bank-transfer` integration) while bank gateways are plugins.
- **Vendor names that remain, on purpose.** Integration slugs (`cwp`, ...) in
  `IntegrationSlugs` / `BuiltInPaymentModules` and the `Integrations/<Vendor>/` adapter folders are
  plugin identifiers: they are stored in settings rows, sent in URLs (`/api/admin/integrations/cwp`)
  and chosen in the admin UI, so renaming them would break stored configuration. They are the only
  place a vendor is named. Persisted *data* is neutral: the `GatewayCustomers` rows and the ledger
  payment method `Card` (the `ConformanceFixes` migration renamed the column and rewrote old
  old card-gateway ledger rows).
- **`ClientRegisteredIntegrationEvent` carries five parameters nothing fills.**
  `RegisterHandler` publishes four arguments and stops, so `IpAddress`,
  `UserAgent`, `DeviceType`, `OperatingSystem` and `Browser` arrive null and `Client.Create`
  writes five mapped, migrated columns from them. Finish it by capturing all five from the
  request, not by trimming the record. Wolverine runs with no broker, so the event never leaves
  the process; it is persisted in the `wolverine` schema like every other queued message (see "Durable
  messaging").
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
