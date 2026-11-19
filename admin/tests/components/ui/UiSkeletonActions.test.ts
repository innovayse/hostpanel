/**
 * Unit tests for UiSkeletonActions: one button shaped bar per action, folded on a phone like the real bar of actions.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiSkeletonActions from '@/components/ui/UiSkeletonActions.vue'

describe('UiSkeletonActions', () => {
  it('draws nothing when there are no actions', () => {
    const wrapper = mount(UiSkeletonActions, { props: { count: 0 } })

    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(0)
  })

  it('draws one button shaped bar for a single action and no menu bar', () => {
    const wrapper = mount(UiSkeletonActions, { props: { count: 1 } })

    expect(wrapper.findAll('.h-10.w-28')).toHaveLength(1)
    expect(wrapper.find('[data-testid="ui-skeleton-actions-more"]').exists()).toBe(false)
  })

  it('keeps the first bar on a phone, hides the others there and draws one bar for the folded menu', () => {
    const wrapper = mount(UiSkeletonActions, { props: { count: 3 } })
    const buttons = wrapper.findAll('.h-10.w-28')

    expect(buttons).toHaveLength(3)
    expect(buttons[0]?.classes()).not.toContain('max-sm:hidden')
    expect(buttons.slice(1).every((bar) => bar.classes().includes('max-sm:hidden'))).toBe(true)
    expect(wrapper.get('[data-testid="ui-skeleton-actions-more"]').classes()).toEqual(expect.arrayContaining(['size-10', 'sm:hidden']))
  })

  it('draws the smaller buttons of a card header when compact', () => {
    const wrapper = mount(UiSkeletonActions, { props: { count: 2, compact: true } })

    expect(wrapper.findAll('.h-9.w-28')).toHaveLength(2)
    expect(wrapper.get('[data-testid="ui-skeleton-actions-more"]').classes()).toContain('size-9')
  })

  it('keeps the bar of the primary action on a phone, as the real header keeps its primary button', () => {
    const wrapper = mount(UiSkeletonActions, { props: { count: 3, primary: 2 } })

    expect(wrapper.findAll('.h-10.w-28').map((bar) => bar.classes().includes('max-sm:hidden'))).toEqual([true, true, false])
  })
})
