---
title: Backend
description: How a request travels from controller to handler and back.
---

# Backend

## The request flow

```
HTTP request
  └── Controller (extends BaseApiController)
        └── ValidationPolicy  (invalid: 400 VALIDATION_ERROR)
              └── Handler
                    └── HandleResult + StatusMapping
                          ├── success: 200 / 201 / 204
                          └── error:   ErrorResponse body
```

Every HTTP request goes through the same path:

1. A controller that extends `BaseApiController` receives the request and sends a message (command or query) on the message bus.
2. `ValidationPolicy` intercepts the message, validates it, and returns a `400 VALIDATION_ERROR` response if validation fails. The handler never runs on a validation failure.
3. The handler runs and returns a `Result`.
4. `HandleResult` on `BaseApiController` inspects the `Result`, calls `StatusMapping` to choose the HTTP status code, and writes the response. On an error the body is an `ErrorResponse`.

## Commands and queries

Use cases live under `Innovayse.Hostpanel.Application/<Feature>/`. Each use case has its own subfolder:

```
Application/<Feature>/Commands/<UseCase>/
  <UseCase>Command.cs
  <UseCase>Handler.cs
  <UseCase>Validator.cs

Application/<Feature>/Queries/<UseCase>/
  <UseCase>Query.cs
  <UseCase>Handler.cs
  <UseCase>Validator.cs
```

DTOs shared between two use cases in the same feature go once in `<Feature>/Common/`.

Handlers are discovered automatically. Never register or call one by hand. Messages carry no identity; handlers read the current user from `ICurrentUserService`.

## Data access

There are no repositories. Handlers take `IHostpanelDbContext` directly as the unit of work.

Entity configurations live in `Infrastructure/Persistence/Configurations/`. Every table, column, key, index and constraint name must be PascalCase. `DatabaseIdentifierNamingTests` fails the build if any identifier deviates.

Every model change requires a migration. `ModelDiffBaselineTests` fails if the model and the latest migration are out of sync.

## Ports and adapters

A port is an `I<Port>` interface. All ports live in `Application/Common/Interfaces/`. Nowhere else. `PortPlacementTests` enforces this.

Adapters implement ports and live in `Infrastructure/<Capability>/`. They are registered in the three `DependencyInjection.cs` files (one per layer) and the API `Extensions/*Registration.cs` files. Registration is by type, never by name.

## Error codes

Error codes are string constants defined in `HostpanelErrorCodes.cs` (feature-specific) and `SharedErrorCodes.cs` (cross-cutting).

Each code needs three things:
1. An English sentence in `ErrorMessages.resx` (with `ru` and `hy` variants in sibling files).
2. An HTTP status in `StatusMapping.cs`.
3. Nothing else. `ResourceCoverageTests` fails the build when a code has no sentence.

The API always returns errors as an `ErrorResponse` body.

## Background jobs

Recurring jobs reschedule themselves: each run queues the next one before it finishes. A distributed lease keeps exactly one instance of each job running across the cluster. Job intervals and timeouts are configured under the `ScheduledJobs` options section.

## Code style

All public members in `backend/src/` require XML doc comments. Missing docs are a build failure, not a review comment. See `rules/xml-docs.md` and `rules/csharp-style.md`.
