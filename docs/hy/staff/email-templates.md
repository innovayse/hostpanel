---
title: Նամակների ձևանմուշներ
description: "Խմբագրեք Hostpanel-ի գործարքային նամակների ձևանմուշները ըստ լեզվի (անգլերեն, ռուսերեն, հայերեն), օգտագործեք փոփոխականներ և ստուգեք նամակների առաքումը։"
keywords: "նամակների ձևանմուշներ, փոփոխականներ, գործարքային նամակներ, նամակների առաքում"
---

# Նամակների ձևանմուշներ <Label type="admin" /> {#email-templates}

<DemoLink path="/settings/email-templates" />

## Ինչպես են աշխատում ձևանմուշները {#how-templates-work}

Նամակի յուրաքանչյուր ձևանմուշ ունի **slug**, որով այն նույնականացվում է կոդում։ Յուրաքանչյուր լեզվի համար պահվում է մեկ տարբերակ (անգլերեն, ռուսերեն և հայերեն)։ Որոշ ձևանմուշներ ստեղծվում են գործարկման ժամանակ, մյուսները՝ առաջին օգտագործման ժամանակ։ Եթե հաճախորդի լեզվով տարբերակ չկա, նամակն ուղարկվում է անգլերենով։

Գոյություն ունեցող slug-ի և լեզվի համար տարբերակ ստեղծելիս վերադարձվում է `EMAIL_TEMPLATE_ALREADY_EXISTS` սխալը։ Գոյություն չունեցող ձևանմուշի դեպքում վերադարձվում է `EMAIL_TEMPLATE_NOT_FOUND`։

<Callout type="tip">
Ձեր խմբագրած ձևանմուշները թարմացման ժամանակ երբեք չեն վերագրվում։ Ձեր փոփոխությունները պահպանվում են։
</Callout>

## Նամակների ձևանմուշների էկրանը {#the-email-templates-screen}

Բացեք **Կարգավորումներ → Նամակների ձևանմուշներ**։ Ենթավերնագիր՝ «Նամակների ձևանմուշներ»։

**Սյունակներ՝** Անվանում, Թեմա, Լեզուներ, Խմբագրել։

## Խմբագրեք ձևանմուշ {#edit-a-template}

<Steps>
<li>

Գտեք ձևանմուշի տողը և սեղմեք **Խմբագրել**։

</li>
<li>

Ընտրեք այն լեզվի ներդիրը, որը ցանկանում եք խմբագրել։ «Բացակայող» նշումով ներդիրը պահված տարբերակ չունի․ պահպանելիս այն կստեղծվի։

</li>
<li>

Խմբագրեք **Թեմա**, **Նկարագրություն** և **Բովանդակություն (HTML)** դաշտերը։

</li>
<li>

Սեղմեք **Պահպանել**։

</li>
</Steps>

## Փոփոխականներ {#placeholders}

Փոփոխականները օգտագործում են Liquid ձևանմուշների լեզուն։ Փոփոխականը գրվում է որպես <code v-pre>{{ name }}</code> կամ <code v-pre>{{ object.field }}</code>, անունները՝ փոքր տառերով և ընդգծման նշաններով։ Բոլոր ձևանմուշներին հասանելի են բրենդի և կայքի փոփոխականները.

```liquid
{{ brand.primary }}       — primary brand colour
{{ brand.primary_dark }}  — dark variant of the primary colour
{{ brand.on_primary }}    — text colour on the primary background
{{ brand.accent }}        — accent colour
{{ brand.logo_url }}      — URL of the brand logo
{{ site.name }}           — the name of the site
```

Օրինակ՝ պայմանով և ցիկլով.

```liquid
{% if invoice.total > 0 %}
  <p>Invoice #{{ invoice.id }} for {{ invoice.total }} {{ invoice.currency }}</p>
{% endif %}

{% for item in items %}
  <li>{{ item.name }}</li>
{% endfor %}
```

## Ձևանմուշները և դրանց փոփոխականները {#templates-and-their-placeholders}

### Հաշիվ {#account}

<SettingTable :settings="[
  { name: 'welcome', type: 'string', default: '', description: 'Ուղարկվում է նոր հաճախորդին հաշվի ստեղծումից հետո։ Փոփոխականներ՝ client.email' },
  { name: 'user-invite', type: 'string', default: '', description: 'Ուղարկվում է, երբ օգտվողին հրավիրում են հաճախորդի հաշիվ։ Փոփոխականներ՝ invite_link, client_name, first_name' },
  { name: 'user-email-verification', type: 'string', default: '', description: 'Միայն լոկալ հաշիվների համար։ Ուղարկվում է նոր էլ. փոստի հասցեն հաստատելու համար։ Փոփոխականներ՝ verification_link' },
  { name: 'user-password-reset', type: 'string', default: '', description: 'Միայն լոկալ հաշիվների համար։ Ուղարկվում է գաղտնաբառի վերականգնում պահանջելիս։ Փոփոխականներ՝ reset_link' },
  { name: 'user-email-change', type: 'string', default: '', description: 'Միայն լոկալ հաշիվների համար։ Ուղարկվում է էլ. փոստի հասցեի փոփոխությունը հաստատելու համար։ Փոփոխականներ՝ confirmation_link' },
  { name: 'user-email-changed-notice', type: 'string', default: '', description: 'Միայն լոկալ հաշիվների համար։ Ուղարկվում է հին հասցեին՝ էլ. փոստի հասցեն փոխելուց հետո։ Փոփոխականներ՝ new_email' },
]" />

### Վճարումներ {#billing}

