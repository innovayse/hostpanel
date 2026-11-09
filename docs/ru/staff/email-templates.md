---
title: Шаблоны писем
description: "Редактирование транзакционных шаблонов писем Hostpanel для каждого языка (английский, русский, армянский), использование плейсхолдеров и проверка доставки почты."
keywords: "шаблоны писем, плейсхолдеры, транзакционные письма, доставка почты"
---

# Шаблоны писем <Label type="admin" /> {#email-templates}

<DemoLink path="/settings/email-templates" />

## Как работают шаблоны {#how-templates-work}

У каждого шаблона письма есть **slug**, по которому он определяется в коде. Для каждого языка (английского, русского и армянского) хранится одна версия. Часть шаблонов создаётся при запуске, остальные создаются при первом использовании. Если версии для языка клиента нет, письмо отправляется на английском.

Создание версии для slug и языка, которые уже существуют, возвращает `EMAIL_TEMPLATE_ALREADY_EXISTS`. Если шаблона не существует, возвращается `EMAIL_TEMPLATE_NOT_FOUND`.

<Callout type="tip">
Шаблоны, которые вы изменили, при обновлении никогда не перезаписываются. Ваши изменения в безопасности.
</Callout>

## Экран «Шаблоны писем» {#the-email-templates-screen}

Откройте **Настройки → Шаблоны писем**. Подзаголовок: «Шаблоны писем».

**Столбцы:** Название, Тема, Языки, Изменить.

## Редактирование шаблона {#edit-a-template}

<Steps>
<li>

Найдите строку шаблона и нажмите **Изменить**.

</li>
<li>

Выберите вкладку языка, который хотите отредактировать. Вкладка с пометкой об отсутствии означает, что сохранённой версии нет; сохранение создаёт её.

</li>
<li>

Отредактируйте **Тему**, **Описание** и **Текст (HTML)**.

</li>
<li>

Нажмите **Сохранить**.

</li>
</Steps>

## Плейсхолдеры {#placeholders}

Плейсхолдеры используют язык шаблонов Liquid. Переменная записывается как <code v-pre>{{ name }}</code> или <code v-pre>{{ object.field }}</code>, имена пишутся строчными буквами с подчёркиваниями. Каждому шаблону доступны переменные бренда и сайта:

```liquid
{{ brand.primary }}       — primary brand colour
{{ brand.primary_dark }}  — dark variant of the primary colour
{{ brand.on_primary }}    — text colour on the primary background
{{ brand.accent }}        — accent colour
{{ brand.logo_url }}      — URL of the brand logo
{{ site.name }}           — the name of the site
```

Пример с условием и циклом:

```liquid
{% if invoice.total > 0 %}
  <p>Invoice #{{ invoice.id }} for {{ invoice.total }} {{ invoice.currency }}</p>
{% endif %}

{% for item in items %}
  <li>{{ item.name }}</li>
{% endfor %}
```

## Шаблоны и их плейсхолдеры {#templates-and-their-placeholders}

### Аккаунт {#account}

<SettingTable :settings="[
  { name: 'welcome', type: 'string', default: '', description: 'Отправляется новому клиенту после создания аккаунта. Плейсхолдеры: client.email' },
  { name: 'user-invite', type: 'string', default: '', description: 'Отправляется, когда пользователя приглашают в аккаунт клиента. Плейсхолдеры: invite_link, client_name, first_name' },
  { name: 'user-email-verification', type: 'string', default: '', description: 'Только для локальных аккаунтов. Отправляется для подтверждения нового адреса электронной почты. Плейсхолдеры: verification_link' },
  { name: 'user-password-reset', type: 'string', default: '', description: 'Только для локальных аккаунтов. Отправляется, когда запрашивается сброс пароля. Плейсхолдеры: reset_link' },
  { name: 'user-email-change', type: 'string', default: '', description: 'Только для локальных аккаунтов. Отправляется для подтверждения смены адреса электронной почты. Плейсхолдеры: confirmation_link' },
  { name: 'user-email-changed-notice', type: 'string', default: '', description: 'Только для локальных аккаунтов. Отправляется на старый адрес после смены адреса электронной почты. Плейсхолдеры: new_email' },
]" />

### Биллинг {#billing}

