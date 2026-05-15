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
  return d.toISOString().split('T')[0]
}
