---
title: Тесты и проверки
description: Все проверки, которые запускает CI, и как запускать их локально.
---

# Тесты и проверки {#tests-and-checks}

## Что запускает CI {#what-ci-runs}

Каждый pull request должен пройти:

1. Тесты бэкенда: доменные, интеграционные, архитектурные и тесты плагинов
2. Панель администратора: линтер, проверка типов, модульные тесты, сквозные тесты
3. Клиентский портал: линтер, проверка типов, модульные тесты

## Тесты бэкенда {#backend-tests}

Запустите все тесты бэкенда внутри контейнера API (из каталога `docker/`):

```bash
./scripts/manage.sh test
```

Пять тестовых проектов:

**`Innovayse.Hostpanel.Domain.Tests`**: модульные тесты доменной логики.

**`Innovayse.Hostpanel.Integration.Tests`**: один файл на сценарий использования по пути `Integration.Tests/<Feature>/<UseCase>Tests.cs`. `UseCaseShapeTests` падает, если у обработчика сценария нет соответствующего файла с тестами. Этот набор сам запускает свой контейнер с базой данных, поэтому внешняя база не нужна. CI делит его на шарды по параллельным раннерам; если вы добавляете новую папку функции верхнего уровня, добавьте её в один из шардов в `scripts/check-integration-shards.sh`.

**`Innovayse.Hostpanel.Architecture.Tests`**: проверяет структурные правила:

| Тестовый класс | Что проверяет |
|------------|---------------|
| `PortPlacementTests` | Все интерфейсы `I<Port>` лежат в `Application/Common/Interfaces/` |
| `DatabaseIdentifierNamingTests` | Все имена таблиц, столбцов и ключей записаны в PascalCase |
| `UseCaseShapeTests` | У каждого обработчика есть соответствующий файл интеграционных тестов |
| `ResourceCoverageTests` | У каждого кода ошибки есть предложение в `ErrorMessages.resx` |

`ModelDiffBaselineTests` лежит в `Integration.Tests/Persistence` и проверяет, что модель EF совпадает с последней миграцией.

**`Innovayse.CWP.Tests`**, **`Innovayse.Inecobank.Tests`**: тесты встроенных плагинов.

## Панель администратора {#admin-panel}

Запускайте из каталога `admin/`:

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

## Клиентский портал {#client-portal}

Запускайте из каталога `client/`. На ноутбуке ограничьте число воркеров тестов до 2, чтобы не перегружать память.

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

Скрипт `lint` включает проверку слоёв (`client/scripts/check-layers.mjs`).

## Перед открытием pull request {#before-opening-a-pull-request}

- Создавайте ветку от `develop`, а не от другой ветки функции.
- Запустите форматтер бэкенда. Точная команда указана в `rules/dotnet-format.md`.
- Добавляйте тесты для каждого изменения в том же pull request.
- Следуйте стилю сообщений коммитов из `rules/project.md`.
- Полный чек-лист pull request смотрите в разделе [Участие в разработке](/ru/developers/contributing).
