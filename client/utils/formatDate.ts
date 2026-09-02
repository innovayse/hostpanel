/**
 * Rendering a date the API sent, in the language the visitor is reading.
 *
 * Three things this file exists to get right, in the order they cost us:
 *
 * 1. **Nothing unreadable ever reaches the page.** `/client/services/16` shipped
 *    `2026-09-08T08:19:37.756135+00:00` straight into a description list, and its neighbour
 *    rendered nothing at all because `services/[id]/index.get.ts` hardcodes `regdate: ''`.
 *    Both go through {@link EMPTY_DATE} here. A missing or unparseable value is never
 *    `Invalid Date` and never the raw string — those are the two failures that actually
 *    shipped.
 * 2. **A calendar date is not a moment.** A renewal date is a day on a bill; a ticket reply is
 *    an instant. {@link formatDate} answers the first, {@link formatDateTime} the second, and
 *    they differ in more than the presence of a clock — see the timezone note below.
 * 3. **The locale is the visitor's, not the server's.** The portal ships en/ru/hy and date-fns
 *    carries all three, so an Armenian customer reads an Armenian month name.
 *
 * ## Timezone: why the calendar path never touches `Date` arithmetic
 *
 * `new Date('2026-09-08')` is UTC midnight, so a browser west of Greenwich renders it as the
 * 7th — a renewal date silently one day early, on the screen a customer pays from. The
 * offset-carrying form has the same hazard from the other side.
 *
 * {@link formatDate} therefore reads the calendar fields **out of the string** with
 * {@link ISO_CALENDAR_DAY} and builds a local-midnight `Date` from them. The backend already
 * chose which day it meant when it wrote `2026-09-08`; re-deriving that day from an instant
 * and a viewer's zone can only ever move it. There is no zone conversion in this path at all,
 * which is also why no `date-fns-tz` dependency was added.
 *
 * {@link formatDateTime} does the opposite, deliberately: a moment *should* be shown in the
 * reader's own zone, so it parses normally and lets the platform convert.
 *
 * ## Locale: an argument, resolved per render, never cached
 *
 * `useI18n()` is a composable and cannot be called from a util. The tempting fix — a
 * module-level `let currentLocale` written by a plugin — is an SSR bug: one Nitro process
 * serves every visitor, so the last request to set it decides what the next one renders.
 *
 * So `locale` is a plain parameter. When a caller omits it, {@link resolveLocale} asks
 * `useNuxtApp()`, which Nuxt scopes to the request being rendered rather than to the module,
 * and falls back to `en` when there is no Nuxt instance (unit tests, and any call outside a
 * render). Nothing is memoised, so nothing can leak between requests.
 *
 * @module utils/formatDate
 */

import { format, isValid, parseISO } from 'date-fns'
// Imported from their own paths rather than the `date-fns/locale` barrel. The barrel
// re-exports 98 locales, and Vite pre-bundles the whole of it in development — three
// named imports still cost all 98 modules there, which is enough to stall the first
// page load on a slow machine. Production tree-shakes either way; this makes the two
// agree, and states outright which three the portal actually ships.
import { enUS } from 'date-fns/locale/en-US'
import { hy } from 'date-fns/locale/hy'
import { ru } from 'date-fns/locale/ru'
import type { Locale } from 'date-fns'

/**
 * What a date renders as when there is nothing to render.
 *
 * The em dash the portal already uses for an absent value — `pages/client/domains/[id].vue`
 * and `pages/client/services/index.vue` both wrote it inline before this file existed, and
 * this is now the one copy of it.
 */
export const EMPTY_DATE = '—'

/**
 * The date-fns locale for each language the portal ships, keyed by `@nuxtjs/i18n` code.
 *
 * The three codes match `nuxt.config.ts`'s `i18n.locales`. Anything else — a locale added to
 * the config without a line here — resolves to `enUS` rather than throwing, because a month
 * name in the wrong language is a smaller failure than a blank service page.
 */
const DATE_LOCALES: Record<string, Locale> = { en: enUS, ru, hy }

/**
 * Leading `YYYY-MM-DD` of an ISO-8601 value, whether or not a time follows it.
 *
 * Matches both shapes the API sends: the bare `2026-09-08` a WHMCS-derived field carries, and
 * the `2026-09-08T08:19:37.756135+00:00` a .NET `DateTimeOffset` serialises to.
 */
const ISO_CALENDAR_DAY = /^(\d{4})-(\d{2})-(\d{2})/

/**
 * Zero-ish dates that mean "unset" rather than a year in antiquity.
 *
 * `0000-00-00` is MySQL's absent date and reaches the portal through the WHMCS-shaped fields;
 * `0001-01-01` is `default(DateTime)` serialised. Both already had `startsWith` guards
 * scattered across `pages/client/dashboard.vue` and `pages/client/domains/index.vue`; the
 * check lives here now so a new call site inherits it instead of re-deriving it.
 */
