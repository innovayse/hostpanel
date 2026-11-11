---
title: Куда добавлять новую функцию
description: Файл для каждого вида изменений и пример по шагам.
---

# Куда добавлять новую функцию {#where-a-new-feature-goes}

## Таблица {#the-table}

| Что | Где |
|------|-------|
| Действие контроллера | `Innovayse.Hostpanel.API/Controllers/<Feature>Controller.cs` |
| Команда | `Innovayse.Hostpanel.Application/<Feature>/Commands/<UseCase>/<UseCase>Command.cs` |
| Запрос | `Innovayse.Hostpanel.Application/<Feature>/Queries/<UseCase>/<UseCase>Query.cs` |
| Обработчик | `Innovayse.Hostpanel.Application/<Feature>/{Commands,Queries}/<UseCase>/<UseCase>Handler.cs` |
| Валидатор | `Innovayse.Hostpanel.Application/<Feature>/{Commands,Queries}/<UseCase>/<UseCase>Validator.cs` |
| Общий DTO | `Innovayse.Hostpanel.Application/<Feature>/Common/<Name>Dto.cs` |
| Порт | `Innovayse.Hostpanel.Application/Common/Interfaces/I<Port>.cs` |
| Код ошибки | `Innovayse.Hostpanel.Application/Common/Errors/HostpanelErrorCodes.cs` |
| Сопоставление статусов | `Shared/Controllers/StatusMapping.cs` |
| Предложение об ошибке (en) | `Innovayse.Hostpanel.Application/Resources/ErrorMessages.resx` |
| Предложение об ошибке (ru) | `Innovayse.Hostpanel.Application/Resources/ErrorMessages.ru.resx` |
| Предложение об ошибке (hy) | `Innovayse.Hostpanel.Application/Resources/ErrorMessages.hy.resx` |
| Сущность | `Innovayse.Hostpanel.Domain/<Feature>/<Entity>.cs` |
| Конфигурация сущности | `Innovayse.Hostpanel.Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` |
| Миграция | `Innovayse.Hostpanel.Infrastructure/Persistence/Migrations/` |
| Адаптер | `Innovayse.Hostpanel.Infrastructure/<Capability>/<Adapter>.cs` |
| Интеграционный тест | `Innovayse.Hostpanel.Integration.Tests/<Feature>/<UseCase>Tests.cs` |
| Экран панели администратора | `admin/src/views/<area>/<ScreenName>View.vue` |
| Хранилище панели администратора | `admin/src/stores/<area>.ts` |
| API-композабл панели администратора | `admin/src/composables/apis/use<Area>Api.ts` |
| Сквозной тест панели администратора | `admin/e2e/<area>.spec.ts` |
| Страница клиентского портала | `client/pages/<area>/<page>.vue` |
| Хранилище клиентского портала | `client/stores/<area>.ts` |
| API-композабл клиентского портала | `client/composables/use<Area>Api.ts` |
| Серверный маршрут портала | `client/server/api/portal/<area>/` |

## Пример по шагам {#a-worked-example}

В этом примере добавляется экран со списком только для чтения в панели администратора для новой функции «Labels».

**1. Запрос, обработчик и валидатор**

Создайте `Innovayse.Hostpanel.Application/Labels/Queries/GetLabels/GetLabelsQuery.cs`, `GetLabelsHandler.cs` и `GetLabelsValidator.cs`. Обработчик принимает `IHostpanelDbContext`, читает строки и возвращает `Result<IReadOnlyList<LabelDto>>`. DTO `LabelDto` лежит в `Labels/Common/LabelDto.cs`; обёрток вида `*Response` нет.

**2. Действие контроллера**

Добавьте действие `GET /api/admin/labels` в `Innovayse.Hostpanel.API/Controllers/LabelsController.cs` (создайте файл, если его нет). Действие создаёт `GetLabelsQuery`, вызывает `await _bus.InvokeAsync<Result<IReadOnlyList<LabelDto>>>(query, ct)` и передаёт результат в `HandleResult`.

**3. Интеграционный тест**

Создайте `Innovayse.Hostpanel.Integration.Tests/Labels/GetLabelsTests.cs`. Тест наполняет базу данными, вызывает конечную точку и проверяет форму ответа. Пока этого файла нет, `UseCaseShapeTests` будет падать.

**4. Хранилище и API-композабл панели администратора**

Добавьте `admin/src/stores/labels.ts` с действием `fetchLabels`, которое вызывает `runAction`. Добавьте `admin/src/composables/apis/useLabelsApi.ts` с функцией `getLabels`, которая вызывает `useApi`.

**5. Экран панели администратора**

Создайте `admin/src/views/labels/LabelsView.vue`. Представление вызывает действие хранилища при монтировании и отображает список. Прямых вызовов `useApi` во view нет.

**6. Запустите проверки**

```bash
# backend (from docker/)
./scripts/manage.sh test

# admin
cd admin && npm run lint && npm run typecheck && npm test -- --maxWorkers=2
```