<SettingTable :settings="[
  { name: 'invoice-created', type: 'string', default: '', description: 'Ուղարկվում է հաճախորդին հաշիվ-ապրանքագիր ստեղծվելիս։ Փոփոխականներ՝ invoice.id, invoice.total, invoice.currency' },
  { name: 'payment-received', type: 'string', default: '', description: 'Ուղարկվում է հաճախորդին վճարումը գրանցվելիս։ Փոփոխականներ՝ invoice.id, invoice.total, invoice.transaction_id' },
  { name: 'invoice-renewal-reminder', type: 'string', default: '', description: 'Ուղարկվում է, երբ երկարացման հիշեցման ժամանակն է։ Փոփոխականներ՝ invoice.id, invoice.total, invoice.currency, invoice.due_date, invoice.url' },
  { name: 'gateway-payment-stalled-admin', type: 'string', default: '', description: 'Ուղարկվում է աշխատակիցներին, երբ վճարային համակարգի վճարումը չափից երկար է սպասման մեջ։ Փոփոխականներ՝ invoice.id, client.id, module, pending_hours' },
  { name: 'gateway-payment-credited-admin', type: 'string', default: '', description: 'Ուղարկվում է աշխատակիցներին, երբ վճարային համակարգի վճարման միջոցով ստացվել է գումար, որը հաշիվ-ապրանքագիրը չի կարողացել գրանցել, և գումարը ավելացվել է հաճախորդի հաշվեկշռին կամ պահվել է ձեռքով լուծման համար։ Փոփոխականներ՝ invoice.id, client.id, module, reference, amount, currency, credited' },
  { name: 'gateway-payment-without-reference-admin', type: 'string', default: '', description: 'Ուղարկվում է աշխատակիցներին, երբ վճարային համակարգը վճարումը նշում է որպես վճարված, բայց վճարման հղում չի ուղարկում։ Ոչինչ չի գրանցվում․ օպերատորը պետք է գտնի և գրանցի այն ձեռքով։ Փոփոխականներ՝ invoice.id, client.id, module, amount, currency' },
]" />

### Ծառայություններ {#services}

<SettingTable :settings="[
  { name: 'service-provisioned', type: 'string', default: '', description: 'Ուղարկվում է հաճախորդին ծառայությունը ակտիվացվելիս։ Փոփոխականներ՝ service.id, service.provisioning_ref, service.domain, service.username' },
  { name: 'service-termination-failed-admin', type: 'string', default: '', description: 'Ուղարկվում է աշխատակիցներին, երբ ծառայության դադարեցումը ձախողվում է։ Փոփոխականներ՝ service.id, reason' },
]" />

### Աջակցության հայտեր {#support-tickets}

<SettingTable :settings="[
  { name: 'ticket-created', type: 'string', default: '', description: 'Ուղարկվում է բաժնին հայտը բացվելիս։ Փոփոխականներ՝ ticket.id, ticket.subject' },
  { name: 'ticket-replied', type: 'string', default: '', description: 'Ուղարկվում է հաճախորդին (աշխատակցի պատասխան) կամ բաժնին (հաճախորդի պատասխան)։ Փոփոխականներ՝ ticket.id, ticket.subject' },
  { name: 'ticket-closed', type: 'string', default: '', description: 'Ուղարկվում է հաճախորդին հայտը փակվելիս։ Փոփոխականներ՝ ticket.id, ticket.subject' },
]" />

### Դոմեններ {#domains}

<SettingTable :settings="[
  { name: 'domain-registered', type: 'string', default: '', description: 'Ուղարկվում է հաճախորդին դոմենը գրանցվելիս։ Փոփոխականներ՝ domain.id, domain.name, domain.registrar, domain.expires_at' },
  { name: 'domain-transferred', type: 'string', default: '', description: 'Ուղարկվում է հաճախորդին դոմենի տեղափոխումն ավարտվելիս։ Փոփոխականներ՝ domain.id, domain.name, domain.registrar, domain.expires_at' },
  { name: 'domain-renewed', type: 'string', default: '', description: 'Ուղարկվում է հաճախորդին դոմենը երկարացվելիս։ Փոփոխականներ՝ domain.id, domain.name, domain.new_expires_at' },
  { name: 'domain-expiring', type: 'string', default: '', description: 'Ուղարկվում է, երբ դոմենի ավարտի ժամկետը մոտենում է։ Փոփոխականներ՝ domain.id, domain.name, domain.expires_at, domain.days_until_expiry' },
  { name: 'domain-expired', type: 'string', default: '', description: 'Ուղարկվում է, երբ դոմենի ժամկետն անցել է։ Փոփոխականներ՝ domain.id, domain.name, domain.linked_service_id' },
  { name: 'domain-registration-failed', type: 'string', default: '', description: 'Ուղարկվում է հաճախորդին, երբ դոմենի գրանցումը ձախողվում է։ Փոփոխականներ՝ domain.name, invoice.id' },
  { name: 'domain-registration-failed-admin', type: 'string', default: '', description: 'Ուղարկվում է աշխատակիցներին, երբ դոմենի գրանցումը ձախողվում է։ Փոփոխականներ՝ domain.name, client.id, invoice.id, reason' },
  { name: 'domain-renewal-failed-admin', type: 'string', default: '', description: 'Ուղարկվում է աշխատակիցներին, երբ դոմենի երկարացումը ձախողվում է։ Փոփոխականներ՝ domain.name, client.id, invoice.id, reason' },
]" />

## Նամակների առաքում {#mail-delivery}

Ձևանմուշներն ուղարկվում են [Կարգավորումների տեղեկատուում](/hy/operators/configuration) նշված SMTP կարգավորումներով։

Հաճախորդները կարող են տեսնել իրենց ուղարկված նամակները հաճախորդի պորտալի **Նամակներ** բաժնում։
