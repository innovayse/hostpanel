/**
 * Unit tests for UiText: native paragraph, tone and size from tokens.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiText from '@/components/ui/UiText.vue'

describe('UiText', () => {
  it('renders the native p with its slot', () => {
    const wrapper = mount(UiText, { slots: { default: 'content' } })

    expect(wrapper.element.tagName).toBe('P')
    expect(wrapper.text()).toBe('content')
  })

  it('draws a span when asked, so it may sit inside a button', () => {
    const wrapper = mount(UiText, { props: { as: 'span' }, slots: { default: 'content' } })

    expect(wrapper.element.tagName).toBe('SPAN')
    expect(wrapper.text()).toBe('content')
  })

  it('passes classes and data attributes through', () => {
    const wrapper = mount(UiText, { attrs: { class: 'x-class', 'data-testid': 'probe' } })

    expect(wrapper.classes()).toContain('x-class')
    expect(wrapper.attributes('data-testid')).toBe('probe')
  })

  it('adds no typography in the plain default, only the wrap of a long unbroken word', () => {
    expect(mount(UiText).classes()).toEqual(['break-words'])
  })

  it('colours and sizes from the named props', () => {
    const wrapper = mount(UiText, { props: { tone: 'danger', size: 'sm' } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['text-status-red', 'text-[0.8125rem]']))
  })

  it('takes the accent colour', () => {
    expect(mount(UiText, { props: { tone: 'accent' } }).classes()).toContain('text-text-accent')
  })

  it('cuts a long line with an ellipsis when truncating, instead of wrapping it', () => {
    const wrapper = mount(UiText, { props: { truncate: true } })

    expect(wrapper.classes()).toEqual(expect.arrayContaining(['min-w-0', 'truncate']))
    expect(wrapper.classes()).not.toContain('break-words')
  })

  it('draws a moment as a time element and passes its datetime through', () => {
    const wrapper = mount(UiText, { props: { as: 'time' }, attrs: { datetime: '2026-01-01T10:00:00.000Z' }, slots: { default: '10:00' } })

    expect(wrapper.element.tagName).toBe('TIME')
    expect(wrapper.attributes('datetime')).toBe('2026-01-01T10:00:00.000Z')
  })
})
