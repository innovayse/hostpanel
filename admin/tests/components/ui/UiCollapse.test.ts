/**
 * Unit tests for UiCollapse: the content is rendered only while open, sits in a one-column grid whose rows the
 * shared collapse transition animates, and the transition is the collapse kind.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiCollapse from '@/components/ui/UiCollapse.vue'
import UiTransition from '@/components/ui/UiTransition.vue'
import { UiTransitionKind } from '@/utils/ui/transition'

/**
 * Mounts the block around a paragraph.
 *
 * @param open - Whether the block is shown.
 * @returns The wrapper.
 */
const mountBlock = (open: boolean) => mount(UiCollapse, { props: { open }, slots: { default: '<p data-testid="content">Hello</p>' } })

describe('UiCollapse', () => {
  it('renders its content only while open, so a closed block takes no focus and is not read aloud', async () => {
    const wrapper = mountBlock(false)
    expect(wrapper.find('[data-testid="content"]').exists()).toBe(false)

    await wrapper.setProps({ open: true })

    expect(wrapper.get('[data-testid="content"]').text()).toBe('Hello')
  })

  it('animates the rows of a grid, never a measured height', () => {
    const wrapper = mountBlock(true)

    expect(wrapper.find('.grid').exists()).toBe(true)
    expect(wrapper.get('.grid > .min-h-0').find('[data-testid="content"]').exists()).toBe(true)
    expect(wrapper.findComponent(UiTransition).props('kind')).toBe(UiTransitionKind.Collapse)
  })
})
