---
title: Errors
---

# Errors

Every failure, from a missing field to a server fault, answers with the same body, sent as `application/problem+json`.

## The error body

<SettingTable :settings="[
  { name: 'type', type: 'string', default: 'None', description: 'The error code, for example VALIDATION_ERROR or INVOICE_NOT_FOUND. Codes are stable: the admin panel and the client portal branch on them, and an existing code is never renamed. Branch on this field, not on detail.' },
  { name: 'title', type: 'string', default: 'None', description: 'A short title, for example Not Found. For most errors it is English whatever the language of the request.' },
  { name: 'status', type: 'number', default: 'None', description: 'The HTTP status of the response, repeated.' },
  { name: 'detail', type: 'string', default: 'None', description: 'A sentence that says what went wrong, in the language of the X-Language header. See Languages.' },
  { name: 'errors', type: 'object or null', default: 'None', description: 'Null, except on a validation error: then it maps each field name, in camelCase, to the sentences that say what is wrong with it.' },
]" />

**A validation error.** The request was refused before anything ran; `errors` says which fields.

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

**A refusal by a business rule.** The request was well formed, but the answer is no. The code names the reason.

```json
{
  "type": "INVOICE_NOT_FOUND",
  "title": "Not Found",
  "status": 404,
  "detail": "No such invoice.",
  "errors": null
}
```

## Error codes

<!--@include: ./_partials/error-codes.md-->

## 503 and Retry-After

Every `503 Service Unavailable` carries a `Retry-After` header with the number of seconds to wait before you try again. The default is 30 seconds. A `429` carries it too, see [Rate limits](./rate-limits).

<Callout type="warning">
Respect <code>Retry-After</code>. Trying again at once, against a 503 or a 429, only adds load to a service that has asked you to wait.
</Callout>
