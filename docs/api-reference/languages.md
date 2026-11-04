---
title: Languages
---

# Languages

<Callout type="tip">
Send <code>X-Language</code> to get messages in the language of your user.
</Callout>

## The X-Language header

Send the language you want in the `X-Language` header. The API answers in English, Russian and Armenian.

<SettingTable :settings="[
  { name: 'X-Language', type: 'request header', default: 'en', description: 'The language code: en, ru or hy. The value is trimmed and compared without regard to case. Anything else, or no header, gives the default language of the install, which is English unless the operator changed it.' },
]" />

Only `X-Language` is read. `Accept-Language` is ignored, so a browser's language setting has no effect on a call to the API.

## What is translated

- **The sentence of an error.** The `detail` of every error body, and the sentences of a validation error in `errors`, come in the language you asked for. The `type` code never changes. Show `detail` to your user, not `title`: the title of most errors stays in English.
- **Storefront content.** Records an operator enters in several languages are returned in the language you asked for, and in the default language where a record has no translation: homepage slides, testimonials, partners, server locations and portfolio projects.

Content that an operator writes in one language only is returned as it is.

## Example

The same call, with a missing invoice, in Armenian:

```bash
curl 'https://panel.example.com/api/me/invoices/99999999' \
  -H 'Authorization: Bearer <token>' \
  -H 'X-Language: hy'
```

```json
{
  "type": "INVOICE_NOT_FOUND",
  "title": "Not Found",
  "status": 404,
  "detail": "Այդպիսի հաշիվ-ապրանքագիր չի գտնվել։",
  "errors": null
}
```
