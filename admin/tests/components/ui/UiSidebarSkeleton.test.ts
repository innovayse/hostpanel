/**
 * Unit tests for UiSidebarSkeleton: the folded rail of an inner sidebar with an icon placeholder per entry.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSidebarSkeleton from '@/components/ui/UiSidebarSkeleton.vue'
import { sidebarRailWidthClass } from '@/utils/ui/sidebar'

/** Two entries of a rail. */
const entries = [
  { key: 'payments', label: 'Payments' },
  { key: 'email', label: 'Email' },
]

describe('UiSidebarSkeleton', () => {
  it('is the folded rail of the real sidebar: the same width and the frosted shell', () => {
    const wrapper = mount(UiSidebarSkeleton, { props: { entries } })
    const rail = wrapper.get('[data-testid="ui-sidebar-skeleton"]')

    expect(rail.classes()).toContain(sidebarRailWidthClass)
    expect(rail.classes()).toContain('glass')
  })

  it('draws one icon placeholder per entry, named by its real caption for assistive technology', () => {
    const wrapper = mount(UiSidebarSkeleton, { props: { entries } })
    const rows = wrapper.findAll('[data-testid="ui-sidebar-skeleton-entry"]')

    expect(rows.map((row) => row.text())).toEqual(['Payments', 'Email'])
    expect(rows.map((row) => row.attributes('title'))).toEqual(['Payments', 'Email'])
    expect(rows[0]?.find('[data-testid="ui-skeleton-bar"]').exists()).toBe(true)
  })

  it('has no link and nothing to focus', () => {
    const wrapper = mount(UiSidebarSkeleton, { props: { entries } })

    expect(wrapper.find('a').exists()).toBe(false)
    expect(wrapper.find('button').exists()).toBe(false)
  })
})
