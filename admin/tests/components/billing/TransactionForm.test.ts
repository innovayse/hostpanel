/**
 * Mount tests for TransactionForm: the submit event, the refusal sentence, the locked client and
 * the v-model update when a field is typed.
 */
import { describe, expect, it, vi } from 'vitest'
import TransactionForm from '@/components/billing/TransactionForm.vue'
import { blankTransactionForm } from '@/lib/transactionForm'
import schema from '@/views/AddTransactionView.schema'
import type { ClientOption } from '@/types/clientoption'
import { mountWithApp } from '../../helpers/mount'
import { fieldsOf } from '../../helpers/schemaParity'

/** One client the picker can offer. */
const clients: ClientOption[] = [{ id: 7, name: 'Ada Lovelace', email: 'ada@example.com', status: 'Active' }]

/**
 * Mounts the form with the props these tests share.
 *
 * @param props - Props to add or replace.
 * @returns The wrapper.
 */
const mountForm = (props: Record<string, unknown> = {}) =>
  mountWithApp(TransactionForm, {
    props: { modelValue: blankTransactionForm(), columns: schema.columns, clients, submitLabel: 'Save it', ...props },
  })

describe('TransactionForm', () => {
  it('labels the submit button with the caption it was given', () => {
    const wrapper = mountForm()

    expect(wrapper.get('[data-testid="billing-transaction-submit"]').text()).toBe('Save it')
  })

  it('emits submit when the form is submitted', async () => {
    const wrapper = mountForm()

    await wrapper.get('form').trigger('submit')

    expect(wrapper.emitted('submit')).toHaveLength(1)
  })

  it('shows the sentence of a refused request', () => {
    const wrapper = mountForm({ error: 'Transaction refused.' })

    expect(wrapper.get('[data-testid="billing-transaction-error"]').text()).toBe('Transaction refused.')
  })

  it('shows the client as text when locked', () => {
    const wrapper = mountForm({ lockClient: true, modelValue: { ...blankTransactionForm(), clientId: '7' } })

    expect(wrapper.get('[data-testid="billing-transaction-client-locked"]').text()).toBe('Ada Lovelace')
  })

  it('writes a typed description through the model', async () => {
    const onUpdate = vi.fn()
    const wrapper = mountForm({ 'onUpdate:modelValue': onUpdate })

    await wrapper.get('[data-testid="billing-transaction-description"]').setValue('Domain renewal')

    expect(onUpdate).toHaveBeenCalledWith(expect.objectContaining({ description: 'Domain renewal' }))
  })

  it('draws every field of the schema it was given and the message of a refused field', () => {
    const wrapper = mountForm({ fieldErrors: { amountIn: 'Amount is wrong.' } })

    for (const field of fieldsOf(schema)) {
      expect(wrapper.find(`[data-testid="ui-form-field-${field.key}"]`).exists()).toBe(true)
    }
    expect(wrapper.get('[data-testid="ui-form-field-amountIn"]').text()).toContain('Amount is wrong.')
  })
})
