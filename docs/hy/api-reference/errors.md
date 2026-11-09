---
title: Սխալներ
description: "Hostpanel API-ի յուրաքանչյուր ձախողում պատասխանում է մեկ application/problem+json մարմնով։ Սխալի մարմինը, սխալի կոդերը և Retry-After վերնագիրը 503-ի դեպքում։"
keywords: "hostpanel api սխալներ, problem+json, սխալի կոդեր, retry-after, 503"
---

# Սխալներ {#errors}

Յուրաքանչյուր ձախողում՝ բացակայող դաշտից մինչև սերվերի խափանում, պատասխանում է միևնույն մարմնով, որն ուղարկվում է որպես `application/problem+json`։

## Սխալի մարմինը {#the-error-body}

<SettingTable :settings="[
  { name: 'type', type: 'string', default: 'Չկա', description: 'Սխալի կոդը, օրինակ՝ VALIDATION_ERROR կամ INVOICE_NOT_FOUND։ Կոդերը կայուն են՝ ադմինի վահանակը և հաճախորդի պորտալը ճյուղավորվում են ըստ դրանց, և գոյություն ունեցող կոդը երբեք չի վերանվանվում։ Ճյուղավորվեք այս դաշտով, ոչ թե detail-ով։' },
  { name: 'title', type: 'string', default: 'Չկա', description: 'Կարճ վերնագիր, օրինակ՝ Not Found։ Սխալների մեծ մասի համար այն անգլերեն է՝ անկախ հարցման լեզվից։' },
  { name: 'status', type: 'number', default: 'Չկա', description: 'Պատասխանի HTTP կարգավիճակը՝ կրկնված։' },
  { name: 'detail', type: 'string', default: 'Չկա', description: 'Նախադասություն, որն ասում է, թե ինչն է սխալ ընթացել, X-Language վերնագրի լեզվով։ Տես Լեզուներ։' },
  { name: 'errors', type: 'object կամ null', default: 'Չկա', description: 'Null, բացի վավերացման սխալից՝ այդ դեպքում այն համապատասխանեցնում է յուրաքանչյուր դաշտի անունը camelCase-ով այն նախադասություններին, որոնք ասում են, թե դաշտում ինչն է սխալ։' },
]" />

**Վավերացման սխալ։** Հարցումը մերժվել է մինչև որևէ բան կատարվելը՝ `errors`-ը ցույց է տալիս, թե որ դաշտերն են սխալ։

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

**Բիզնես կանոնով մերժում։** Հարցումը ճիշտ է կազմված, բայց պատասխանը՝ ոչ։ Կոդը նշում է պատճառը։

```json
{
  "type": "INVOICE_NOT_FOUND",
  "title": "Not Found",
  "status": 404,
  "detail": "No such invoice.",
  "errors": null
}
```

## Սխալի կոդեր {#error-codes}

<!--@include: @/en/api-reference/_partials/error-codes.md-->

## 503 և Retry-After {#_503-and-retry-after}

Յուրաքանչյուր `503 Service Unavailable` պատասխան պարունակում է `Retry-After` վերնագիր՝ վայրկյանների այն քանակով, որքան պետք է սպասել նորից փորձելուց առաջ։ Լռելյայն արժեքը 30 վայրկյան է։ `429`-ը նույնպես այն պարունակում է, տես [Հարցումների սահմանափակումներ](/hy/api-reference/rate-limits)։

<Callout type="warning">
Հարգեք <code>Retry-After</code>-ը։ Անմիջապես նորից փորձելը՝ 503-ի կամ 429-ի դեպքում, միայն ծանրաբեռնում է այն ծառայությանը, որը խնդրել է սպասել։
</Callout>
