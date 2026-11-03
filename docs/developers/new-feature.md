---
title: Where a new feature goes
description: The file for every kind of change and a worked example.
---

# Where a new feature goes

## The table

| What | Where |
|------|-------|
| Controller action | `Innovayse.Hostpanel.API/Controllers/<Feature>Controller.cs` |
| Command | `Innovayse.Hostpanel.Application/<Feature>/Commands/<UseCase>/<UseCase>Command.cs` |
| Query | `Innovayse.Hostpanel.Application/<Feature>/Queries/<UseCase>/<UseCase>Query.cs` |
| Handler | `Innovayse.Hostpanel.Application/<Feature>/{Commands,Queries}/<UseCase>/<UseCase>Handler.cs` |
| Validator | `Innovayse.Hostpanel.Application/<Feature>/{Commands,Queries}/<UseCase>/<UseCase>Validator.cs` |
| Shared DTO | `Innovayse.Hostpanel.Application/<Feature>/Common/<Name>Dto.cs` |
| Port | `Innovayse.Hostpanel.Application/Common/Interfaces/I<Port>.cs` |
| Error code | `Innovayse.Hostpanel.Application/Common/HostpanelErrorCodes.cs` |
| Status mapping | `Shared/Controllers/StatusMapping.cs` |
| Error sentence (en) | `Innovayse.Hostpanel.Infrastructure/Localisation/ErrorMessages.resx` |
| Error sentence (ru) | `Innovayse.Hostpanel.Infrastructure/Localisation/ErrorMessages.ru.resx` |
| Error sentence (hy) | `Innovayse.Hostpanel.Infrastructure/Localisation/ErrorMessages.hy.resx` |
| Entity | `Innovayse.Hostpanel.Domain/<Feature>/<Entity>.cs` |
| Entity configuration | `Innovayse.Hostpanel.Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` |
| Migration | `Innovayse.Hostpanel.Infrastructure/Persistence/Migrations/` |
| Adapter | `Innovayse.Hostpanel.Infrastructure/<Capability>/<Adapter>.cs` |
| Integration test | `Innovayse.Hostpanel.Integration.Tests/<Feature>/<UseCase>Tests.cs` |
| Admin screen | `admin/src/views/<area>/<ScreenName>View.vue` |
| Admin store | `admin/src/stores/<area>.ts` |
| Admin API composable | `admin/src/composables/apis/use<Area>Api.ts` |
| Admin end-to-end test | `admin/src/e2e/<area>.spec.ts` |
| Client page | `client/pages/<area>/<page>.vue` |
| Client store | `client/stores/<area>.ts` |
| Client API composable | `client/composables/use<Area>Api.ts` |
| Portal server route | `client/server/api/portal/<area>/` |

## A worked example

This example adds a read-only admin list screen for a new "Labels" feature.

**1. Query, handler and validator**

Create `Innovayse.Hostpanel.Application/Labels/Queries/GetLabels/GetLabelsQuery.cs`, `GetLabelsHandler.cs` and `GetLabelsValidator.cs`. The handler takes `IHostpanelDbContext`, reads the rows and returns a `Result<GetLabelsResponse>`.

**2. Controller action**

Add a `GET /api/admin/labels` action to `Innovayse.Hostpanel.API/Controllers/LabelsController.cs` (create it if it does not exist). The action creates a `GetLabelsQuery`, calls `await Mediator.Send(query, ct)` and passes the result to `HandleResult`.

**3. Integration test**

Create `Innovayse.Hostpanel.Integration.Tests/Labels/GetLabelsTests.cs`. The test seeds the database, calls the endpoint and asserts the response shape. `UseCaseShapeTests` will fail until this file exists.

**4. Admin store and API composable**

Add `admin/src/stores/labels.ts` with a `fetchLabels` action that calls `runAction`. Add `admin/src/composables/apis/useLabelsApi.ts` with a `getLabels` function that calls `useApi`.

**5. Admin screen**

Create `admin/src/views/labels/LabelsView.vue`. The view calls the store action on mount and renders the list. No direct `useApi` calls in the view.

**6. Run the checks**

```bash
# backend
docker compose exec hostpanel-api dotnet test

# admin
cd admin && npm run lint && npm run typecheck && npm run test
```
