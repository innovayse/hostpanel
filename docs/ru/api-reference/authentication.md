---
title: Аутентификация
description: "Вход в REST API Hostpanel и передача токена доступа или cookie сессии. Двухфакторный вход, срок жизни токена и режим внешнего входа."
keywords: "аутентификация api hostpanel, bearer-токен, cookie сессии, двухфакторный вход, rest api"
---

# Аутентификация {#authentication}

Вход даёт сразу две вещи: cookie сессии для браузеров и токен доступа в теле ответа для всего остального. Передавайте любое из двух в вызовах, которым это нужно. Скрипты и другие серверы используют токен.

<Callout type="note">
Долгоживущих токенов API пока нет. Каждый токен выдаётся при входе, действует 15 минут и продлевается, только пока вы работаете. Когда сессия достигает максимального возраста, скрипт входит заново.
</Callout>

## Вход {#sign-in}

<Label type="local" /> Только локальные учётные записи: при внешнем сервисе входа эти маршруты отвечают 404, см. [Режим входа](#sign-on-mode).

```
POST /api/auth/login
```

Отправьте адрес электронной почты и пароль. Ответ `200` содержит токен, и в том же ответе устанавливается cookie сессии:

<SettingTable :settings="[
  { name: 'email', type: 'string', default: 'Нет', description: 'Адрес электронной почты учётной записи.' },
  { name: 'password', type: 'string', default: 'Нет', description: 'Пароль учётной записи.' },
]" />

В ответе приходит токен и срок его жизни:

<SettingTable :settings="[
  { name: 'accessToken', type: 'string', default: 'Нет', description: 'Подписанный токен. Передавайте его в заголовке Authorization.' },
  { name: 'expiresIn', type: 'number', default: 'Нет', description: 'Срок жизни токена в секундах. 900, то есть 15 минут.' },
]" />

Неверные учётные данные дают `SIGN_IN_UNAUTHORIZED` (401). После слишком большого числа неверных паролей подряд учётная запись блокируется и отвечает `SIGN_IN_ACCOUNT_LOCKED` (423), см. [Ограничения частоты запросов](/ru/api-reference/rate-limits#sign-in-lockout).

## Bearer-токен {#bearer-token}

Передавайте токен в заголовке `Authorization`:

```
Authorization: Bearer <token>
```

Токен действует 15 минут. Пока вы продолжаете вызовы, API выдаёт свежий токен: в последней трети срока жизни успешный ответ содержит новый токен в заголовке `X-Renewed-Access-Token`. Используйте его в следующих вызовах. Сессия никогда не продлевается дальше максимального возраста, который считается от входа, после этого нужно войти заново. Установка задаёт этот возраст через `SESSION_MAX_MINUTES`, по умолчанию 720 минут.

<SettingTable :settings="[
  { name: 'Authorization', type: 'заголовок запроса', default: 'Нет', description: 'Bearer и следом токен доступа. Если заголовок есть, cookie игнорируется.' },
  { name: 'X-Renewed-Access-Token', type: 'заголовок ответа', default: 'Нет', description: 'Свежий токен доступа. Приходит, когда использованный вами токен в последней трети срока жизни и вызов прошёл успешно.' },
]" />

## Cookie сессии {#session-cookie}

Ответ на вход также устанавливает cookie сессии `hostpanel_session`. Она помечена `HttpOnly`, поэтому скрипт на странице не может её прочитать, и `SameSite=Strict`, поэтому браузер никогда не отправляет её с другого сайта. Она помечена `Secure` везде, кроме локального узла разработки. Браузеру, который её хранит, для собственных вызовов больше ничего не нужно. API заново выдаёт cookie при продлении сессии.

Запрос, который меняет состояние, то есть любой метод, кроме `GET`, `HEAD`, `OPTIONS` и `TRACE`, и который несёт cookie сессии, должен нести и этот заголовок, иначе он будет отклонён с `CSRF_HEADER_MISSING` (403):

<SettingTable :settings="[
  { name: 'X-Requested-With', type: 'заголовок запроса', default: 'XMLHttpRequest', description: 'Обязателен для запроса, который меняет состояние, когда учётными данными служит cookie сессии. Страница на другом сайте не может добавить этот заголовок без кросс-доменного разрешения, которого этот API не даёт.' },
]" />

Запросу, который аутентифицируется bearer-токеном, этот заголовок не нужен. Маршруты плагинов, `/api/admin/plugins/{id}/ext/...` и `/api/client/plugins/{id}/...`, требуют заголовок или bearer-токен в каждом запросе, который меняет состояние.

## Выход {#sign-out}

```
DELETE /api/auth/logout
```

Очищает cookie сессии и отвечает `200`, была сессия или нет. Токен не хранит состояния: на сервере нечего отзывать, поэтому копия токена остаётся действительной до истечения срока. Сброс пароля, отключение двухфакторной аутентификации и смена адреса электронной почты завершают все сессии учётной записи сразу.

## Двухфакторная аутентификация {#two-factor-authentication}

