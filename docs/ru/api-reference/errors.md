---
title: Ошибки
description: "Любой сбой API Hostpanel возвращает одно тело application/problem+json. Тело ошибки, коды ошибок и заголовок Retry-After при ответе 503."
keywords: "ошибки api hostpanel, problem+json, коды ошибок, retry-after, 503"
---

# Ошибки {#errors}

Любой сбой, от пропущенного поля до сбоя сервера, возвращает одно и то же тело, которое отправляется как `application/problem+json`.

## Тело ошибки {#the-error-body}

<SettingTable :settings="[
  { name: 'type', type: 'string', default: 'Нет', description: 'Код ошибки, например VALIDATION_ERROR или INVOICE_NOT_FOUND. Коды стабильны: от них зависят панель администратора и клиентский портал, и существующий код никогда не переименовывается. Ориентируйтесь на это поле, а не на detail.' },
  { name: 'title', type: 'string', default: 'Нет', description: 'Короткий заголовок, например Not Found. У большинства ошибок он на английском независимо от языка запроса.' },
  { name: 'status', type: 'number', default: 'Нет', description: 'HTTP-статус ответа, повторённый в теле.' },
  { name: 'detail', type: 'string', default: 'Нет', description: 'Предложение о том, что пошло не так, на языке из заголовка X-Language. См. раздел Языки.' },
  { name: 'errors', type: 'object или null', default: 'Нет', description: 'Null, кроме ошибки валидации: тогда это соответствие имени каждого поля в camelCase и предложений о том, что с ним не так.' },
]" />

**Ошибка валидации.** Запрос отклонён до выполнения, а `errors` указывает, какие поля неверны.

```json
{
  "type": "VALIDATION_ERROR",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "email": ["An email address is required."],
    "password": ["A password is required."]
  }
}
```

**Отказ по бизнес-правилу.** Запрос составлен верно, но ответ отрицательный. Код называет причину.

```json
{
  "type": "INVOICE_NOT_FOUND",
  "title": "Not Found",
  "status": 404,
  "detail": "No such invoice.",
  "errors": null
}
```

## Коды ошибок {#error-codes}

<!--@include: @/en/api-reference/_partials/error-codes.md-->

## 503 и Retry-After {#_503-and-retry-after}

Каждый ответ `503 Service Unavailable` содержит заголовок `Retry-After` с числом секунд, которое нужно подождать перед повторной попыткой. По умолчанию это 30 секунд. Ответ `429` тоже его содержит, см. [Ограничения частоты запросов](/ru/api-reference/rate-limits).

<Callout type="warning">
Соблюдайте <code>Retry-After</code>. Повторная попытка сразу же, при 503 или 429, только добавляет нагрузку на сервис, который просит вас подождать.
</Callout>
