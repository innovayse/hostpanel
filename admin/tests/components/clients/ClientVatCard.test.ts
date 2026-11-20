/**
 * Mount tests for ClientVatCard: the status badge and the date read from the client's last completed check, the registered
 * name and address, the "not checked" and "no number" states, the Check again action and the API's refusal sentence.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ClientVatCard from '@/components/clients/ClientVatCard.vue'
import { ClientVatStatus } from '@/types/clientvat'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the card.
 *
 * @param props - Props to override.
 * @returns The wrapper.
 */
const mountCard = (props: Record<string, unknown> = {}) =>
  mount(ClientVatCard, { props: { taxId: 'DE123456789', ...props }, global: { plugins: [createTestI18n()] } })

/** A valid check with the details the service returned. */
const VALID = { status: ClientVatStatus.Valid, checkedAt: '2026-11-10T09:30:00Z', name: 'Acme GmbH', address: 'Main St 1, Berlin' }

describe('ClientVatCard', () => {
  it('says where to enter the number and offers no check while the client has none', () => {
    const wrapper = mountCard({ taxId: null })

    expect(wrapper.get('[data-testid="client-vat-none"]').text()).toContain('Tax ID')
    expect(wrapper.find('[data-testid="client-vat-number"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="client-vat-check"]').attributes('disabled')).toBeDefined()
  })

  it('treats a blank number as none', () => {
    const wrapper = mountCard({ taxId: '   ' })

    expect(wrapper.find('[data-testid="client-vat-none"]').exists()).toBe(true)
  })

  it('shows a number that was never checked as not checked, with no date', () => {
    const wrapper = mountCard({ vat: { status: null, checkedAt: null, name: null, address: null } })

    expect(wrapper.get('[data-testid="client-vat-number"]').text()).toBe('DE123456789')
    expect(wrapper.get('[data-testid="client-vat-status"]').text()).toBe('Not checked')
    expect(wrapper.find('[data-testid="client-vat-date"]').exists()).toBe(false)
  })

  it('shows a valid number with the date of the check and the registered name and address', () => {
    const wrapper = mountCard({ vat: VALID })

    expect(wrapper.get('[data-testid="client-vat-status"]').text()).toBe('Valid')
    expect(wrapper.get('[data-testid="client-vat-date"]').text()).toContain('Checked')
    expect(wrapper.get('[data-testid="client-vat-name"]').text()).toBe('Acme GmbH')
    expect(wrapper.get('[data-testid="client-vat-address"]').text()).toBe('Main St 1, Berlin')
  })

  it('shows an invalid number without a name or address', () => {
    const wrapper = mountCard({ vat: { status: ClientVatStatus.Invalid, checkedAt: '2026-11-10T09:30:00Z', name: null, address: null } })

    expect(wrapper.get('[data-testid="client-vat-status"]').text()).toBe('Not valid')
    expect(wrapper.find('[data-testid="client-vat-name"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-vat-address"]').exists()).toBe(false)
  })

  it('emits check when staff run the check again', async () => {
    const wrapper = mountCard({ vat: VALID })

    await wrapper.get('[data-testid="client-vat-check"]').trigger('click')

    expect(wrapper.emitted('check')).toHaveLength(1)
  })

  it('shows the API sentence of a check that gave no verdict, and keeps the status that was read', () => {
    const wrapper = mountCard({ vat: VALID, error: 'The VAT number service is not answering right now.' })

    expect(wrapper.get('[data-testid="client-vat-feedback"]').text()).toBe('The VAT number service is not answering right now.')
    expect(wrapper.get('[data-testid="client-vat-status"]').text()).toBe('Valid')
  })

  it('shows the outcome line when nothing was refused', () => {
    const wrapper = mountCard({ vat: VALID, notice: 'VAT number checked.' })

    expect(wrapper.get('[data-testid="client-vat-feedback"]').text()).toBe('VAT number checked.')
  })

  it('disables the button while a check is in flight', () => {
    const wrapper = mountCard({ vat: VALID, checking: true })

    expect(wrapper.get('[data-testid="client-vat-check"]').attributes('disabled')).toBeDefined()
  })
})
