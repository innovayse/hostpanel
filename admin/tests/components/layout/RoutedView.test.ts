/**
 * Mount tests for RoutedView on a real memory router with the navigation hooks installed: a click
 * on a screen still downloading shows the skeleton at once (its own schema when it has one, the generic stand-in when it
 * has none), only the level the navigation replaces
 * changes, and a download that fails or stalls for good shows the load error.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { defineComponent, h, onMounted } from 'vue'
import type { Component } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Router } from 'vue-router'
import RoutedView from '@/components/layout/RoutedView.vue'
import { LOAD_RETRIES, LOAD_TIMEOUT_MS, RETRY_STEP_MS, withRetry } from '@/lib/lazyRoute'
import { navigationAdmitted, navigationFailed, navigationSettled } from '@/middleware/navigation'
import { pinia } from '@/pinia'
import { useNavigationStore } from '@/stores/navigation'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema, TabsSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Builds a screen that renders one marked paragraph.
 *
 * @param id - Its `data-testid`.
 * @returns The component.
 */
const screen = (id: string): Component => defineComponent({ render: () => h('p', { 'data-testid': id }, id) })

/** A section layout with its own header and a nested routed slot, like the client area. */
const AreaLayout: Component = defineComponent({
  render: () => h('section', [h('h2', { 'data-testid': 'area-header' }, 'area'), h(RoutedView)]),
})

/** A loader whose download finishes only when the test says so. */
interface HeldLoader {
  /** The loader to put in a route record. */
  load: () => Promise<{ default: Component }>
  /** Delivers the screen. */
  arrive: () => void
}

/**
 * Builds a loader that holds its download until released.
 *
 * @param component - The screen it delivers.
 * @returns The loader and its release.
 */
const heldLoader = (component: Component): HeldLoader => {
  let release: () => void = () => undefined
  const download = new Promise<{ default: Component }>((resolve) => {
    release = () => resolve({ default: component })
  })
  return { load: () => download, arrive: () => release() }
}

/**
 * Mounts a RoutedView at the top of a router with the navigation hooks installed.
 *
 * @param slow - The loader of `/slow` and `/area/slow`.
 * @returns The wrapper and the router, already at `/`.
 */
const mountRouted = async (slow: () => Promise<{ default: Component }>): Promise<{ wrapper: VueWrapper; router: Router }> => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: screen('home') },
      { path: '/slow', component: slow },
      {
        path: '/area',
        component: AreaLayout,
        children: [
          { path: 'ready', component: screen('area-ready') },
          { path: 'slow', component: slow },
        ],
      },
    ],
  })
  router.beforeEach(navigationAdmitted)
  router.afterEach(navigationSettled)
  router.onError(navigationFailed)
  await router.push('/')
  const wrapper = mount(defineComponent({ render: () => h('div', [h(RoutedView)]) }), {
    global: { plugins: [createTestI18n(), pinia, router] },
  })
  await flushPromises()
  return { wrapper, router }
}

beforeEach(() => {
  // The hooks use the shared Pinia; start every test from an idle store.
  const store = useNavigationStore(pinia)
  store.pending = null
  store.failed = null
})

describe('RoutedView', () => {
  it('renders the current screen', async () => {
    const { wrapper } = await mountRouted(heldLoader(screen('slow')).load)

    expect(wrapper.find('[data-testid="home"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(false)
  })

  it('shows the skeleton at once while the next screen downloads, then the screen', async () => {
    const held = heldLoader(screen('slow'))
    const { wrapper, router } = await mountRouted(held.load)

    const navigation = router.push('/slow')
    await flushPromises()
    expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="home"]').exists()).toBe(false)

    held.arrive()
    await navigation
    await flushPromises()

    expect(wrapper.find('[data-testid="slow"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(false)
  })

  it('keeps the enclosing layout and shows the skeleton only in its routed slot', async () => {
    const held = heldLoader(screen('area-slow'))
    const { wrapper, router } = await mountRouted(held.load)
    await router.push('/area/ready')
    await flushPromises()

    const navigation = router.push('/area/slow')
    await flushPromises()

    expect(wrapper.find('[data-testid="area-header"]').exists()).toBe(true)
    expect(wrapper.find('section [data-testid="view-loading"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="view-loading"]')).toHaveLength(1)

    held.arrive()
    await navigation
    await flushPromises()

    expect(wrapper.find('[data-testid="area-slow"]').exists()).toBe(true)
  })

  it('ends on the load error, not the skeleton, when the download stalls for good', async () => {
    vi.useFakeTimers()
    try {
      const { wrapper, router } = await mountRouted(withRetry(() => new Promise<{ default: Component }>(() => undefined)))

      const navigation = router.push('/slow')
      const outcome = navigation.then(() => 'arrived', (error: Error) => error.message)
      await vi.advanceTimersByTimeAsync(1)
      expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(true)

      await vi.advanceTimersByTimeAsync(LOAD_TIMEOUT_MS * (LOAD_RETRIES + 1) + RETRY_STEP_MS * 3)

      expect(await outcome).toContain('took longer than')
      await flushPromises()
      expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(false)
      expect(wrapper.find('[data-testid="view-load-error"]').exists()).toBe(true)
    } finally {
      vi.useRealTimers()
    }
  })

  it('shows the load error when the screen never arrives, and the next screen afterwards', async () => {
    const { wrapper, router } = await mountRouted(() => Promise.reject(new Error('offline')))

    await expect(router.push('/slow')).rejects.toThrow('offline')
    await flushPromises()
    expect(wrapper.find('[data-testid="view-load-error"]').exists()).toBe(true)

    await router.push('/area/ready')
    await flushPromises()

    expect(wrapper.find('[data-testid="view-load-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="area-ready"]').exists()).toBe(true)
  })
})

/** A form screen's schema, as a route record carries it. */
const FORM_SCHEMA: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  columns: [[{ key: 'account', title: 'shared.id', fields: [{ key: 'email', label: 'shared.email' }] }]],
}

/** The schema of a layout with tabs, whose routed child draws in its outlet. */
const TABS_SCHEMA: TabsSkeletonSchema = {
  kind: SkeletonKind.Tabs,
  tabs: [{ key: 'summary', label: 'clients.clientSummary.summary' }],
}

/**
 * Builds a router of a shell layout, a tab layout and screens whose records carry skeleton schemas, with the
 * navigation hooks installed; the router is not navigated, like a page that has just loaded.
 *
 * @param slow - The loader of the screens still downloading.
 * @param shellMounted - Called each time the shell component is mounted, so a test can tell one instance from two.
 * @returns The router, at its start location.
 */
const skeletonRouter = (slow: () => Promise<{ default: Component }>, shellMounted: () => void): Router => {
  /** The eager layout: chrome around a routed slot, like the app layout. */
  const Shell: Component = defineComponent({
    setup() {
      onMounted(shellMounted)
      return () => h('div', [h('nav', { 'data-testid': 'shell-chrome' }, 'chrome'), h(RoutedView)])
    },
  })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        component: Shell,
        meta: { shell: true },
        children: [
          { path: 'ready', component: screen('shell-ready') },
          { path: 'form', component: slow, meta: { skeleton: FORM_SCHEMA } },
          { path: 'bare', component: slow },
          {
            path: 'area',
            component: AreaLayout,
            meta: { skeleton: TABS_SCHEMA },
            children: [
              { path: 'ready', component: screen('area-ready') },
              { path: 'form', component: slow, meta: { skeleton: FORM_SCHEMA } },
            ],
          },
        ],
      },
    ],
  })
  router.beforeEach(navigationAdmitted)
  router.afterEach(navigationSettled)
  router.onError(navigationFailed)
  return router
}

