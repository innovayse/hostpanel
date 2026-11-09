---
title: Бэкенд
description: Как запрос проходит от контроллера к обработчику и обратно.
---

# Бэкенд {#backend}

## Путь запроса {#the-request-flow}

```
HTTP request
  └── Controller (extends BaseApiController)
        └── ValidationPolicy  (invalid: 400 VALIDATION_ERROR)
              └── Handler
                    └── HandleResult + StatusMapping
                          ├── success: 200 / 201 / 204
                          └── error:   ErrorResponse body
```

Каждый HTTP-запрос проходит один и тот же путь:

1. Контроллер, наследующий `BaseApiController`, получает запрос и отправляет сообщение (команду или запрос) в шину сообщений.
2. `ValidationPolicy` перехватывает сообщение, проверяет его и при ошибке проверки возвращает ответ `400 VALIDATION_ERROR`. При ошибке проверки обработчик не запускается.
3. Обработчик выполняется и возвращает `Result`.
4. `HandleResult` в `BaseApiController` разбирает `Result`, вызывает `StatusMapping`, чтобы выбрать HTTP-код статуса, и записывает ответ. При ошибке тело ответа это `ErrorResponse`.

## Команды и запросы {#commands-and-queries}

Сценарии использования лежат в `Innovayse.Hostpanel.Application/<Feature>/`. У каждого сценария своя подпапка:

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

DTO, общие для двух сценариев одной функции, кладутся один раз в `<Feature>/Common/`.

Обработчики находятся автоматически. Никогда не регистрируйте и не вызывайте их вручную. Сообщения не несут идентичности; обработчики получают текущего пользователя из `ICurrentUserService`.

## Доступ к данным {#data-access}

Репозиториев нет. Обработчики напрямую принимают `IHostpanelDbContext` как единицу работы.

Конфигурации сущностей лежат в `Infrastructure/Persistence/Configurations/`. Имена всех таблиц, столбцов, ключей, индексов и ограничений должны быть в PascalCase. `DatabaseIdentifierNamingTests` проваливает сборку, если какой-либо идентификатор отклоняется от этого правила.

Каждое изменение модели требует миграции. `ModelDiffBaselineTests` падает, если модель и последняя миграция расходятся.

## Порты и адаптеры {#ports-and-adapters}

Порт это интерфейс `I<Port>`. Все порты лежат в `Application/Common/Interfaces/` и больше нигде. Это проверяет `PortPlacementTests`.

Адаптеры реализуют порты и лежат в `Infrastructure/<Capability>/`. Они регистрируются в трёх файлах `DependencyInjection.cs` (по одному на слой) и в файлах API `Extensions/*Registration.cs`. Регистрация идёт по типу, никогда по имени.

## Коды ошибок {#error-codes}

Коды ошибок это строковые константы, определённые в `HostpanelErrorCodes.cs` (для конкретных функций) и `SharedErrorCodes.cs` (сквозные).

Каждому коду нужны три вещи:
1. Предложение на английском в `ErrorMessages.resx` (варианты `ru` и `hy` лежат в соседних файлах).
2. HTTP-статус в `StatusMapping.cs`.
3. Больше ничего. `ResourceCoverageTests` проваливает сборку, если у кода нет предложения.

API всегда возвращает ошибки в теле `ErrorResponse`.

## Фоновые задачи {#background-jobs}

Периодические задачи перепланируют себя сами: каждый запуск ставит в очередь следующий до своего завершения. Распределённая аренда (lease) гарантирует, что во всём кластере работает ровно один экземпляр каждой задачи. Интервалы и таймауты задач настраиваются в разделе опций `ScheduledJobs`.

## Стиль кода {#code-style}

Все публичные члены в `backend/src/` требуют XML-комментариев документации. Отсутствие документации это сбой сборки, а не замечание на ревью. Смотрите `rules/xml-docs.md` и `rules/csharp-style.md`.
