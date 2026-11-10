/**
 * Timings and sizes shared by several screens: how long a notice stays up, how long a search waits
 * for the last keystroke, how many rows a page holds, how long a text field may be. A screen imports
 * the name instead of repeating the number, so one value changes in one place.
 */

/** Milliseconds in a second. */
export const MS_PER_SECOND = 1000

/** Milliseconds in a minute. */
export const MS_PER_MINUTE = 60 * MS_PER_SECOND

/** Milliseconds in a day. */
export const MS_PER_DAY = 24 * 60 * MS_PER_MINUTE

/** Bytes in a kilobyte, to show a file size in KB. */
export const BYTES_PER_KB = 1024

/** How long a "saved" or "done" notice stays on screen, in milliseconds. */
export const NOTICE_VISIBLE_MS = 3 * MS_PER_SECOND

/** How long typing must pause before a search is sent, in milliseconds. */
export const SEARCH_DEBOUNCE_MS = 300

/** Rows per page of an ordinary list. */
export const DEFAULT_PAGE_SIZE = 20

/** Rows per page of a ledger-style list (invoices, transactions, activity). */
export const LEDGER_PAGE_SIZE = 25

/** Rows per page of a report table. */
export const REPORT_PAGE_SIZE = 50

/** Rows fetched at once to fill a picker or a whole-client list. */
export const LOOKUP_PAGE_SIZE = 100

/** Longest a one-line text setting (a name, a tagline, a contact line) may be. */
export const SHORT_TEXT_MAX_LENGTH = 200

/** Longest a tag-manager container id may be. */
export const ANALYTICS_ID_MAX_LENGTH = 20

/** Longest a chat server token or address may be. */
export const CHAT_SETTING_MAX_LENGTH = 300

/** Longest description the API accepts for a billable item (its create validator's limit). */
export const BILLABLE_ITEM_DESCRIPTION_MAX_LENGTH = 500

/** Length of an ISO 4217 currency code, alpha or numeric. */
export const CURRENCY_CODE_LENGTH = 3

/** Most decimal places a currency may show. */
export const CURRENCY_MAX_DECIMALS = 4

/** Smallest step of an exchange rate to the base currency. */
export const EXCHANGE_RATE_STEP = 0.00000001

/** Length of a one-time sign-in code. */
export const TWO_FACTOR_CODE_LENGTH = 6

/** The top of a percentage. */
export const MAX_PERCENT = 100

/** How often a running migration job is polled for its status, in milliseconds. */
export const POLL_INTERVAL_MS = 2 * MS_PER_SECOND

/** How long a "copied" confirmation stays on screen, in milliseconds. */
export const COPIED_VISIBLE_MS = 2 * MS_PER_SECOND
