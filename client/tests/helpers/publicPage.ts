/**
 * Helpers for testing the public pages (contact, FAQ, knowledge base, announcements, legal texts).
 *
 * A Nuxt page leans on auto-imports that do not exist in a unit test (`useSeo`, `useLocalePath`,
 * `createError` and the rest) and awaits `useAsyncData` at the top of its setup, which makes it an async
 * component. This module registers the real composables of ours under their auto-import names, stands in
 * only for the Nuxt and Vue globals, and wraps the page in `<Suspense>`, so a test mounts the real page against
 * the real kit and a real Pinia and reads what it renders. What a page tells the head (title, canonical link,
 * structured data) is read back through {@link seoTags}, {@link canonicalOf} and {@link publishedSchemas}.
 *
 * @module tests/helpers/publicPage
 */

import { config } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { storeToRefs } from 'pinia'
import { ref, watchEffect } from 'vue'
import type { Component } from 'vue'
import { vi } from 'vitest'
import { useAppColorMode } from '~/composables/useAppColorMode'
import { useContactDetails } from '~/composables/useContactDetails'
import { useContactHours } from '~/composables/useContactHours'
import { usePortalSettings } from '~/composables/usePortalSettings'
import { useSchemaOrg } from '~/composables/useSchemaOrg'
import { useSeo } from '~/composables/useSeo'
import { useSiteIdentity } from '~/composables/useSiteIdentity'
import { mountAsyncPage } from '~/tests/helpers/mountWithKit'
import type { MountWithKitOptions } from '~/tests/helpers/mountWithKit'
import { stubLocalePath, stubRoute, TEST_LOCALE_PREFIX } from '~/tests/helpers/runtimeStubs'
import { ColorMode } from '~/types/colormode'
import { COLOR_MODE_COOKIE } from '~/utils/colorMode'

/** The public address the stood-in runtime config reports, which every canonical URL starts with. */
export const PUBLIC_PAGE_BASE_URL = 'https://portal.test'

/** The spies a test reads after mounting a page. */
export interface PublicPageSpies {
  /** `useSeoMeta`: called by the pages that set only title, description and robots, and by the real `useSeo`. */
  useSeoMeta: ReturnType<typeof vi.fn>
  /** `useHead`: called by the real `useSeo` (links, keywords) and `injectSchema` (structured data). */
  useHead: ReturnType<typeof vi.fn>
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
  /** Whether the visitor chose the dark colour mode; the light one otherwise. */
  dark?: boolean
  /** The deployment's public address the runtime config reports; {@link PUBLIC_PAGE_BASE_URL} unless a test says otherwise. */
  baseUrl?: string
  /** Further public runtime config the page reads, such as the billing portal address. */
  publicConfig?: Record<string, unknown>
}

/** One document of the head input a `useHead` call produced, as far as the readers below need. */
interface HeadInput {
  /** The `<link>` tags. */
  link?: Array<{ rel: string, href: string }>
  /** The `<script>` tags. */
  script?: Array<{ type: string, innerHTML: string }>
}

/**
 * Stands in for the Nuxt and Vue auto-imports the public pages use, and registers the real composables of ours.
 *
 * @param options - Route, `tm`, colour mode, public address and runtime config for this test.
 * @returns The spies a test asserts on.
 */
export const stubPublicPageRuntime = (options: PublicPageOptions = {}): PublicPageSpies => {
  const spies: PublicPageSpies = {
    useSeoMeta: vi.fn(),
    useHead: vi.fn(),
    createError: vi.fn((details: { statusCode: number, statusMessage?: string }) =>
      Object.assign(new Error(details.statusMessage ?? ''), { statusCode: details.statusCode })),
  }

  vi.stubGlobal('watchEffect', watchEffect)
  vi.stubGlobal('storeToRefs', storeToRefs)
  stubLocalePath(TEST_LOCALE_PREFIX)
  vi.stubGlobal('useRuntimeConfig', () => ({ public: { baseUrl: options.baseUrl ?? PUBLIC_PAGE_BASE_URL, ...options.publicConfig } }))
  // The colour mode is the visitor's choice, kept in the cookie, so that is what a test sets; any other cookie is unset.
  vi.stubGlobal('useCookie', (name: string) => ref(name === COLOR_MODE_COOKIE ? (options.dark ? ColorMode.Dark : ColorMode.Light) : null))
  vi.stubGlobal('usePortalSettings', usePortalSettings)
  vi.stubGlobal('useSiteIdentity', useSiteIdentity)
  vi.stubGlobal('useContactDetails', useContactDetails)
  vi.stubGlobal('useContactHours', useContactHours)
  vi.stubGlobal('useAppColorMode', useAppColorMode)
  vi.stubGlobal('useSeo', useSeo)
  vi.stubGlobal('useSchemaOrg', useSchemaOrg)
  vi.stubGlobal('useSeoMeta', spies.useSeoMeta)
  vi.stubGlobal('useHead', spies.useHead)
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
 * Reads the head input of every `useHead` call, resolving the getters the real composables pass.
 *
 * @param spies - The spies of the mounted page.
 * @returns The head inputs, oldest call first.
 */
const headInputs = (spies: PublicPageSpies): HeadInput[] =>
  spies.useHead.mock.calls.map(([input]) => (typeof input === 'function' ? input() : input) as HeadInput)

/**
 * Reads the tags the page set through `useSeoMeta` (its own call or the real `useSeo`'s), getters resolved.
 *
 * @param spies - The spies of the mounted page.
 * @returns The tags of the latest call, empty when no tag was set.
 */
export const seoTags = (spies: PublicPageSpies): Record<string, unknown> => {
  const latest = spies.useSeoMeta.mock.calls.at(-1)?.[0] as Record<string, unknown> | undefined
  return Object.fromEntries(Object.entries(latest ?? {}).map(([name, value]) => [name, typeof value === 'function' ? value() : value]))
}

/**
 * Reads the canonical address the real `useSeo` put in the head.
 *
 * @param spies - The spies of the mounted page.
 * @returns The canonical URL, undefined when the page set none.
 */
export const canonicalOf = (spies: PublicPageSpies): string | undefined =>
  headInputs(spies).flatMap((input) => input.link ?? []).find((link) => link.rel === 'canonical')?.href

/**
 * Reads the structured data the real `injectSchema` put in the head. A page that publishes from a reactive effect
 * publishes again each time its data changes, and the latest publication is the one that stands.
 *
 * @param spies - The spies of the mounted page.
 * @returns The JSON-LD documents of the latest publication, in the order they were published.
 */
export const publishedSchemas = (spies: PublicPageSpies): Array<Record<string, unknown>> =>
  (headInputs(spies).filter((input) => input.script !== undefined).at(-1)?.script ?? [])
    .map((script) => JSON.parse(script.innerHTML) as Record<string, unknown>)

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
