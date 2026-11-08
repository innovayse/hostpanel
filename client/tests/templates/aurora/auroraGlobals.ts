/**
 * Test support for the aurora template: stands in for the Nuxt composables its components call.
 *
 * Under Nuxt, `useLocalePath`, `usePortalSettings`, `useAuthMode`, `useBrandLogo`,
 * `useSiteIdentity`, `usePortalApps`, `useAuthStore`, `useI18n` (with `tm`/`rt`), `toRefs` and `storeToRefs` are
 * auto-imported. Vitest has no such step, so each is defined here as a global.
 *
 * @module tests/templates/aurora/auroraGlobals
 */

import { ref, toRefs } from 'vue'
import type { Ref } from 'vue'
import { storeToRefs } from 'pinia'
import { vi } from 'vitest'
import { DEFAULT_SITE_NAME, stubTemplateGlobals } from '~/tests/helpers/templateGlobals'
import type { TemplateGlobalsOptions } from '~/tests/helpers/templateGlobals'
import type { PlanCard } from '~/types/plancard'
import type { PortalApp } from '~/types/portalapp'

/** The signed-in customer the auth stand-in reports. */
export interface AuroraTestUser {
  /** Given name. */
  firstName: string
  /** Family name. */
  lastName: string
  /** E-mail address. */
  email: string
}

/** Options for {@link stubAuroraGlobals}: the shared template options plus the aurora-only ones. */
export interface AuroraGlobalsOptions extends TemplateGlobalsOptions {
  /** The customer the auth store reports; undefined means signed out. */
  user?: AuroraTestUser
  /** Apps the launcher composable lists. */
  apps?: PortalApp[]
  /** Locale messages by key, as `tm()` returns them (the FAQ and testimonial lists). */
  messages?: Record<string, unknown>
}

/** What {@link stubAuroraGlobals} hands back so a test can assert on the auth calls. */
export interface AuroraGlobalsHandle {
  /** Spy standing in for the auth store's `signOut`. */
  signOut: ReturnType<typeof vi.fn>
  /** Spy standing in for the auth store's `fetchUser`. */
  fetchUser: ReturnType<typeof vi.fn>
  /** The profile ref the auth store reports. */
  user: Ref<AuroraTestUser | null>
}

/**
 * Defines the aurora components' auto-imported helpers as globals.
 *
 * @param options Settings, logo, site name, session and apps to report.
 * @returns The auth spies and profile ref, for assertions.
 */
export const stubAuroraGlobals = (options: AuroraGlobalsOptions = {}): AuroraGlobalsHandle => {
  stubTemplateGlobals({ logoAlt: DEFAULT_SITE_NAME, ...options })
  /** The signed in user the stubbed session reports, or null when signed out. */
  const user = ref<AuroraTestUser | null>(options.user ?? null)
  const signOut = vi.fn(async () => undefined)
  const fetchUser = vi.fn(async () => undefined)
  const messages = options.messages ?? {}
  vi.stubGlobal('useI18n', () => ({
    t: (key: string) => key,
    te: () => false,
    tm: (key: string) => messages[key] ?? [],
    rt: (value: string) => value,
    locale: ref('en'),
  }))
  vi.stubGlobal('toRefs', toRefs)
  vi.stubGlobal('storeToRefs', storeToRefs)
  vi.stubGlobal('usePortalApps', () => ({ apps: ref(options.apps ?? []) }))
  vi.stubGlobal('useAuthStore', () => ({
    user,
    isLoggedIn: ref(Boolean(options.user)),
    fetchUser,
    signOut,
    logout: signOut,
  }))
  return { signOut, fetchUser, user }
}

/** Plan fixtures shared by the plan, comparison and page tests. */
export const PLANS: PlanCard[] = [
  {
    id: 1,
    name: 'Starter',
    description: 'For a first site',
    features: ['1 site', '10 GB SSD'],
    isFree: false,
    priceMonthly: '$4.99',
    priceAnnual: '$3.99',
    cyclePrices: { monthly: '$4.99', annual: '$3.99' },
    href: '/configure/1',
  },
  {
    id: 2,
    name: 'Business',
    description: '',
    features: [],
    isFree: false,
    priceMonthly: '$9.99',
    priceAnnual: '$7.99',
    cyclePrices: { monthly: '$9.99', quarterly: '$8.99', annual: '$7.99' },
    href: '/configure/2',
  },
  {
    id: 3,
    name: 'Free',
    description: 'Try it',
    features: ['500 MB'],
    isFree: true,
    priceMonthly: '$0',
    priceAnnual: '$0',
    cyclePrices: { monthly: '$0', annual: '$0' },
    href: '/configure/3',
  },
]
