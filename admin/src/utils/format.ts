/**
 * Formats an ISO date string as a localized date.
 *
 * @param iso - ISO 8601 date string, null, or undefined.
 * @param fallback - Text to return if the date is invalid. Defaults to '\u2014'.
 * @returns Formatted date string or the fallback.
 */
export function formatDate(iso: string | null | undefined, fallback = '\u2014'): string {
  if (!iso) return fallback
  const d = new Date(iso)
  if (isNaN(d.getTime()) || d.getFullYear() < 2000) return fallback
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' })
}

/**
 * Extracts the YYYY-MM-DD portion from an ISO date string for input[type=date].
 *
 * @param iso - ISO 8601 date string or undefined.
 * @returns Date string suitable for date input, or empty string.
 */
export function toDateInputValue(iso: string | null | undefined): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  return d.toISOString().split('T')[0] ?? ''
}

/**
 * Splits a `YYYY-MM-DD` date string into its numeric year, month and day parts.
 *
 * Exists because `noUncheckedIndexedAccess` types the result of `String.split` as
 * `(string | undefined)[]`, which every caller would otherwise have to widen by hand.
 * A missing part yields `NaN`, exactly as `parseInt(undefined as never)` did before —
 * callers here always pass a string that has already been validated as a full date, so
 * the `NaN` branch is unreachable in practice and is preserved only to keep behaviour
 * identical for a malformed input.
 *
 * @param iso - Date string in `YYYY-MM-DD` form.
 * @returns The three parts as numbers; `NaN` for any part the string did not contain.
 */
export const splitIsoDate = (iso: string): { year: number; month: number; day: number } => {
  const [year = '', month = '', day = ''] = iso.split('-')
  return { year: parseInt(year), month: parseInt(month), day: parseInt(day) }
}

/**
 * Renders a `Date` as its UTC `YYYY-MM-DD` day, the form every date `<input>` and every
 * date-keyed lookup in the admin uses.
 *
 * `Date.toISOString()` always contains a `T`, so the first split part is always present —
 * but `noUncheckedIndexedAccess` cannot know that, and spelling the fallback out at each of
 * the ~18 call sites would bury the intent. Callers must pass a valid `Date`; an invalid one
 * makes `toISOString()` throw here exactly as it did inline.
 *
 * @param date - The date to render. Must be a valid `Date`.
 * @returns The UTC calendar day as `YYYY-MM-DD`.
 */
export const toIsoDay = (date: Date): string => date.toISOString().split('T')[0] ?? ''
