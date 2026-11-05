---
title: Pagination
---

# Pagination

The list endpoints that can return many rows answer one page at a time. The endpoint pages mark them: their query parameters include `Page` and `PageSize`, and their response body is a `PaginatedList`.

## Request

<SettingTable :settings="[
  { name: 'Page', type: 'number', default: 'None', description: 'The page to read, counting from 1. The name is matched without regard to case, so page works too. On most lists a value below 1 is refused with a validation error.' },
  { name: 'PageSize', type: 'number', default: 'None', description: 'How many items one page holds. The largest size depends on the list: 100 on an ordinary list, 200 on a report. On most lists a size outside 1 to the largest is refused.' },
]" />

Pass them in the query string. This reads the second page of 25 clients:

```
GET /api/clients?Page=2&PageSize=25
```

<Callout type="note">
The request names are <code>Page</code> and <code>PageSize</code>, but the response calls the page <code>pageNumber</code>. A request with a <code>pageNumber</code> parameter does not read that page: the name is not recognised, so the call is treated as if no page was given.
</Callout>

Out-of-range values are refused, not adjusted, on most lists. Asking `GET /api/clients?Page=0&PageSize=500` answers `400` with `VALIDATION_ERROR`:

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

A few lists, among them the list of users, adjust the values instead: a size above the largest is cut to it and a page below 1 is read as 1. The endpoint page does not say which kind a list is, so check the values you got back in the response.

A page after the last is not an error: it answers `200` with an empty `items` list.

## Response

A paginated endpoint answers with a `PaginatedList`:

<SettingTable :settings="[
  { name: 'items', type: 'array', default: 'None', description: 'The items on this page.' },
  { name: 'pageNumber', type: 'number', default: 'None', description: 'The number of this page, counting from 1.' },
  { name: 'pageSize', type: 'number', default: 'None', description: 'The page size that was applied.' },
  { name: 'totalPages', type: 'number', default: 'None', description: 'How many pages the whole result has.' },
  { name: 'totalCount', type: 'number', default: 'None', description: 'How many items the whole result has.' },
  { name: 'hasPreviousPage', type: 'boolean', default: 'None', description: 'True when pageNumber is greater than 1.' },
  { name: 'hasNextPage', type: 'boolean', default: 'None', description: 'True when pageNumber is less than totalPages.' },
  { name: 'metadata', type: 'object or null', default: 'None', description: 'Extra data about the list as a whole, for the endpoints that have some. Null otherwise.' },
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

To read every page, follow `hasNextPage` until it is false.

Some list endpoints return a plain array and no paging at all, for example the invoices of a client. Their endpoint page shows the array as the body.
