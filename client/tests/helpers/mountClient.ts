/**
 * Mount helper for the client-area components and pages: the kit plus every
 * `components/client/**` component, registered under the name Nuxt auto-imports it by.
 *
 * Nuxt names a component after its folders and its file (`components/client/services/ServiceCard.vue`
 * is `ClientServicesServiceCard`) and drops the folder parts a file name already starts with
 * (`services/setup/SetupWizard.vue` is `ClientServicesSetupWizard`). The client components render one
 * another under those names, so a test that mounts one needs them all registered. Pages are mounted
 * through {@link mountPage}, which wraps the page in `Suspense` because they `await` their data.
 *
 * @module tests/helpers/mountClient
 */

import { storeToRefs } from 'pinia'
import { vi } from 'vitest'
import type { VueWrapper } from '@vue/test-utils'
import type { Component } from 'vue'
import Logo from '~/components/Logo.vue'
import { mountAsyncPage, mountWithKit } from '~/tests/helpers/mountWithKit'
import { nuxtComponentName } from '~/tests/helpers/nuxtComponentName'
import type { MountWithKitOptions } from '~/tests/helpers/mountWithKit'
import { stubLocalePath, stubRoute } from '~/tests/helpers/runtimeStubs'

/** Every client component file, loaded once. */
const CLIENT_MODULES = import.meta.glob<{ default: Component }>('../../components/client/**/*.vue', { eager: true })

/**
 * Registers the client components under their auto-import names, with the real `Logo` the auth shell draws (it lives
 * outside `components/client`).
 *
 * @returns Map of component name to component.
 */
const buildClientComponents = (): Record<string, Component> => {
  const registry: Record<string, Component> = { Logo }
  for (const [path, module] of Object.entries(CLIENT_MODULES)) {
    const parts = path.replace('../../components/', '').replace(/\.vue$/, '').split('/')
    const file = parts.pop()! // a glob key always ends in a file name
    registry[nuxtComponentName(parts, file)] = module.default
  }
  return registry
}

/** The client components, built once for every mount. */
const CLIENT_COMPONENTS = buildClientComponents()

/**
 * Mounts a client component with the kit and every client component registered.
 *
 * @param component The component under test.
 * @param options Props, slots, attributes and stubs, as for {@link mountWithKit}.
 * @returns The Vue Test Utils wrapper.
 */
export const mountClient = (component: Component, options: MountWithKitOptions = {}): VueWrapper =>
  mountWithKit(component, { ...options, components: { ...CLIENT_COMPONENTS, ...options.components } })

/**
 * Mounts a page (an async-setup component) inside `Suspense` and waits until it has rendered.
 *
 * The kit and the client components are registered as for {@link mountClient}; `mountWithKit`
 * installs a fresh Pinia, so a page's stores start empty.
 *
 * @param page The page component.
 * @param options Extra global components or stubs for this mount, and optionally a Pinia the test filled.
 * @returns The wrapper around the page's rendered output.
 */
export const mountPage = (page: Component, options: Pick<MountWithKitOptions, 'components' | 'stubs' | 'pinia'> = {}): Promise<VueWrapper> =>
  mountAsyncPage(page, { components: { ...CLIENT_COMPONENTS, ...options.components }, stubs: options.stubs, pinia: options.pinia })

/** What a test can change about the stood-in runtime. */
export interface ClientRuntimeOptions {
  /** The route parameters (`route.params`). */
  params?: Record<string, string>
  /** The route query (`route.query`). */
  query?: Record<string, string>
}

/**
 * Stands in for the Nuxt auto-imports the client pages and components use that
 * `vitest.setup.ts` does not cover: the route, the locale-aware path builder, `useHead`,
 * `clearNuxtData` and Pinia's `storeToRefs`.
 *
 * @param options - Route parameters and query for this test.
 * @returns The `useHead` and `clearNuxtData` spies a test can assert on.
 */
export const stubClientRuntime = (options: ClientRuntimeOptions = {}): { useHead: ReturnType<typeof vi.fn>, clearNuxtData: ReturnType<typeof vi.fn> } => {
  const useHead = vi.fn()
  const clearNuxtData = vi.fn()
  stubRoute(options)
  stubLocalePath()
  vi.stubGlobal('useHead', useHead)
  vi.stubGlobal('clearNuxtData', clearNuxtData)
  vi.stubGlobal('storeToRefs', storeToRefs)
  return { useHead, clearNuxtData }
}
