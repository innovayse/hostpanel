/**
 * Mount tests for IntegrationConfigForm: one control per described field, edits go into the save
 * payload (toggles as "true"/"false"), a secret is write only (masked, replaced or cleared, never
 * pre-filled), and the test button asks the parent to test.
 */
import { describe, expect, it } from 'vitest'
import IntegrationConfigForm from '@/components/integrations/IntegrationConfigForm.vue'
import { mountWithApp } from '../../helpers/mount'
import { IntegrationFieldType } from '@/types/integration'
import { integrationDetail } from '../../helpers/integrationFixtures'

/**
 * Mounts the form for the default detail.
 *
 * @param props - Props to change.
 * @returns The wrapper.
 */
const mountForm = (props: Record<string, unknown> = {}) =>
  mountWithApp(IntegrationConfigForm, { props: { integration: integrationDetail(), loading: false, testing: false, ...props } })

describe('IntegrationConfigForm', () => {
  it('heads the form with the name from the API and the short description the admin keeps for the slug', () => {
    const wrapper = mountForm()

    expect(wrapper.text()).toContain('Stripe')
    expect(wrapper.text()).toContain('Accept credit card payments online')
  })

  it('renders one control per field definition', () => {
    const wrapper = mountForm()

    expect(wrapper.find('[data-testid="integration-field-secretKey"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-field-mode"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-field-note"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-field-live"]').exists()).toBe(true)
  })

  it('shows a stored secret masked, with no value in any input', () => {
    const wrapper = mountForm()

    expect(wrapper.get('[data-testid="integration-field-secretKey-masked"]').text()).toContain('set')
    expect(wrapper.find('[data-testid="integration-field-secretKey-input"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="integration-field-secretKey-replace"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-field-secretKey-clear"]').exists()).toBe(true)
  })

  it('renders a plugin secret field (type Secret, isSecret true) as the write only box', () => {
    const wrapper = mountForm({
      integration: integrationDetail({
        config: { apiToken: { value: null, isSecret: true, hasValue: true } },
        fieldDefinitions: [{ key: 'apiToken', label: 'API token', type: IntegrationFieldType.Secret, required: true }],
      }),
    })

    expect(wrapper.get('[data-testid="integration-field-apiToken-masked"]').text()).toContain('set')
    expect(wrapper.find('[data-testid="integration-field-apiToken-input"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="integration-field-apiToken-replace"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="integration-field-apiToken-clear"]').exists()).toBe(true)
  })

  it('leaves an untouched secret out of the save payload', async () => {
    const wrapper = mountForm()

    await wrapper.get('[data-testid="integration-save"]').trigger('click')

    const payload = wrapper.emitted('save')?.[0]?.[0] as { config: Record<string, string>; clearFields?: string[] }
    expect(payload.config).not.toHaveProperty('secretKey')
    expect(payload.clearFields).toBeUndefined()
  })

  it('opens an empty password input on replace and sends the typed secret', async () => {
    const wrapper = mountForm()

    await wrapper.get('[data-testid="integration-field-secretKey-replace"]').trigger('click')
    const input = wrapper.get('[data-testid="integration-field-secretKey-input"]')
    expect(input.attributes('type')).toBe('password')
    expect((input.element as HTMLInputElement).value).toBe('')
    await input.setValue('sk_live_new')
    await wrapper.get('[data-testid="integration-save"]').trigger('click')

    const payload = wrapper.emitted('save')?.[0]?.[0] as { config: Record<string, string> }
    expect(payload.config.secretKey).toBe('sk_live_new')
  })

  it('sends a cleared secret as clearFields', async () => {
    const wrapper = mountForm()

    await wrapper.get('[data-testid="integration-field-secretKey-clear"]').trigger('click')
    expect(wrapper.find('[data-testid="integration-field-secretKey-pending-clear"]').exists()).toBe(true)
    await wrapper.get('[data-testid="integration-save"]').trigger('click')

    const payload = wrapper.emitted('save')?.[0]?.[0] as { config: Record<string, string>; clearFields?: string[] }
    expect(payload.clearFields).toEqual(['secretKey'])
    expect(payload.config).not.toHaveProperty('secretKey')
  })

  it('emits the edited values and the toggled switch on save', async () => {
    const wrapper = mountForm()

    await wrapper.get('[data-testid="integration-field-secretKey-replace"]').trigger('click')
    await wrapper.get('[data-testid="integration-field-secretKey-input"]').setValue('sk_live_new')
    await wrapper.get('[data-testid="integration-field-live"]').trigger('click')
    await wrapper.get('[data-testid="integration-save"]').trigger('click')

    const payload = wrapper.emitted('save')?.[0]?.[0] as { isEnabled: boolean; config: Record<string, string> }
    expect(payload.isEnabled).toBe(true)
    expect(payload.config.secretKey).toBe('sk_live_new')
    expect(payload.config.live).toBe('true')
    expect(payload.config.note).toBe('hello')
  })

  it('emits the enabled switch state', async () => {
    const wrapper = mountForm()

    await wrapper.get('[data-testid="integration-enabled"]').trigger('click')
    await wrapper.get('[data-testid="integration-save"]').trigger('click')

    const payload = wrapper.emitted('save')?.[0]?.[0] as { isEnabled: boolean }
    expect(payload.isEnabled).toBe(false)
  })

  it('emits test from the test button', async () => {
    const wrapper = mountForm()

    await wrapper.get('[data-testid="integration-test"]').trigger('click')

    expect(wrapper.emitted('test')).toHaveLength(1)
  })

  it('blocks both actions while the other one runs', () => {
    const saving = mountForm({ loading: true })
    expect(saving.get('[data-testid="integration-test"]').attributes('disabled')).toBeDefined()

    const testing = mountForm({ testing: true })
    expect(testing.get('[data-testid="integration-save"]').attributes('disabled')).toBeDefined()
  })
})
