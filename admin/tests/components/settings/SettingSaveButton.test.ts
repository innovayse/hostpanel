/**
 * Tests for SettingSaveButton: disabled until there is an edit, one click event, saving label.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SettingSaveButton from '@/components/settings/SettingSaveButton.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the button with the English catalogue.
 *
 * @param props - Props for the button.
 * @returns The wrapper.
 */
const mountButton = (props: { dirty: boolean; saving?: boolean }) =>
  mount(SettingSaveButton, { props, global: { plugins: [createTestI18n()] } })

describe('SettingSaveButton', () => {
  it('is disabled while there is nothing to save', () => {
    const wrapper = mountButton({ dirty: false })

    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toBe('Save')
  })

  it('emits click once when pressed with an edit pending', async () => {
    const wrapper = mountButton({ dirty: true })

    await wrapper.get('button').trigger('click')

    expect(wrapper.emitted('click')).toHaveLength(1)
  })

  it('keeps the label, marks it busy and blocks the click while a save is in flight', async () => {
    const wrapper = mountButton({ dirty: true, saving: true })

    expect(wrapper.text()).toBe('Save')
    expect(wrapper.get('button').attributes('aria-busy')).toBe('true')
    expect(wrapper.get('button').attributes('disabled')).toBeDefined()
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('click')).toBeUndefined()
  })

  it('is the primary action of the shared form action row, as wide as its card on a phone', () => {
    const wrapper = mountButton({ dirty: true })

    expect(wrapper.get('[data-testid="ui-form-actions"]').classes()).toEqual(expect.arrayContaining(['max-sm:w-full', 'max-sm:items-stretch']))
    expect(wrapper.get('[data-testid="ui-form-actions"]').classes()).not.toContain('max-sm:sticky')
    expect(wrapper.get('button').classes()).toContain('not-disabled:gradient-brand')
  })

  it('puts the attributes it is given on the button, not on the row', () => {
    const wrapper = mount(SettingSaveButton, { props: { dirty: true }, attrs: { 'data-testid': 'save-x' }, global: { plugins: [createTestI18n()] } })

    expect(wrapper.get('button').attributes('data-testid')).toBe('save-x')
  })
})
