/**
 * An Identity user as the admin user-management screens see them: the list row, the client
 * accounts linked to it, and the detail that joins the two.
 */

/** What narrows the user list: the search text, which the identity provider matches itself. */
export interface UserListFilters {
  /** Search text matched against name and email; empty or missing for every user. */
  search?: string
}

/** Summary DTO for user list rows. */
export interface UserListItem {
  /** Identity user ID. */
  id: string
  /** Linked client ID, or null if no client record. */
  clientId: number | null
  /** User first name. */
  firstName: string
  /** User last name. */
  lastName: string
  /** User email address. */
  email: string
  /** Preferred language code or null. */
  language: string | null
  /** Last login timestamp or null. */
  lastLoginAt: string | null
  /** Account creation timestamp. */
  createdAt: string
}

/** Client account linked to a user. */
export interface UserAccount {
  /** Client primary key. */
  clientId: number
  /** Client first name. */
  firstName: string
  /** Client last name. */
  lastName: string
  /** Company name or null. */
  companyName: string | null
  /** Whether this user owns the account. */
  isOwner: boolean
}

/** Full user detail with linked accounts. */
export interface UserDetail extends UserListItem {
  /** Linked client accounts. */
  accounts: UserAccount[]
}

/** Body of `PUT /admin/users/{id}`. */
export interface UpdateUserPayload {
  /** First name. */
  firstName: string
  /** Last name. */
  lastName: string
  /** Email address. */
  email: string
  /** Preferred language code, or null. */
  language: string | null
}