/**
 * Mounts a RoutedView at the top of a skeleton router, without navigating first.
 *
 * @param router - The router to mount on.
 * @returns The wrapper.
 */
const mountOn = (router: Router): VueWrapper =>
  mount(defineComponent({ render: () => h('div', [h(RoutedView)]) }), {
    global: { plugins: [createTestI18n(), pinia, router] },
  })

describe('RoutedView skeletons', () => {
  it('draws the schema of the record the navigation brings, not the generic stand-in', async () => {
    const held = heldLoader(screen('form-screen'))
    const router = skeletonRouter(held.load, () => undefined)
    await router.push('/ready')
    const wrapper = mountOn(router)
    await flushPromises()

    const navigation = router.push('/form')
    await flushPromises()

    expect(wrapper.find('[data-testid="ui-skeleton-of"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="shell-chrome"]').exists()).toBe(true)

    held.arrive()
    await navigation
    await flushPromises()

    expect(wrapper.find('[data-testid="form-screen"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-skeleton-of"]').exists()).toBe(false)
  })

  it('falls back to the generic stand-in for a record without a schema', async () => {
    const held = heldLoader(screen('bare-screen'))
    const router = skeletonRouter(held.load, () => undefined)
    await router.push('/ready')
    const wrapper = mountOn(router)
    await flushPromises()

    const navigation = router.push('/bare')
    await flushPromises()

    expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-skeleton-of"]').exists()).toBe(false)

    held.arrive()
    await navigation
  })

  it('draws a tab layout with the skeleton of the record below it in its outlet', async () => {
    const held = heldLoader(screen('area-form'))
    const router = skeletonRouter(held.load, () => undefined)
    await router.push('/ready')
    const wrapper = mountOn(router)
    await flushPromises()

    const navigation = router.push('/area/form')
    await flushPromises()

    const skeletons = wrapper.findAll('[data-testid="ui-skeleton-of"]')
    expect(skeletons).toHaveLength(2)
    expect(skeletons[0]?.element.contains(skeletons[1]?.element ?? null)).toBe(true)
    expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(false)

    held.arrive()
    await navigation
  })

  it('draws the generic stand-in in the outlet when the record below the tab layout has no schema', async () => {
    const held = heldLoader(screen('area-bare'))
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        { path: '/', component: screen('home') },
        {
          path: '/area',
          component: AreaLayout,
          meta: { skeleton: TABS_SCHEMA },
          children: [{ path: 'bare', component: held.load }],
        },
      ],
    })
    router.beforeEach(navigationAdmitted)
    router.afterEach(navigationSettled)
    router.onError(navigationFailed)
    await router.push('/')
    const wrapper = mountOn(router)
    await flushPromises()

    const navigation = router.push('/area/bare')
    await flushPromises()

    expect(wrapper.findAll('[data-testid="ui-skeleton-of"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="ui-skeleton-of"] [data-testid="view-loading"]').exists()).toBe(true)

    held.arrive()
    await navigation
  })

  it('draws the eager shell itself on a hard load, with the skeleton of the screen inside it, and keeps its instance', async () => {
    const held = heldLoader(screen('form-screen'))
    const shellMounted = vi.fn()
    const router = skeletonRouter(held.load, shellMounted)
    const wrapper = mountOn(router)

    const navigation = router.push('/form')
    await flushPromises()

    expect(wrapper.find('[data-testid="shell-chrome"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-skeleton-of"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="view-loading"]').exists()).toBe(false)

    held.arrive()
    await navigation
    await flushPromises()

    expect(wrapper.find('[data-testid="form-screen"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-skeleton-of"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="shell-chrome"]')).toHaveLength(1)
    expect(shellMounted).toHaveBeenCalledTimes(1)
  })
})
