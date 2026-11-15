/**
 * Helpers for testing the public pages (contact, FAQ, knowledge base, announcements, legal texts).
 *
 * A Nuxt page leans on auto-imports that do not exist in a unit test (`useSeo`, `useLocalePath`,
 * `createError` and the rest) and awaits `useAsyncData` at the top of its setup, which makes it an async
 * component. This module stands in for the first and wraps the second in `<Suspense>`, so a test
 * mounts the real page against the real kit and a real Pinia and reads what it renders.
 *
 * @module tests/helpers/publicPage
 */

import { config } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { storeToRefs } from 'pinia'
import { ref, watchEffect } from 'vue'
import type { Component } from 'vue'
import { vi } from 'vitest'
import { mountAsyncPage } from '~/tests/helpers/mountWithKit'
import type { MountWithKitOptions } from '~/tests/helpers/mountWithKit'
import { stubLocalePath, stubRoute, TEST_LOCALE_PREFIX } from '~/tests/helpers/runtimeStubs'

/** The spies a test reads after mounting a page. */
export interface PublicPageSpies {
  /** `useSeo`: called by the pages that set canonical, hreflang and social tags. */
  useSeo: ReturnType<typeof vi.fn>
  /** `useSeoMeta`: called by the pages that set only title, description and robots. */
  useSeoMeta: ReturnType<typeof vi.fn>
  /** `injectSchema`: the structured data a page publishes. */
  injectSchema: ReturnType<typeof vi.fn>
  /** `createError`: how a page raises a 404 for a route it does not serve. */
  createError: ReturnType<typeof vi.fn>
}

/** What a test can change about the stood-in runtime. */
export interface PublicPageOptions {
  /** The route parameters (`route.params`). */
  params?: Record<string, string>
  /** The route query (`route.query`). */
  query?: Record<string, string | string[]>
  /** `tm` of vue-i18n: returns the raw message tree a page reads arrays from. */
  tm?: (key: string) => unknown
  /** Whether the page is in dark mode. */
  dark?: boolean
}

/**
 * Stands in for the Nuxt auto-imports the public pages use.
 *
 * @param options - Route, `tm` and colour mode for this test.
 * @returns The spies a test asserts on.
 */
export const stubPublicPageRuntime = (options: PublicPageOptions = {}): PublicPageSpies => {
  const spies: PublicPageSpies = {
    useSeo: vi.fn(() => ({ baseUrl: 'https://portal.test', canonicalUrl: ref('https://portal.test/canonical') })),
    useSeoMeta: vi.fn(),
    injectSchema: vi.fn(),
    createError: vi.fn((details: { statusCode: number, statusMessage?: string }) =>
      Object.assign(new Error(details.statusMessage ?? ''), { statusCode: details.statusCode })),
  }

  vi.stubGlobal('watchEffect', watchEffect)
  vi.stubGlobal('storeToRefs', storeToRefs)
  stubLocalePath(TEST_LOCALE_PREFIX)
  vi.stubGlobal('useSeo', spies.useSeo)
  vi.stubGlobal('useSeoMeta', spies.useSeoMeta)
  vi.stubGlobal('useHead', vi.fn())
  vi.stubGlobal('useSchemaOrg', () => ({
    organizationSchema: () => ({ '@type': 'Organization' }),
    localBusinessSchema: () => ({ '@type': 'LocalBusiness' }),
    websiteSchema: () => ({ '@type': 'WebSite' }),
    productSchema: () => ({ '@type': 'Product' }),
    productFamilySchema: (family: unknown) => ({ '@type': 'Product', family }),
    breadcrumbSchema: (items: unknown[]) => ({ '@type': 'BreadcrumbList', items }),
    faqSchema: (items: unknown[]) => ({ '@type': 'FAQPage', items }),
    articleSchema: (article: object) => ({ '@type': 'Article', ...article }),
    injectSchema: spies.injectSchema,
  }))
  vi.stubGlobal('useAppColorMode', () => ({ isDark: ref(options.dark ?? false) }))
  vi.stubGlobal('createError', spies.createError)
  stubRoute(options)
  vi.stubGlobal('useI18n', () => ({
    t: (key: string) => key,
    te: () => false,
    tm: options.tm ?? (() => []),
    locale: ref('en'),
  }))

  return spies
}

/**
 * Registers components every following mount can resolve by name, the way Nuxt auto-registers
 * `components/` for a page.
 *
 * @param components - Components by the name a template uses.
 */
export const registerPageComponents = (components: Record<string, Component>): void => {
  config.global.components = { ...config.global.components, ...components }
}

/**
 * Mounts a page inside `<Suspense>` and waits for its async setup to settle.
 *
 * @param page - The page component.
 * @param options - Props, attributes and stubs for the mount; props go to the page, not the host.
 * @returns The wrapper around the host; the page's DOM is inside it.
 */
export const mountPage = (page: Component, options: MountWithKitOptions = {}): Promise<VueWrapper> =>
  mountAsyncPage(page, options)
