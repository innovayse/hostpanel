/**
 * Test support for the aurora template: the real composables and stores its components call, fed by the stub server.
 *
 * Under Nuxt, `useLocalePath`, `usePortalSettings`, `useAuthMode`, `useBrandLogo`, `useSiteIdentity`,
 * `usePortalApps`, `useAuthStore`, `useI18n` (with `tm`/`rt`), `toRefs` and `storeToRefs` are auto-imported.
 * Vitest has no such step, so the real composables and the real auth store of ours are registered under their
 * auto-import names, the settings and the signed-in account are answered by the stub server, and only the Nuxt
 * and Vue globals that do not exist in a unit test are stood in for.
 *
 * @module tests/templates/aurora/auroraGlobals
 */

import { ref, toRefs } from 'vue'
import { storeToRefs } from 'pinia'
import { vi } from 'vitest'
import { usePortalApps } from '~/composables/usePortalApps'
import { SessionCookie } from '~/constants/sessionCookie'
import { useAuthStore } from '~/stores/auth'
import { AccountEndpoint } from '~/tests/helpers/accountPaths'
import { makeClientUser } from '~/tests/helpers/clientUser'
import { readOf } from '~/tests/helpers/stubRoutes'
import { useTemplateGlobals } from '~/tests/helpers/templateGlobals'
import type { TemplateGlobals, TemplateGlobalsOptions } from '~/tests/helpers/templateGlobals'
import type { ClientUser } from '~/types/clientuser'
import type { PlanCard } from '~/types/plancard'

/** The signed-in customer the stub server answers the identity read with. */
export type AuroraTestUser = Pick<ClientUser, 'firstName' | 'lastName' | 'email'>

/** Options for {@link AuroraGlobals.loadGlobals}: the shared template options plus the aurora-only ones. */
export interface AuroraGlobalsOptions extends TemplateGlobalsOptions {
  /** The customer the session reports; undefined means signed out. */
  user?: AuroraTestUser
  /** Locale messages by key, as `tm()` returns them (the FAQ and testimonial lists). */
  messages?: Record<string, unknown>
}

/** What {@link useAuroraGlobals} hands the test file. */
export interface AuroraGlobals extends TemplateGlobals {
  /**
   * Registers the real aurora composables and stores, answers the settings and the session, and stands in for the
   * Nuxt and Vue globals a unit test lacks.
   *
   * @param options - Settings, logo, site name, session and messages to report.
   * @returns Resolves once the settings are loaded.
   */
  loadGlobals: (options?: AuroraGlobalsOptions) => Promise<void>
}

/**
 * Registers the hooks every test of an aurora component needs; call it once at the top of the test file, then
 * call `loadGlobals` in each test. A signed-in customer is a readable session cookie plus the identity the stub
 * answers, which the real auth store reads when a component asks for it.
 *
 * @returns The stub accessor, the reply table and the loader of the aurora globals.
 */
export const useAuroraGlobals = (): AuroraGlobals => {
  const template = useTemplateGlobals()

  /**
   * Loads the template settings, then registers the aurora composables and the session.
   *
   * @param options - Settings, logo, site name, session and messages to report.
   * @returns Resolves once the settings are loaded.
   */
  const loadGlobals = async (options: AuroraGlobalsOptions = {}): Promise<void> => {
    const { user, messages = {}, ...templateOptions } = options
    await template.loadGlobals(templateOptions)
    if (user) {
      template.replies[readOf(AccountEndpoint.Me)] = { body: makeClientUser(user) }
    }
    const stoodInI18n = useI18n()
    vi.stubGlobal('useI18n', () => ({
      ...stoodInI18n,
      t: (key: string) => key,
      te: () => false,
      tm: (key: string) => messages[key] ?? [],
      rt: (value: string) => value,
      locale: ref('en'),
    }))
    vi.stubGlobal('useCookie', (name: string) => ref(user && name === SessionCookie.Authed ? '1' : null))
    vi.stubGlobal('toRefs', toRefs)
    vi.stubGlobal('storeToRefs', storeToRefs)
    vi.stubGlobal('usePortalApps', usePortalApps)
    vi.stubGlobal('useAuthStore', useAuthStore)
  }

  return { ...template, loadGlobals }
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
