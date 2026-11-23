/**
 * Unit tests for ClientLayoutNavItem: one sidebar link.
 *
 * @module tests/components/client/layout/NavItem.test
 */

import { describe, expect, it } from 'vitest'
import { Server } from 'lucide-vue-next'
import { mountClient } from '~/tests/helpers/mountClient'
import NavItem from '~/components/client/layout/NavItem.vue'
import { CLIENT_SERVICES_PATH } from '~/constants/clientRoutes'

/** A link to the services section. */
const ITEM = { to: CLIENT_SERVICES_PATH, label: 'My Services', icon: Server }

describe('ClientLayoutNavItem', () => {
  it('links to the target with its label', () => {
    const wrapper = mountClient(NavItem, { props: { item: ITEM, active: false } })

    const link = wrapper.find('a')
    expect(link.attributes('href')).toBe(CLIENT_SERVICES_PATH)
    expect(link.text()).toBe('My Services')
  })

  it('derives its test id from the target', () => {
    const wrapper = mountClient(NavItem, { props: { item: ITEM, active: false } })

    expect(wrapper.find('[data-testid="nav-client-services"]').exists()).toBe(true)
  })

  it('highlights the active section', () => {
    const active = mountClient(NavItem, { props: { item: ITEM, active: true } })
    const idle = mountClient(NavItem, { props: { item: ITEM, active: false } })

    expect(active.find('a').classes()).toContain('bg-primary-500/10')
    expect(idle.find('a').classes()).not.toContain('bg-primary-500/10')
  })

  it('emits navigate when followed', async () => {
    const wrapper = mountClient(NavItem, { props: { item: ITEM, active: false } })

    await wrapper.find('a').trigger('click')

    expect(wrapper.emitted('navigate')).toHaveLength(1)
  })

  it('keeps a 44px row and lets a long label break instead of pushing the drawer wider', () => {
    const wrapper = mountClient(NavItem, { props: { item: { ...ITEM, label: 'A very long plugin menu label' }, active: false } })

    expect(wrapper.find('a').classes()).toContain('min-h-11')
    expect(wrapper.find('a span').classes()).toEqual(expect.arrayContaining(['min-w-0', 'break-words']))
  })
})
