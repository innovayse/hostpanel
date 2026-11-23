/**
 * Tests for SettingsSubsection: the titled group of fields inside a settings card.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SettingsSubsection from '@/components/settings/SettingsSubsection.vue'

describe('SettingsSubsection', () => {
  it('shows its title as a heading and its fields as the body', () => {
    const wrapper = mount(SettingsSubsection, {
      props: { title: 'Contacts' },
      slots: { default: '<p data-testid="field">Email</p>' },
    })

    expect(wrapper.get('h3').text()).toBe('Contacts')
    expect(wrapper.get('[data-testid="field"]').text()).toBe('Email')
  })

  it('is a group named by its heading', () => {
    const wrapper = mount(SettingsSubsection, { props: { title: 'Contacts' } })

    expect(wrapper.attributes('role')).toBe('group')
    expect(wrapper.attributes('aria-labelledby')).toBe(wrapper.get('h3').attributes('id'))
  })

  it('spans the whole row of the card and lays its fields out in two columns by default, one when asked', () => {
    const two = mount(SettingsSubsection, { props: { title: 'A' } })
    const one = mount(SettingsSubsection, { props: { title: 'A', columns: 1 } })

    expect(two.classes()).toContain('col-span-full')
    expect(two.get('.grid').classes()).toContain('@lg:grid-cols-2')
    expect(one.get('.grid').classes()).not.toContain('@lg:grid-cols-2')
  })
})
