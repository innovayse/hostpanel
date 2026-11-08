/**
 * Unit tests for PluginLicenceCard: the saved or missing state, the write-only password field,
 * saving a trimmed key, clearing the field once a key is saved, and removing the saved key.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginLicenceCard from '@/components/plugins/details/PluginLicenceCard.vue'
import { createTestI18n } from '../../../helpers/i18n'

/**
 * Mounts the card.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountCard = (props: { hasKey?: boolean; busy?: boolean } = {}) =>
  mount(PluginLicenceCard, {
    props: { hasKey: false, busy: false, ...props },
    global: { plugins: [createTestI18n()] },
  })

describe('PluginLicenceCard', () => {
  it('says no key is saved and offers no removal', () => {
    const wrapper = mountCard()

    expect(wrapper.find('[data-testid="plugin-licence-missing"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-licence-remove"]').exists()).toBe(false)
  })

  it('says a key is saved and offers its removal, without showing the key', () => {
    const wrapper = mountCard({ hasKey: true })

    expect(wrapper.find('[data-testid="plugin-licence-saved"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-licence-input"]').attributes('type')).toBe('password')
    expect((wrapper.get('[data-testid="plugin-licence-input"]').element as HTMLInputElement).value).toBe('')
  })

  it('has the card padding', () => {
    expect(mountCard().get('[data-testid="plugin-licence-card"]').classes()).toContain('p-5')
  })

  it('has a placeholder on the field', () => {
    const wrapper = mountCard()

    expect(wrapper.get('[data-testid="plugin-licence-input"]').attributes('placeholder')).toBe('Paste the licence key')
  })

  it('keeps save disabled until a key is typed, then emits the trimmed key', async () => {
    const wrapper = mountCard()

    expect(wrapper.get('[data-testid="plugin-licence-save"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="plugin-licence-input"]').setValue('  KEY-123  ')
    await wrapper.get('[data-testid="plugin-licence-save"]').trigger('click')

    expect(wrapper.emitted('save')).toEqual([['KEY-123']])
  })

  it('clears the field once the server holds the key', async () => {
    const wrapper = mountCard()
    await wrapper.get('[data-testid="plugin-licence-input"]').setValue('KEY-123')

    await wrapper.setProps({ hasKey: true })

    expect((wrapper.get('[data-testid="plugin-licence-input"]').element as HTMLInputElement).value).toBe('')
  })

  it('emits remove from the remove button', async () => {
    const wrapper = mountCard({ hasKey: true })

    await wrapper.get('[data-testid="plugin-licence-remove"]').trigger('click')

    expect(wrapper.emitted('remove')).toHaveLength(1)
  })

  it('blocks save and remove while busy', async () => {
    const wrapper = mountCard({ hasKey: true, busy: true })
    await wrapper.get('[data-testid="plugin-licence-input"]').setValue('KEY-123')

    expect(wrapper.get('[data-testid="plugin-licence-save"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="plugin-licence-remove"]').attributes('disabled')).toBeDefined()
  })
})