Если у учётной записи включена двухфакторная аутентификация, вход занимает два вызова. Первый отвечает `200` и вместо токена доступа возвращает ожидающий токен, при этом cookie не устанавливается:

```json
{
  "twoFactorRequired": true,
  "pendingToken": "..."
}
```

Отправьте ожидающий токен вместе с кодом из приложения-аутентификатора:

```
POST /api/auth/2fa-login
```

<SettingTable :settings="[
  { name: 'pendingToken', type: 'string', default: 'Нет', description: 'Ожидающий токен из первого вызова. Он живёт недолго.' },
  { name: 'code', type: 'string', default: 'Нет', description: 'Код из приложения-аутентификатора.' },
]" />

При успехе ответ такой же, как при входе без двухфакторной аутентификации: токен доступа и cookie сессии. Неверный код даёт `TWO_FACTOR_CODE_UNAUTHORIZED` (401). Ожидающий токен, который повреждён, просрочен или уже использован, даёт `PENDING_TOKEN_UNAUTHORIZED` (401).

## Что нужно каждой аудитории {#what-each-audience-needs}

Требования к каждой конечной точке указаны на его странице в блоке **Access**. Вот эти требования:

<SettingTable :settings="[
  { name: 'Anonymous', type: '', default: 'Нет', description: 'Учётные данные не нужны. Маршруты входа и публичные маршруты витрины.' },
  { name: 'Any signed-in user', type: '', default: 'Нет', description: 'Действующая сессия клиента или сотрудника.' },
  { name: 'Client', type: '', default: 'Нет', description: 'Вошедший клиентский пользователь, который действует в своей учётной записи. Вход сотрудника не связан с клиентской учётной записью, и такой вызов отклоняется.' },
  { name: 'Client permission', type: '', default: 'Нет', description: 'Для некоторых клиентских маршрутов нужно также разрешение, которое владелец учётной записи выдал пользователю, например ViewPayInvoices. Пользователь без него получает CLIENT_PERMISSION_DENIED (403).' },
  { name: 'Admin', type: '', default: 'Нет', description: 'Вошедший сотрудник с ролью Admin.' },
]" />

Разрешения, которые может иметь клиентский пользователь, перечислены в разделе [Пользователи и разрешения](/ru/customers/account#permissions) руководства для клиентов.

Вызов без учётных данных или с просроченными даёт `UNAUTHORIZED` (401). Вызов с учётными данными, которым это действие недоступно, даёт `FORBIDDEN` (403).

## Режим входа {#sign-on-mode}

<Label type="sso" /> Установка может передать вход внешнему сервису, если задать `AUTH_MODE=sso`. В этом режиме локальные маршруты API, `login`, `2fa-login`, `register`, `forgot-password`, `reset-password` и `confirm-email`, отвечают 404, а API не выдаёт собственных токенов: сессии и токены приходят от сервиса входа. `GET /api/auth/mode` сообщает клиенту, в каком режиме работает установка. Настройки описаны в разделе [Режимы входа](/ru/operators/configuration#sign-in-modes).

## Полный пример {#a-full-example}

Эти три вызова выполняют вход, обращаются к клиентскому маршруту и к маршруту администратора. Для клиентского маршрута нужен вход клиентского пользователя, а для маршрута администратора нужен сотрудник, поэтому в примере вход выполняется дважды.

**Шаг 1. Войдите как клиентский пользователь и прочитайте токен.**

::: code-group

```bash [curl]
curl -X POST 'https://panel.example.com/api/auth/login' \
  -H 'Content-Type: application/json' \
  -d '{"email": "client@example.com", "password": "your-password"}'
```

```js [JavaScript]
const signIn = await fetch('https://panel.example.com/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email: 'client@example.com', password: 'your-password' }),
})
const { accessToken } = await signIn.json()
```

:::

В ответе приходит токен:

```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresIn": 900
}
```

**Шаг 2. Вызовите клиентский маршрут с токеном.** Он возвращает счета вошедшего клиента.

::: code-group

```bash [curl]
curl 'https://panel.example.com/api/me/invoices' \
  -H 'Authorization: Bearer <token>'
```

```js [JavaScript]
const invoices = await fetch('https://panel.example.com/api/me/invoices', {
  headers: { Authorization: `Bearer ${accessToken}` },
})
console.log(await invoices.json())
```

:::

**Шаг 3. Вызовите маршрут администратора.** Войдите как сотрудник тем же способом, затем прочитайте страницу клиентов с этим токеном:

::: code-group

```bash [curl]
curl 'https://admin.example.com/api/clients?Page=1&PageSize=20' \
  -H 'Authorization: Bearer <staff-token>'
```

```js [JavaScript]
const clients = await fetch('https://admin.example.com/api/clients?Page=1&PageSize=20', {
  headers: { Authorization: `Bearer ${staffToken}` },
})
console.log(await clients.json())
```

:::

Если ответ содержит `X-Renewed-Access-Token`, используйте этот токен в следующих вызовах. Ответ на третий вызов, страница результатов, см. [Постраничная выдача](/ru/api-reference/pagination).
