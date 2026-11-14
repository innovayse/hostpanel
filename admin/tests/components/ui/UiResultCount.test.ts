/**
 * Unit tests for UiResultCount: the muted sentence, its tone and size, the bar that keeps its place while the first
 * answer is pending, and the test ids of the two states.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiResultCount from '@/components/ui/UiResultCount.vue'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'

describe('UiResultCount', () => {
  it('writes the sentence in the muted body size by default', () => {
    const wrapper = mount(UiResultCount, { slots: { default: '12 records found' } })

    expect(wrapper.text()).toBe('12 records found')
    expect(wrapper.find('[data-testid="ui-result-count-skeleton"]').exists()).toBe(false)
    expect(wrapper.classes()).toContain('text-text-muted')
    expect(wrapper.classes()).toContain('text-sm')
  })

  it('takes the tone and size of UiText', () => {
    const wrapper = mount(UiResultCount, { props: { tone: UiTextTone.Secondary, size: UiTextSize.Xs }, slots: { default: '3 found' } })

    expect(wrapper.classes()).not.toContain('text-text-muted')
    expect(wrapper.classes()).toContain('text-xs')
  })

  it('draws a bar and no sentence while loading', () => {
    const wrapper = mount(UiResultCount, { props: { loading: true }, slots: { default: '0 records found' } })

    expect(wrapper.find('[data-testid="ui-result-count-skeleton"]').exists()).toBe(true)
    expect(wrapper.text()).toBe('')
  })

  it('puts the test id of the parent on the sentence only, so the bar keeps its own', () => {
    const idle = mount(UiResultCount, { attrs: { 'data-testid': 'count' }, slots: { default: '1' } })
    const loading = mount(UiResultCount, { props: { loading: true }, attrs: { 'data-testid': 'count' } })

    expect(idle.attributes('data-testid')).toBe('count')
    expect(loading.attributes('data-testid')).toBe('ui-result-count-skeleton')
    expect(loading.find('[data-testid="count"]').exists()).toBe(false)
  })

  it('keeps the classes of the parent on both states, so the line does not move when the answer arrives', () => {
    const idle = mount(UiResultCount, { attrs: { class: 'mt-2' }, slots: { default: '1' } })
    const loading = mount(UiResultCount, { props: { loading: true }, attrs: { class: 'mt-2' } })

    expect(idle.classes()).toContain('mt-2')
    expect(loading.classes()).toContain('mt-2')
  })
})
