/**
 * Unit tests for UiResultCount: the muted sentence, its tone and size, and the bar that keeps its place while
 * the first answer is pending.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiResultCount from '@/components/ui/UiResultCount.vue'

describe('UiResultCount', () => {
  it('writes the sentence in the muted body size by default', () => {
    const wrapper = mount(UiResultCount, { slots: { default: '12 records found' } })

    expect(wrapper.text()).toBe('12 records found')
    expect(wrapper.find('[data-testid="ui-result-count-skeleton"]').exists()).toBe(false)
    expect(wrapper.classes()).toContain('text-text-muted')
  })

  it('takes the tone and size of UiText', () => {
    const wrapper = mount(UiResultCount, { props: { tone: 'secondary', size: 'xs' }, slots: { default: '3 found' } })

    expect(wrapper.classes()).not.toContain('text-text-muted')
    expect(wrapper.classes()).toContain('text-xs')
  })

  it('draws a bar and no sentence while loading', () => {
    const wrapper = mount(UiResultCount, { props: { loading: true }, slots: { default: '0 records found' } })

    expect(wrapper.find('[data-testid="ui-result-count-skeleton"]').exists()).toBe(true)
    expect(wrapper.text()).toBe('')
  })

  it('passes a test id on to the root in either state', () => {
    const idle = mount(UiResultCount, { attrs: { 'data-testid': 'count' }, slots: { default: '1' } })
    const loading = mount(UiResultCount, { props: { loading: true }, attrs: { 'data-testid': 'count' } })

    expect(idle.attributes('data-testid')).toBe('count')
    expect(loading.attributes('data-testid')).toBe('count')
  })
})
