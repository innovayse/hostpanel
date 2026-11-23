/**
 * Unit tests for UiAsideLayout: the two slots, the fixed-width side column and the sticky panel.
 */
import { describe, expect, it } from 'vitest'
import { mountWithKit } from '~/tests/helpers/mountWithKit'
import UiAsideLayout from '~/components/ui/UiAsideLayout.vue'

describe('UiAsideLayout', () => {
  it('renders the main and aside slots in order', () => {
    const wrapper = mountWithKit(UiAsideLayout, { slots: { main: 'Details', aside: 'Summary' } })

    expect(wrapper.find('[data-testid="ui-aside-layout-main"]').text()).toBe('Details')
    expect(wrapper.find('[data-testid="ui-aside-layout-aside"]').text()).toBe('Summary')
  })

  it('is a two-column grid with a fixed side column from lg', () => {
    const wrapper = mountWithKit(UiAsideLayout)

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['grid', 'lg:grid-cols-[minmax(0,1fr)_360px]']))
  })

  it('keeps the side panel in view while the page scrolls', () => {
    const wrapper = mountWithKit(UiAsideLayout)

    expect(wrapper.find('[data-testid="ui-aside-layout-aside"]').classes()).toContain('lg:sticky')
  })
})
