/**
 * Helpers for the client auth, layout and dashboard screens: the `Logo` stand-in, the Nuxt
 * auto-imports those screens use, and a page mount that includes both.
 *
 * `mountClient` and `mountPage` (see `mountClient.ts`) register the kit and every
 * `components/client` component; `Logo` lives outside that folder and reads the brand
 * settings, so it is replaced by a plain image here.
 *
 * @module tests/helpers/authScreens
 */

import type { VueWrapper } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import type { Component } from 'vue'
import { vi } from 'vitest'
import { mountClient, mountPage } from '~/tests/helpers/mountClient'
import type { MountWithKitOptions } from '~/tests/helpers/mountWithKit'
import { stubLocalePath, stubRoute } from '~/tests/helpers/runtimeStubs'

/** `Logo` stand-in: an image carrying a test id. */
export const APP_LOGO_STUB = defineComponent({
  name: 'Logo',
  setup() {
    return () => h('img', { 'data-testid': 'app-logo' })
  },
})

/** What a test can change about the stood-in runtime. */
export interface AuthRuntimeOptions {
  /** The route query (`route.query`). */
  query?: Record<string, string>
  /** The full path (`route.fullPath`). */
  fullPath?: string
  /** Whether the deployment signs in through the platform SSO. */
  isSso?: boolean
  /** The public runtime config. */
  publicConfig?: Record<string, unknown>
}

/** The spies a test reads after mounting a screen. */
export interface AuthRuntimeSpies {
  /** `router.push`. */
  push: ReturnType<typeof vi.fn>
}

/**
 * Stands in for the auto-imports the auth screens use that `vitest.setup.ts` does not cover.
 *
 * @param options - Route, SSO flag and public config for this test.
 * @returns The router spy.
 */
export const stubAuthRuntime = (options: AuthRuntimeOptions = {}): AuthRuntimeSpies => {
  const push = vi.fn()
  stubRoute(options)
  vi.stubGlobal('useRouter', () => ({ push, replace: vi.fn(), back: vi.fn() }))
  stubLocalePath()
  vi.stubGlobal('useRuntimeConfig', () => ({ public: options.publicConfig ?? {} }))
  vi.stubGlobal('useAuthMode', () => ({ mode: ref(options.isSso ? 'sso' : 'local'), isSso: ref(Boolean(options.isSso)), signInHref: ref('/x') }))
  vi.mocked(navigateTo).mockClear()
  return { push }
}

/**
 * Mounts a component with the `Logo` stand-in registered.
 *
 * @param component The component under test.
 * @param options Props, slots, attributes and stubs, as for {@link mountClient}.
 * @returns The Vue Test Utils wrapper.
 */
export const mountWithLogo = (component: Component, options: MountWithKitOptions = {}): VueWrapper =>
  mountClient(component, { ...options, components: { Logo: APP_LOGO_STUB, ...options.components } })

/**
 * Mounts a page inside `Suspense` with the `Logo` stand-in registered.
 *
 * @param page The page under test.
 * @param components Further components the page uses, e.g. the plugin slot of a client page.
 * @returns The wrapper, after the page has settled.
 */
export const mountAuthPage = (page: Component, components: Record<string, Component> = {}): Promise<VueWrapper> =>
  mountPage(page, { components: { Logo: APP_LOGO_STUB, ...components } })
