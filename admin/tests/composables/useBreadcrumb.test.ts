/**
 * Tests for useBreadcrumb: the trail follows the current route and its pattern, names the pending target while its
 * screens download, and is translated in the active language.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h } from 'vue'
import type { RouteLocationNormalized } from 'vue-router'
import { useBreadcrumb } from '@/composables/useBreadcrumb'
import { useNavigationStore } from '@/stores/navigation'
import { mountScreen } from '../helpers/mount'
import { stubRoutes } from '../helpers/routes'

/** A host that prints the trail as `a > b`, so a test reads it as text. */
const Host = defineComponent({
  /**
   * Draws the labels of the trail joined by an arrow.
   *
   * @returns The render function.
   */
  setup() {
    const crumbs = useBreadcrumb()
    return () => h('p', { 'data-testid': 'trail' }, crumbs.value.map((crumb) => crumb.label).join(' > '))
  },
})

describe('useBreadcrumb', () => {
  it('builds the trail of the current route from its pattern', async () => {
    stubRoutes({})
    const { wrapper } = await mountScreen(Host, {}, '/settings/slides/4/edit', '/settings/slides/:id/edit')

    expect(wrapper.get('[data-testid="trail"]').text()).toBe('Settings > Slides > Edit')
  })

  it('follows the navigation in flight, so the bar answers the click before the screen has arrived', async () => {
    stubRoutes({})
    const { wrapper } = await mountScreen(Host, {}, '/dashboard', '/dashboard')

    useNavigationStore().pending = { path: '/settings/general', matched: [], meta: {} } as unknown as RouteLocationNormalized
    await wrapper.vm.$nextTick()

    expect(wrapper.get('[data-testid="trail"]').text()).toBe('Settings > General')
  })
})
