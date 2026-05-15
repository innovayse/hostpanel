# Clients Advanced Search — Design Spec

**Date:** 2026-04-30
**Status:** Draft

## Summary

Replace the single search input on the admin Clients list page with a multi-field filter bar inspired by WHMCS. Filters are applied server-side and passed as query parameters to `GET /api/clients`.

## Filter Fields

| Field | Type | Backend Param | Searches |
|-------|------|---------------|----------|
| Client/Company Name | Text input | `search` | firstName, lastName, companyName (existing) |
| Email Address | Text input | `email` | Identity user email |
| Phone Number | Text input | `phone` | Client.Phone |
| Status | Dropdown | `status` | Client.Status enum (Any/Active/Inactive/Suspended/Closed) |

**Client Group** is excluded from this iteration (no entity exists yet).

## UX Behavior

- Filter bar sits between the page header and the table, inside a `bg-surface-card` card with `border-border`.
- All text inputs are inline on desktop (single row), stacking on mobile.
- **Search button** (primary gradient) applies all filters and resets to page 1.
- **Clear link** resets all fields to empty/Any and re-fetches.
- Filters are only applied on explicit Search click — no debounce/auto-submit.
- Active filter count badge shown on the Search button when filters are applied.
- Empty state message updates to "No clients match your filters" when filters are active.

## Backend Changes

### 1. Query Record

```csharp
// ListClientsQuery.cs
public record ListClientsQuery(
    int Page,
    int PageSize,
    string? Search = null,
    string? Email = null,
    string? Phone = null,
    string? Status = null);
```

### 2. Controller

Add `email`, `phone`, and `status` as `[FromQuery]` parameters to `ClientsController.GetAllAsync`. Pass them through to the query.

### 3. Repository — IClientRepository

Update `ListAsync` signature:

```csharp
Task<(IReadOnlyList<Client> Items, int TotalCount)> ListAsync(
    int page, int pageSize, string? search,
    string? phone, string? status,
    CancellationToken ct);
```

Phone and status are filtered directly on the `Client` entity. Email filtering cannot happen in the repository because email lives on the Identity user.

### 4. Handler — ListClientsHandler

Email filtering strategy:
- If `email` filter is provided, use `IUserService` to find matching user IDs first, then pass those IDs to the repository as an additional filter.
- This avoids cross-DbContext joins while keeping the filter server-side.

Add to `IUserService`:

```csharp
Task<List<string>> FindUserIdsByEmailAsync(string emailSearch, CancellationToken ct);
```

Update `IClientRepository.ListAsync` to accept `IEnumerable<string>? userIds` for email-based filtering:

```csharp
Task<(IReadOnlyList<Client> Items, int TotalCount)> ListAsync(
    int page, int pageSize, string? search,
    string? phone, string? status,
    IEnumerable<string>? userIds,
    CancellationToken ct);
```

Handler flow:
1. If email filter present → call `userService.FindUserIdsByEmailAsync(email)` → get matching user IDs
2. Pass all filters (search, phone, status, userIds) to `clientRepo.ListAsync`
3. Batch-fetch emails for returned clients (existing logic)
4. Return `PagedResult<ClientListItemDto>`

### 5. Repository Implementation

```csharp
public async Task<(IReadOnlyList<Client> Items, int TotalCount)> ListAsync(
    int page, int pageSize, string? search,
    string? phone, string? status,
    IEnumerable<string>? userIds,
    CancellationToken ct)
{
    var query = db.Clients.AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.ToLower();
        query = query.Where(c =>
            c.FirstName.ToLower().Contains(term) ||
            c.LastName.ToLower().Contains(term) ||
            (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
    }

    if (!string.IsNullOrWhiteSpace(phone))
    {
        var phoneTerm = phone.ToLower();
        query = query.Where(c => c.Phone != null && c.Phone.ToLower().Contains(phoneTerm));
    }

    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ClientStatus>(status, true, out var parsed))
    {
        query = query.Where(c => c.Status == parsed);
    }

    if (userIds is not null)
    {
        var idList = userIds.ToList();
        query = query.Where(c => idList.Contains(c.UserId));
    }

    var totalCount = await query.CountAsync(ct);
    var items = await query
        .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
        .Skip((page - 1) * pageSize).Take(pageSize)
        .ToListAsync(ct);

    return (items, totalCount);
}
```

## Frontend Changes

### 1. Store

Replace `fetchAll(search?: string)` with `fetchAll(filters?: ClientFilters)`:

```typescript
interface ClientFilters {
  search?: string
  email?: string
  phone?: string
  status?: string
}
```

Build `URLSearchParams` from all non-empty filter values.

### 2. View — Filter Bar

Filter bar component rendered above the table:

```
┌─────────────────────────────────────────────────────────────────────┐
│  [Name input]  [Email input]  [Phone input]  [Status ▾]  [Search] │
└─────────────────────────────────────────────────────────────────────┘
```

- Inputs use the same dark theme styling as the existing search input.
- Status dropdown: native `<select>` styled to match, options: Any, Active, Inactive, Suspended, Closed.
- Search button: `gradient-brand` styled primary button.
- Clear link: text button, only visible when any filter is active.

### 3. Mobile

On screens < `sm`, inputs stack vertically (1 column). Search button spans full width.

## Files Changed

**Backend:**
- `Application/Clients/Queries/ListClients/ListClientsQuery.cs` — add params
- `Application/Clients/Queries/ListClients/ListClientsHandler.cs` — email lookup + pass filters
- `Application/Auth/Interfaces/IUserService.cs` — add `FindUserIdsByEmailAsync`
- `Infrastructure/Auth/UserService.cs` — implement email search
- `Domain/Clients/Interfaces/IClientRepository.cs` — update `ListAsync` signature
- `Infrastructure/Clients/ClientRepository.cs` — implement new filters
- `API/Clients/ClientsController.cs` — add query params

**Frontend:**
- `admin/src/modules/clients/stores/clientsStore.ts` — filter params
- `admin/src/modules/clients/views/ClientsListView.vue` — filter bar UI

## Out of Scope

- Client Group filter (no entity yet)
- Export/CSV download
- Saved/named filters
- Column sorting
