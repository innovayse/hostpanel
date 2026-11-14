/**
 * Tests for the route-synced tab: the address is read against the enum, the default is left out of it, and
 * other query keys survive a change of tab.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useRouteTab } from '@/composables/useRouteTab'
import type { RouteTab } from '@/composables/useRouteTab'

/** The probe screen's tabs. */
enum Tab {
  /** The default tab. */
  One = 'one',
  /** A second tab. */
  Two = 'two',
}

/**
 * Mounts a screen that uses the composable, at the given address.
 *
 * @param url - The address the router starts at.
 * @returns The router and a reader of the composable's result.
 */
const setup = async (url: string) => {
  let state: RouteTab<Tab> | null = null
  const Screen = defineComponent({
    /**
     * Runs the composable.
     *
     * @returns The render function of an empty element.
     */
    setup() {
      state = useRouteTab(Tab, Tab.One)
      return () => h('div')
    },
  })
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/s', component: Screen }] })
  await router.push(url)
  await router.isReady()
  mount({ render: () => h(Screen) }, { global: { plugins: [router] } })
  return { router, tab: () => state as unknown as RouteTab<Tab> }
}

describe('useRouteTab', () => {
  it('falls back to the default when the address names no tab', async () => {
    const { tab } = await setup('/s')
    expect(tab().tab.value).toBe(Tab.One)
  })

  it('reads a known tab from the address', async () => {
    const { tab } = await setup('/s?tab=two')
    expect(tab().tab.value).toBe(Tab.Two)
  })

  it('falls back for an unknown tab', async () => {
    const { tab } = await setup('/s?tab=nope')
    expect(tab().tab.value).toBe(Tab.One)
  })

  it('writes a non-default tab and keeps other query keys', async () => {
    const { router, tab } = await setup('/s?x=1')
    await tab().select(Tab.Two)
    expect(router.currentRoute.value.query).toEqual({ x: '1', tab: 'two' })
    expect(tab().tab.value).toBe(Tab.Two)
  })

  it('leaves the default tab out of the address', async () => {
    const { router, tab } = await setup('/s?tab=two&x=1')
    await tab().select(Tab.One)
    expect(router.currentRoute.value.query).toEqual({ x: '1' })
  })
})
