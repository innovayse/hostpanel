/**
 * Mount tests for ClientExternalAccountCard: the typed values go out trimmed (blank as null), Save
 * needs a value, clearing asks first, the API sentence and field messages show and a field
 * message is dropped as soon as its input changes.
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import ClientExternalAccountCard from '@/components/clients/ClientExternalAccountCard.vue'
import { createTestI18n } from '../../helpers/i18n'

/**
 * Mounts the card with the dialog teleport inlined.
 *
 * @param props - Props and models to override.
 * @returns The wrapper.
 */
const mountCard = (props: Record<string, unknown> = {}) =>
  mount(ClientExternalAccountCard, { props, global: { plugins: [createTestI18n()], stubs: { teleport: true } } })

/** A link the client already has. */
const LINK = { provider: 'crm', id: 'acct-1' }

describe('ClientExternalAccountCard', () => {
  it('says there is no link and offers no Clear while the client is not linked', () => {
    const wrapper = mountCard()

    expect(wrapper.get('[data-testid="client-external-account-current"]').text()).toBe('Not linked to an external account.')
    expect(wrapper.find('[data-testid="client-external-account-clear"]').exists()).toBe(false)
  })

  it('shows the current link and offers Clear while the client is linked', () => {
    const wrapper = mountCard({ linked: LINK })

    expect(wrapper.get('[data-testid="client-external-account-current"]').text()).toBe('Linked to crm, account acct-1.')
    expect(wrapper.find('[data-testid="client-external-account-clear"]').exists()).toBe(true)
  })

  it('keeps Save off until a value is typed', async () => {
    const wrapper = mountCard()

    expect(wrapper.get('[data-testid="client-external-account-save"]').attributes('disabled')).toBeDefined()

    await wrapper.get('[data-testid="client-external-account-provider"]').setValue('crm')
    expect(wrapper.get('[data-testid="client-external-account-save"]').attributes('disabled')).toBeUndefined()
  })

  it('emits the trimmed values on save and blank as null', async () => {
    const wrapper = mountCard()

    await wrapper.get('[data-testid="client-external-account-provider"]').setValue('  crm  ')
    await wrapper.get('[data-testid="client-external-account-save"]').trigger('click')
    await wrapper.get('[data-testid="client-external-account-id"]').setValue(' acct-1 ')
    await wrapper.get('[data-testid="client-external-account-save"]').trigger('click')

    expect(wrapper.emitted('save')).toEqual([
      [{ provider: 'crm', accountId: null }],
      [{ provider: 'crm', accountId: 'acct-1' }],
    ])
  })

  it('asks before clearing and emits clear only on confirm', async () => {
    const wrapper = mountCard({ linked: LINK })

    await wrapper.get('[data-testid="client-external-account-clear"]').trigger('click')
    expect(wrapper.find('[data-testid="client-external-account-confirm"]').exists()).toBe(true)
    expect(wrapper.emitted('clear')).toBeUndefined()

    await wrapper.get('[data-testid="dialog-cancel"]').trigger('click')
    expect(wrapper.find('[data-testid="client-external-account-confirm"]').exists()).toBe(false)
    expect(wrapper.emitted('clear')).toBeUndefined()

    await wrapper.get('[data-testid="client-external-account-clear"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    expect(wrapper.emitted('clear')).toHaveLength(1)
    expect(wrapper.find('[data-testid="client-external-account-confirm"]').exists()).toBe(false)
  })

  it('shows the API sentence of a conflict', () => {
    const wrapper = mountCard({ error: 'This account is already linked to another client.' })

    expect(wrapper.get('[data-testid="client-external-account-status"]').text())
      .toBe('This account is already linked to another client.')
  })

  it('shows the outcome line when nothing was refused', () => {
    const wrapper = mountCard({ notice: 'External account link saved.' })

    expect(wrapper.get('[data-testid="client-external-account-status"]').text()).toBe('External account link saved.')
  })

  it('shows the API field messages under their inputs', () => {
    const wrapper = mountCard({ fieldErrors: { provider: 'Provider is required.', accountId: 'Account id is too long.' } })

    expect(wrapper.get('[data-testid="client-external-account-provider-error"]').text()).toBe('Provider is required.')
    expect(wrapper.get('[data-testid="client-external-account-id-error"]').text()).toBe('Account id is too long.')
  })

  it('asks to drop a field message when that field changes, and only that one', async () => {
    const wrapper = mountCard({ fieldErrors: { provider: 'Provider is required.' } })

    await wrapper.get('[data-testid="client-external-account-id"]').setValue('acct-1')
    expect(wrapper.emitted('clearFieldError')).toBeUndefined()

    await wrapper.get('[data-testid="client-external-account-provider"]').setValue('crm')
    expect(wrapper.emitted('clearFieldError')).toEqual([['provider']])
  })

  it('disables Save and Clear while a call is in flight', () => {
    const wrapper = mountCard({ saving: true, provider: 'crm', linked: LINK })

    expect(wrapper.get('[data-testid="client-external-account-save"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="client-external-account-clear"]').attributes('disabled')).toBeDefined()
  })
})
