---
title: Tests and checks
description: Every check CI runs and how to run them locally.
---

# Tests and checks

## What CI runs

Every pull request must pass:

1. Backend tests: domain, integration, architecture and plugin
2. Admin panel: lint, typecheck, unit tests, end-to-end tests
3. Client portal: lint, typecheck, unit tests

## Backend tests

Run all backend tests inside the API container (from the `docker/` directory):

```bash
./scripts/manage.sh test
```

Five test projects:

**`Innovayse.Hostpanel.Domain.Tests`**: unit tests for domain logic.

**`Innovayse.Hostpanel.Integration.Tests`**: one file per use case at `Integration.Tests/<Feature>/<UseCase>Tests.cs`. `UseCaseShapeTests` fails if a use case handler has no matching test file. This suite starts its own database container, so no external database is needed. CI shards it across parallel runners; if you add a new top-level feature folder, add it to a shard in `scripts/check-integration-shards.sh`.

**`Innovayse.Hostpanel.Architecture.Tests`**: enforces structural rules:

| Test class | What it checks |
|------------|---------------|
| `PortPlacementTests` | All `I<Port>` interfaces are in `Application/Common/Interfaces/` |
| `DatabaseIdentifierNamingTests` | All table, column and key names are PascalCase |
| `UseCaseShapeTests` | Every handler has a matching integration test file |
| `ResourceCoverageTests` | Every error code has a sentence in `ErrorMessages.resx` |

`ModelDiffBaselineTests` lives in `Integration.Tests/Persistence` and checks that the EF model matches the latest migration.

**`Innovayse.CWP.Tests`**, **`Innovayse.Inecobank.Tests`**: tests for the bundled plugins.

## Admin panel

Run from the `admin/` directory:

::: code-group

```bash [lint]
npm run lint
```

```bash [lint:i18n]
npm run lint:i18n
```

```bash [typecheck]
npm run typecheck
```

```bash [test]
npm test -- --maxWorkers=2
```

```bash [e2e]
npm run e2e
```

:::

## Client portal

Run from the `client/` directory. On a laptop, limit test workers to 2 to avoid memory pressure.

::: code-group

```bash [lint]
yarn lint
```

```bash [lint:i18n]
yarn lint:i18n
```

```bash [typecheck]
yarn typecheck
```

```bash [test]
yarn test --maxWorkers=2
```

```bash [e2e]
yarn e2e
```

:::

The `lint` script includes the layer check (`client/scripts/check-layers.mjs`).

## Before opening a pull request

- Branch from `develop`, not from another feature branch.
- Run the backend formatter. See `rules/dotnet-format.md` for the exact command.
- Include tests for every change in the same pull request.
- Follow the commit message style in `rules/project.md`.
- See [Contributing](./contributing) for the full pull request checklist.