<SettingTable :settings="[
  { name: 'invoice-created', type: 'string', default: '', description: 'Отправляется клиенту при создании счёта. Плейсхолдеры: invoice.id, invoice.total, invoice.currency' },
  { name: 'payment-received', type: 'string', default: '', description: 'Отправляется клиенту при записи платежа. Плейсхолдеры: invoice.id, invoice.total, invoice.transaction_id' },
  { name: 'invoice-renewal-reminder', type: 'string', default: '', description: 'Отправляется, когда наступает время напоминания о продлении. Плейсхолдеры: invoice.id, invoice.total, invoice.currency, invoice.due_date, invoice.url' },
  { name: 'gateway-payment-stalled-admin', type: 'string', default: '', description: 'Отправляется сотрудникам, когда платёж через шлюз слишком долго находится в ожидании. Плейсхолдеры: invoice.id, client.id, module, pending_hours' },
  { name: 'gateway-payment-credited-admin', type: 'string', default: '', description: 'Отправляется сотрудникам, когда шлюз принял платёж, который счёт не смог записать, и сумма была добавлена на кредитный баланс клиента или оставлена для ручного разбора. Плейсхолдеры: invoice.id, client.id, module, reference, amount, currency, credited' },
  { name: 'gateway-payment-without-reference-admin', type: 'string', default: '', description: 'Отправляется сотрудникам, когда шлюз сообщает о платеже как об оплаченном, но не присылает ссылку на платёж. Ничего не записывается; оператор должен найти и записать платёж вручную. Плейсхолдеры: invoice.id, client.id, module, amount, currency' },
]" />

### Услуги {#services}

<SettingTable :settings="[
  { name: 'service-provisioned', type: 'string', default: '', description: 'Отправляется клиенту, когда услуга настроена. Плейсхолдеры: service.id, service.provisioning_ref, service.domain, service.username' },
  { name: 'service-termination-failed-admin', type: 'string', default: '', description: 'Отправляется сотрудникам, когда не удалось завершить услугу. Плейсхолдеры: service.id, reason' },
]" />

### Тикеты поддержки {#support-tickets}

<SettingTable :settings="[
  { name: 'ticket-created', type: 'string', default: '', description: 'Отправляется отделу, когда открывается тикет. Плейсхолдеры: ticket.id, ticket.subject' },
  { name: 'ticket-replied', type: 'string', default: '', description: 'Отправляется клиенту (ответ сотрудника) или отделу (ответ клиента). Плейсхолдеры: ticket.id, ticket.subject' },
  { name: 'ticket-closed', type: 'string', default: '', description: 'Отправляется клиенту, когда тикет закрывается. Плейсхолдеры: ticket.id, ticket.subject' },
]" />

### Домены {#domains}

<SettingTable :settings="[
  { name: 'domain-registered', type: 'string', default: '', description: 'Отправляется клиенту, когда домен зарегистрирован. Плейсхолдеры: domain.id, domain.name, domain.registrar, domain.expires_at' },
  { name: 'domain-transferred', type: 'string', default: '', description: 'Отправляется клиенту, когда перенос домена завершён. Плейсхолдеры: domain.id, domain.name, domain.registrar, domain.expires_at' },
  { name: 'domain-renewed', type: 'string', default: '', description: 'Отправляется клиенту, когда домен продлён. Плейсхолдеры: domain.id, domain.name, domain.new_expires_at' },
  { name: 'domain-expiring', type: 'string', default: '', description: 'Отправляется, когда подходит дата истечения домена. Плейсхолдеры: domain.id, domain.name, domain.expires_at, domain.days_until_expiry' },
  { name: 'domain-expired', type: 'string', default: '', description: 'Отправляется, когда дата истечения домена прошла. Плейсхолдеры: domain.id, domain.name, domain.linked_service_id' },
  { name: 'domain-registration-failed', type: 'string', default: '', description: 'Отправляется клиенту, когда регистрация домена не удалась. Плейсхолдеры: domain.name, invoice.id' },
  { name: 'domain-registration-failed-admin', type: 'string', default: '', description: 'Отправляется сотрудникам, когда регистрация домена не удалась. Плейсхолдеры: domain.name, client.id, invoice.id, reason' },
  { name: 'domain-renewal-failed-admin', type: 'string', default: '', description: 'Отправляется сотрудникам, когда продление домена не удалось. Плейсхолдеры: domain.name, client.id, invoice.id, reason' },
]" />

## Доставка почты {#mail-delivery}

Шаблоны отправляются через настройки SMTP, заданные в [справочнике по настройке](/ru/operators/configuration).

Клиенты могут видеть отправленные им письма в разделе **История писем** клиентского портала.