const UNSET_YEARS = ['0000', '0001']

/**
 * Reads the language to format in, without a module-level cache.
 *
 * @param locale - What the caller passed, if anything.
 * @returns The caller's locale, else the locale of the request currently rendering, else `en`.
 */
const resolveLocale = (locale?: string): string => {
  if (locale) return locale

  try {
    // `useNuxtApp()` is scoped to the running request during SSR, so this is per-visitor and
    // not shared state. It throws when there is no Nuxt instance — a unit test, or a call from
    // outside a render — which is exactly when `en` is the right answer.
    const i18n = (useNuxtApp() as { $i18n?: { locale?: { value?: string } } }).$i18n
    return i18n?.locale?.value ?? 'en'
  }
  catch {
    return 'en'
  }
}

/**
 * Maps an `@nuxtjs/i18n` locale code onto its date-fns locale.
 *
 * @param locale - Locale code, e.g. `hy`. A regional tag such as `hy-AM` is accepted and its
 *                 language subtag used, since `nuxt.config.ts` carries both forms.
 * @returns The date-fns locale, defaulting to `enUS`.
 */
const dateLocale = (locale?: string): Locale =>
  DATE_LOCALES[resolveLocale(locale).split('-')[0]!] ?? enUS

/**
 * Parses a value the API sent into a `Date`, or `null` when it does not denote one.
 *
 * @param value - Whatever the field held: an ISO string, a `Date`, an epoch, or nothing.
 * @param calendarOnly - True to read the calendar day out of an ISO string rather than convert
 *                       an instant into the viewer's zone. See the timezone note on this
 *                       module.
 * @returns The parsed date, or `null` for an absent, sentinel or unparseable value.
 */
const toDate = (value: string | number | Date | null | undefined, calendarOnly: boolean): Date | null => {
  if (value === null || value === undefined || value === '') return null

  if (typeof value === 'string') {
    const day = ISO_CALENDAR_DAY.exec(value)
    if (day && UNSET_YEARS.includes(day[1]!)) return null

    // The string already names the day the backend meant. Building local midnight from those
    // three numbers is the whole timezone fix: no instant is constructed, so no zone can move
    // the answer.
    if (day && calendarOnly) return new Date(Number(day[1]), Number(day[2]) - 1, Number(day[3]))

    const parsed = parseISO(value)
    // `parseISO` answers Invalid Date rather than throwing, and a non-ISO string — a WHMCS
    // free-text field, say — lands here.
    return isValid(parsed) ? parsed : null
  }

  const parsed = value instanceof Date ? value : new Date(value)
  return isValid(parsed) ? parsed : null
}

/**
 * Formats a calendar date — a renewal date, a registration date, an expiry.
 *
 * The day is taken from the value as written, never re-derived from an instant, so it cannot
 * shift by one in a westerly timezone. Use {@link formatDateTime} for something that happened
 * at a moment.
 *
 * @param value - The field as the API sent it. Anything absent, sentinel or unparseable is
 *                fine to pass — that is the case this function exists for.
 * @param locale - `@nuxtjs/i18n` locale code. Omit inside a component and the current request's
 *                 locale is used.
 * @returns The localised date, or {@link EMPTY_DATE} when there is no date to show.
 */
export const formatDate = (value: string | number | Date | null | undefined, locale?: string): string => {
  const date = toDate(value, true)
  // `PP` is date-fns' medium localised date — "Sep 8, 2026", "8 сент. 2026 г.", "8 սեպ, 2026 թ."
  // No day-of-week and no ambiguous all-numeric order, which is what a billing date needs.
  return date ? format(date, 'PP', { locale: dateLocale(locale) }) : EMPTY_DATE
}

/**
 * Formats a moment — a ticket reply, an invoice payment, a last-updated stamp.
 *
 * Converted into the reader's own timezone, which is the opposite of {@link formatDate} and is
 * correct for the same reason: "when did this happen" is a question about an instant, and the
 * reader's clock is the one they will compare it against.
 *
 * @param value - The field as the API sent it, absent and unparseable values included.
 * @param locale - `@nuxtjs/i18n` locale code. Omit inside a component to use the request's.
 * @returns The localised date and time, or {@link EMPTY_DATE} when there is none to show.
 */
export const formatDateTime = (value: string | number | Date | null | undefined, locale?: string): string => {
  const date = toDate(value, false)
  // `PPp` is `PP` plus a localised short time. The seconds and microseconds the backend sends
  // are deliberately dropped: nothing on these screens is decided at that resolution.
  return date ? format(date, 'PPp', { locale: dateLocale(locale) }) : EMPTY_DATE
}
