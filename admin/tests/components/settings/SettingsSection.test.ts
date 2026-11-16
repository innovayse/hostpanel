/**
 * Tests for SettingsSection: the titled card that groups the controls of a settings screen.
 */
import { describe, expect, it } from 'vitest'
import { h } from 'vue'
import { mount } from '@vue/test-utils'
import UiField from '@/components/ui/UiField.vue'
import SettingsSection from '@/components/settings/SettingsSection.vue'

describe('SettingsSection', () => {
  it('renders the title as a heading and the slot as the body', () => {
    const wrapper = mount(SettingsSection, {
      props: { title: 'Branding' },
      slots: { default: '<p data-testid="body">Controls</p>' },
    })

    expect(wrapper.get('h2').text()).toBe('Branding')
    expect(wrapper.get('[data-testid="body"]').text()).toBe('Controls')
  })

  it('renders the description only when one is given', () => {
    const without = mount(SettingsSection, { props: { title: 'A' } })
    const withText = mount(SettingsSection, { props: { title: 'A', description: 'What this does' } })

    expect(without.find('p').exists()).toBe(false)
    expect(withText.get('p').text()).toBe('What this does')
  })

  it('lays the controls out in one column by default and in two from md when asked', () => {
    const single = mount(SettingsSection, { props: { title: 'A' } })
    const double = mount(SettingsSection, { props: { title: 'A', columns: 2 } })

    expect(single.get('.grid').classes()).toContain('grid-cols-1')
    expect(single.get('.grid').classes()).not.toContain('md:grid-cols-2')
    expect(double.get('.grid').classes()).toEqual(expect.arrayContaining(['grid-cols-1', 'md:grid-cols-2']))
  })

  it('keeps the real title and description and draws its fields as placeholders while loading', () => {
    const wrapper = mount(SettingsSection, {
      props: { title: 'Branding', description: 'What this does', loading: true },
      slots: { default: () => [h(UiField, { label: 'Name' }, { default: () => h('input', { 'data-testid': 'real-control' }) })] },
    })

    expect(wrapper.get('h2').text()).toBe('Branding')
    expect(wrapper.text()).toContain('What this does')
    expect(wrapper.text()).toContain('Name')
    expect(wrapper.find('[data-testid="real-control"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="ui-field-skeleton"]').exists()).toBe(true)
    expect(wrapper.attributes('aria-busy')).toBe('true')
  })

  it('is a section landmark named by its title', () => {
    const wrapper = mount(SettingsSection, { props: { title: 'Branding' } })

    expect(wrapper.element.tagName).toBe('SECTION')
    const titleId = wrapper.get('h2').attributes('id')
    expect(titleId).toBeTruthy()
    expect(wrapper.attributes('aria-labelledby')).toBe(titleId)
  })

  it('keeps the classes the caller passes on the card', () => {
    const wrapper = mount(SettingsSection, { props: { title: 'A' }, attrs: { class: 'mb-24' } })

    expect(wrapper.classes()).toContain('mb-24')
  })
})
