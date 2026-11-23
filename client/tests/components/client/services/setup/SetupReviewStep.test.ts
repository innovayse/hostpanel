/**
 * Unit tests for SetupReviewStep: the recap, the refusal and the build and back events.
 */
import { describe, expect, it } from 'vitest'
import { mountClient } from '~/tests/helpers/mountClient'
import SetupReviewStep from '~/components/client/services/setup/SetupReviewStep.vue'

/**
 * Mounts the step with defaults.
 *
 * @param props - Props to override.
 * @returns The wrapper.
 */
const mountStep = (props: Record<string, unknown> = {}) =>
  mountClient(SetupReviewStep, { props: { planName: 'Starter', domain: 'example.com', username: 'webmstr', submitting: false, error: '', ...props } })

describe('SetupReviewStep', () => {
  it('recaps the plan, the domain and the username', () => {
    const wrapper = mountStep()

    expect(wrapper.text()).toContain('Starter')
    expect(wrapper.text()).toContain('example.com')
    expect(wrapper.text()).toContain('webmstr')
  })

  it('shows the API refusal only when there is one', () => {
    expect(mountStep().find('[data-testid="setup-error"]').exists()).toBe(false)
    expect(mountStep({ error: 'Provisioning failed.' }).find('[data-testid="setup-error"]').text()).toBe('Provisioning failed.')
  })

  it('labels the back button with the shared back key', () => {
    expect(mountStep().find('[data-testid="setup-change"]').text()).toBe('common.back')
  })

  it('starts provisioning and goes back', async () => {
    const wrapper = mountStep()

    await wrapper.find('[data-testid="setup-build"]').trigger('click')
    await wrapper.find('[data-testid="setup-change"]').trigger('click')

    expect(wrapper.emitted('build')).toHaveLength(1)
    expect(wrapper.emitted('back')).toHaveLength(1)
  })

  it('locks both buttons while provisioning is in flight', () => {
    const wrapper = mountStep({ submitting: true })

    expect(wrapper.find('[data-testid="setup-build"]').attributes('disabled')).toBeDefined()
    expect(wrapper.find('[data-testid="setup-change"]').attributes('disabled')).toBeDefined()
  })
})
