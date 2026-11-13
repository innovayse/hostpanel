/**
 * Unit tests for UiPageHeader: title as the page heading, subtitle and slots.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import UiPageHeader from '@/components/ui/UiPageHeader.vue'

describe('UiPageHeader', () => {
  it('renders the title as the level-1 heading', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients' } })

    expect(wrapper.get('h1').text()).toBe('Clients')
  })

  it('renders the subtitle when given', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients', subtitle: 'All accounts' } })

    expect(wrapper.text()).toContain('All accounts')
  })

  it('renders the actions slot and the default slot', () => {
    const wrapper = mount(UiPageHeader, {
      props: { title: 'Clients' },
      slots: { default: '<span data-testid="extra" />', actions: '<button data-testid="act" />' },
    })

    expect(wrapper.find('[data-testid="extra"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="act"]').exists()).toBe(true)
  })

  it('sets the title in the muted subtitle colour below it as the concept does', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients', subtitle: 'All accounts' } })

    expect(wrapper.get('h1').classes()).toEqual(expect.arrayContaining(['text-[1.875rem]', 'font-semibold']))
    expect(wrapper.get('p').classes()).toEqual(expect.arrayContaining(['text-text-muted', 'mt-1.5']))
  })

  it('draws a title bar instead of the text while loading, and a subtitle bar only when one is passed', () => {
    const plain = mount(UiPageHeader, { props: { title: 'Clients', loading: true } })
    const withSubtitle = mount(UiPageHeader, { props: { title: 'Clients', subtitle: 'All accounts', loading: true } })

    expect(plain.find('h1').exists()).toBe(false)
    expect(plain.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(1)
    expect(withSubtitle.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(2)
    expect(withSubtitle.text()).not.toContain('All accounts')
    expect(plain.attributes('aria-busy')).toBe('true')
  })

  it('needs no title while loading', () => {
    const wrapper = mount(UiPageHeader, { props: { loading: true } })

    expect(wrapper.find('h1').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="ui-skeleton-bar"]')).toHaveLength(1)
  })

  it('keeps the actions while loading', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients', loading: true }, slots: { actions: '<button data-testid="act" />' } })

    expect(wrapper.find('[data-testid="act"]').exists()).toBe(true)
  })

  it('lets the actions wrap on a narrow screen instead of overflowing', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients' }, slots: { actions: '<button data-testid="act" />' } })
    const actions = wrapper.get('[data-testid="act"]').element.parentElement

    expect(actions?.classList.contains('flex-wrap')).toBe(true)
    expect(actions?.classList.contains('shrink-0')).toBe(false)
  })

  it('keeps 22px under itself less the gap its stack already adds', () => {
    const wrapper = mount(UiPageHeader, { props: { title: 'Clients' } })

    expect(wrapper.classes()).toContain('mb-[calc(1.375rem-var(--stack-gap,0rem))]')
  })
})
