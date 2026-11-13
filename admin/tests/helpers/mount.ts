/**
 * Mount helpers for screen and component tests: the real kit, a real Pinia, the English catalogue
 * and a real (memory) router, with teleports inlined so dialogs are queryable. The API is stubbed
 * at the network level by the test (`stubFetch`, `stubRoutes`), never by mocking a store or
 * `useApi`.
 *
 * One builder (`createApp`) owns the plugin set; the exports differ only in whether they settle
 * the first requests and what they hand back.
 */
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter, matchedRouteKey } from 'vue-router'
import type { RouteRecordRaw, Router } from 'vue-router'
import { computed } from 'vue'
import type { Component, DefineComponent, Plugin } from 'vue'
import { createTestI18n } from './i18n'

/** How many times to let pending promises settle after mounting. */
const SETTLE_ROUNDS = 8

/** Pattern matching any path. */
const CATCH_ALL = '/:pathMatch(.*)*'

/** Stand-in page for every route a screen links to. */
const Blank: Component = { render: () => null }

/** A mounted component with the router it runs in. */
export interface MountedScreen {
  /** The mounted wrapper. */
  wrapper: VueWrapper
  /** The memory router, to assert navigation. */
  router: Router
}

/** Options of {@link mountWithApp}. */
export interface MountWithAppOptions {
  /** Props handed to the component. */
  props?: Record<string, unknown>
  /** Components to stub by name (a chart cannot draw without a canvas, for example). */
  stubs?: Record<string, boolean>
  /** Slot content by slot name, as markup. */
  slots?: Record<string, string>
}

/** Where the mounted view sits in the router. */
export interface MountViewOptions {
  /** Path the router is at when the view mounts; `/` by default. */
  path?: string
  /** Route pattern matching `path` (for example `/support/tickets/:id`); matches anything by default. */
  pattern?: string
}

/**
 * Builds the memory router and the plugin set every mount shares.
 *
 * @param routes - Routes of the memory router.
 * @param stubs - Extra component stubs on top of the inlined teleport.
 * @returns The router and the `global` mount option.
 */
const createApp = (
  routes: RouteRecordRaw[],
  stubs: Record<string, boolean> = {},
): { router: Router; global: { plugins: Plugin[]; stubs: Record<string, boolean>; provide: Record<symbol, unknown> } } => {
  const pinia = createPinia()
  setActivePinia(pinia)
  const router = createRouter({ history: createMemoryHistory(), routes })
  // A real `<RouterView>` hands its route record down; the component is mounted bare, so the
  // record is provided here. Without it `onBeforeRouteLeave` warns that no record is active.
  const matched = computed(() => router.currentRoute.value.matched[0])
  return {
    router,
    global: { plugins: [createTestI18n(), pinia, router], stubs: { teleport: true, ...stubs }, provide: { [matchedRouteKey as symbol]: matched } },
  }
}

/**
 * Lets pending promises settle: a screen's first requests chain (a list, then what the list needs),
 * and a stubbed response body takes a tick or two to read.
 *
 * @returns Resolves after several flush rounds.
 */
const settle = async (): Promise<void> => {
  for (let round = 0; round < SETTLE_ROUNDS; round += 1) {
    await flushPromises()
  }
}

/**
 * Mounts a component and waits for its first requests to settle.
 *
 * @param component - The component to mount.
 * @param props - Props for it.
 * @param route - Path the router starts on.
 * @param routePattern - Route pattern the component is mounted under (to carry params such as `:id`).
 * @returns The wrapper and the router.
 */
export const mountScreen = async (
  component: Component,
  props: Record<string, unknown> = {},
  route = '/',
  routePattern = CATCH_ALL,
): Promise<MountedScreen> => {
  // The screen's own pattern, plus a stand-in for every other page it links to (breadcrumbs, back
  // links, redirects after save), so a link to a page that is not under test is not a warning.
  const routes: RouteRecordRaw[] = [{ path: routePattern, component: Blank }]
  if (routePattern !== CATCH_ALL) {
    routes.push({ path: CATCH_ALL, component: Blank })
  }
  const { router, global } = createApp(routes)
  await router.push(route)
  await router.isReady()
  // The helper takes any component and loose props, so the component is seen as a plain `DefineComponent`.
  const wrapper = mount(component as DefineComponent, { props, global })
  await settle()
  return { wrapper, router }
}

/**
 * Mounts a view at a path and waits for its first requests to settle.
 *
 * @param component - The view to mount.
 * @param options - The router position.
 * @returns The mounted wrapper, after pending promises have flushed.
 */
export const mountView = async (component: Component, options: MountViewOptions = {}): Promise<VueWrapper> =>
  (await mountScreen(component, {}, options.path ?? '/', options.pattern)).wrapper

/**
 * Mounts a screen registered under its own route at a concrete URL, without waiting for requests.
 *
 * @param component - The screen.
 * @param routePath - Route pattern the screen is registered under, e.g. `/orders/:id`.
 * @param url - Concrete URL to open, e.g. `/orders/8`.
 * @returns The wrapper and the router.
 */
export const mountRouted = async (component: Component, routePath: string, url: string): Promise<MountedScreen> => {
  const { router, global } = createApp([
    { path: routePath, component },
    { path: '/:rest(.*)*', component: Blank },
  ])
  await router.push(url)
  await router.isReady()
  const wrapper = mount(component, { global })
  return { wrapper, router }
}

/**
 * Mounts a component inside the app plugins, synchronously.
 *
 * @param component - The component.
 * @param options - Props, stubs and slot content.
 * @returns The mounted wrapper.
 */
export const mountWithApp = (component: Component, options: MountWithAppOptions = {}): VueWrapper => {
  const { global } = createApp([{ path: '/:pathMatch(.*)*', component: Blank }], options.stubs)
  return mount(component, { props: options.props, slots: options.slots, global })
}

/**
 * Mounts a screen registered under its own route, opened at that same path, and waits for its
 * first load to settle.
 *
 * @param component - The screen.
 * @param path - Route path it is registered and opened under.
 * @returns The wrapper, after pending promises have flushed.
 */
export const mountRoutedSettled = async (component: Component, path: string): Promise<VueWrapper> => {
  const { wrapper } = await mountRouted(component, path, path)
  await flushPromises()
  return wrapper
}
