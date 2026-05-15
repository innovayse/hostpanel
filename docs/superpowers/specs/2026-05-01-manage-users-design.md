# Manage Users Page — Design Spec

**Date:** 2026-05-01
**Status:** Draft

## Summary

Admin page at `/clients/users` for managing Identity users. Lists all users with profile info and last login time. Provides edit modal, password reset via email, and admin-initiated password change. Deleting a user removes the Identity record only — client data is preserved for reporting.

## Page Layout

### Users Table

Paginated table with columns:

| Column | Source | Notes |
|--------|--------|-------|
| First Name | `AppUser.FirstName` | |
| Last Name | `AppUser.LastName` | |
| Email | `AppUser.Email` | |
| Last Login | `AppUser.LastLoginAt` | Formatted as locale date+time (e.g. "Apr 30, 2026, 14:32"). Shows "Never" if null |
| Actions | — | Split button |

### Actions Split Button

`[ Manage User | ▾ ]`

- **"Manage User"** (left portion) → opens UserFormModal
- **▾** (right portion) → opens dropdown:
  - "Send Password Reset Email" → calls `POST /api/admin/users/{id}/reset-password`, shows success/error toast
  - "Change Password" → opens ChangePasswordModal

## Manage User Modal (UserFormModal)

Fields:
- **First Name** — text input, required
- **Last Name** — text input, required
- **Email** — text input, required
- **Language** — AppSelect dropdown with options: Default, English (`en`), Russian (`ru`), Armenian (`hy`)

Accounts section (read-only):
- Table: ID, Client Name, Company Name, Owner (checkmark if the client's `UserId` matches)
- Shows all client records linked to this Identity user
- If no linked accounts, shows "No accounts linked"

Footer:
- Left: **Permanently Delete** — red button, confirms via browser `confirm()` dialog, then calls `DELETE /api/admin/users/{id}`
- Right: **Close** (secondary) + **Save** (primary gradient)

## Change Password Modal (ChangePasswordModal)

Small centered modal:
- **New Password** — single password input
- Footer: **Cancel** (secondary) + **Save** (primary)
- Calls `POST /api/admin/users/{id}/change-password` with `{ password }`

## Backend Changes

### 1. AppUser Entity

Add two fields to `AppUser`:

```csharp
public string? Language { get; set; }
public DateTimeOffset? LastLoginAt { get; set; }
```

Create EF migration for these columns.

### 2. Update LastLoginAt on Login

In `LoginHandler`, after successful authentication, update `LastLoginAt` to `DateTimeOffset.UtcNow` and save. Requires adding an update method to `IUserService`.

### 3. New Controller — AdminUsersController

Route: `api/admin/users`
Auth: `[Authorize(Roles = "Admin")]`

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Paginated user list. Query params: `page`, `pageSize`, `search`. Returns `PagedResult<UserListItemDto>` |
| GET | `/{id}` | Single user with linked client accounts. Returns `UserDetailDto` |
| PUT | `/{id}` | Update firstName, lastName, email, language. Body: `UpdateUserRequest` |
| DELETE | `/{id}` | Delete Identity user only. Client records stay as orphans |
| POST | `/{id}/reset-password` | Generates password reset token, sends email via existing email system |
| POST | `/{id}/change-password` | Admin sets password directly. Body: `{ password: string }` |

### 4. DTOs

**UserListItemDto:**
```
Id, FirstName, LastName, Email, Language, LastLoginAt, CreatedAt
```

**UserDetailDto:**
```
Id, FirstName, LastName, Email, Language, LastLoginAt, CreatedAt,
Accounts: UserAccountDto[]
```

**UserAccountDto:**
```
ClientId, FirstName, LastName, CompanyName, IsOwner (always true — 1:1 user→client)
```

**UpdateUserRequest:**
```
FirstName, LastName, Email, Language
```

**ChangePasswordRequest:**
```
Password
```

### 5. IUserService — New Methods

```csharp
Task<(List<UserListItemDto> Items, int TotalCount)> ListAsync(int page, int pageSize, string? search, CancellationToken ct);
Task<UserDetailDto?> GetByIdWithAccountsAsync(string userId, CancellationToken ct);
Task UpdateProfileAsync(string userId, string firstName, string lastName, string email, string? language, CancellationToken ct);
Task DeleteAsync(string userId, CancellationToken ct);
Task<string> GeneratePasswordResetTokenAsync(string userId, CancellationToken ct);
Task ChangePasswordAsync(string userId, string newPassword, CancellationToken ct);
Task UpdateLastLoginAsync(string userId, CancellationToken ct);
```

### 6. Password Reset Email

Uses the existing email template system:
- Create a `user-password-reset` email template (seeded on first use or via migration)
- Template receives `{{ reset_link }}` variable
- Reset link points to a dedicated password reset page (e.g. `/client/reset-password?token=...&email=...`)

## Frontend Files

| File | Purpose |
|------|---------|
| `admin/src/modules/clients/stores/usersStore.ts` | New Pinia store for user CRUD |
| `admin/src/modules/clients/views/ManageUsersView.vue` | Rewrite placeholder with users table |
| `admin/src/modules/clients/components/UserFormModal.vue` | Edit user modal with accounts section |
| `admin/src/modules/clients/components/ChangePasswordModal.vue` | Single-field password modal |

## Out of Scope

- Two-factor authentication toggle (shown in WHMCS screenshot but deferred)
- User role management (Admin/Reseller/Client role assignment)
- Bulk actions (multi-select delete, etc.)
- User creation from admin panel (users register themselves)
