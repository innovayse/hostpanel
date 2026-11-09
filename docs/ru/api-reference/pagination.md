---
title: Постраничная выдача
description: "Как списочные конечные точки Hostpanel возвращают данные по одной странице: параметры запроса Page и PageSize и тело ответа PaginatedList."
keywords: "постраничная выдача api, page, pagesize, paginatedlist"
---

# Постраничная выдача {#pagination}

Списочные конечные точки, которые могут вернуть много строк, отвечают по одной странице. Страницы конечных точек это отмечают: среди их параметров запроса есть `Page` и `PageSize`, а тело ответа — `PaginatedList`.

## Запрос {#request}

<SettingTable :settings="[
  { name: 'Page', type: 'number', default: 'Нет', description: 'Номер читаемой страницы, отсчёт с 1. Имя сопоставляется без учёта регистра, поэтому page тоже работает. В большинстве списков значение меньше 1 отклоняется ошибкой валидации.' },
  { name: 'PageSize', type: 'number', default: 'Нет', description: 'Сколько элементов помещается на одной странице. Наибольший размер зависит от списка: 100 для обычного списка, 200 для отчёта. В большинстве списков размер вне диапазона от 1 до наибольшего отклоняется.' },
]" />

Передавайте их в строке запроса. Этот запрос читает вторую страницу по 25 клиентов:

```
GET /api/clients?Page=2&PageSize=25
```

<Callout type="note">
В запросе имена <code>Page</code> и <code>PageSize</code>, но в ответе страница называется <code>pageNumber</code>. Запрос с параметром <code>pageNumber</code> не читает эту страницу: имя не распознаётся, поэтому вызов обрабатывается так, будто страница не указана.
</Callout>

В большинстве списков значения вне диапазона отклоняются, а не исправляются. Запрос `GET /api/clients?Page=0&PageSize=500` отвечает `400` с `VALIDATION_ERROR`:

```json
{
  "type": "VALIDATION_ERROR",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "page": ["Page must be 1 or greater."],
    "pageSize": ["Page size must be between 1 and 100."]
  }
}
```

Несколько списков, среди них список пользователей, исправляют значения: размер больше наибольшего обрезается до него, а страница меньше 1 читается как 1. Страница конечной точки не говорит, к какому виду относится список, поэтому проверяйте значения, которые пришли в ответе.

Страница после последней не считается ошибкой: ответ `200` содержит пустой список `items`.

## Ответ {#response}

Конечная точка с постраничной выдачей отвечает объектом `PaginatedList`:

<SettingTable :settings="[
  { name: 'items', type: 'array', default: 'Нет', description: 'Элементы этой страницы.' },
  { name: 'pageNumber', type: 'number', default: 'Нет', description: 'Номер этой страницы, отсчёт с 1.' },
  { name: 'pageSize', type: 'number', default: 'Нет', description: 'Применённый размер страницы.' },
  { name: 'totalPages', type: 'number', default: 'Нет', description: 'Сколько страниц во всём результате.' },
  { name: 'totalCount', type: 'number', default: 'Нет', description: 'Сколько элементов во всём результате.' },
  { name: 'hasPreviousPage', type: 'boolean', default: 'Нет', description: 'True, когда pageNumber больше 1.' },
  { name: 'hasNextPage', type: 'boolean', default: 'Нет', description: 'True, когда pageNumber меньше totalPages.' },
  { name: 'metadata', type: 'object или null', default: 'Нет', description: 'Дополнительные данные обо всём списке для тех конечных точек, у которых они есть. В остальных случаях Null.' },
]" />

```json
{
  "items": [
    { "id": 8, "email": "customer5@example.test" },
    { "id": 7, "email": "customer4@example.test" }
  ],
  "pageNumber": 1,
  "pageSize": 2,
  "totalPages": 10,
  "totalCount": 20,
  "hasPreviousPage": false,
  "hasNextPage": true,
  "metadata": null
}
```

Чтобы прочитать все страницы, следуйте за `hasNextPage`, пока он не станет false.

Некоторые списочные конечные точки возвращают простой массив без постраничной выдачи, например счета клиента. Страница такой конечной точки показывает массив как тело ответа.
