/**
 * Tests for PluginRegistryInstanceCard: the state of this server's registration with the registry is shown as a badge
 * (not registered, waiting, verified, revoked, failed), the server's sentence for the last failure and the next automatic
 * attempt are shown, the Retry button is offered for everything but a verified server, it shows progress while busy, and a
 * click asks the parent to register again.
 */
import { describe, expect, it } from 'vitest'
import PluginRegistryInstanceCard from '@/components/plugins/PluginRegistryInstanceCard.vue'
import { RegistryInstanceState } from '@/types/plugin'
import type { RegistryInstanceDto } from '@/types/plugin'
import { mountScreen } from '../../helpers/mount'

/**
 * Builds a registration state.
 *
 * @param change - The members that differ from a server that was never registered.
 * @returns The state.
 */
const instance = (change: Partial<RegistryInstanceDto> = {}): RegistryInstanceDto => ({
  state: RegistryInstanceState.NotRegistered,
  instanceId: null,
  baseUrl: null,
  errorCode: null,
  message: null,
  verifiedAt: null,
  nextAttemptAt: null,
  ...change,
})

describe('PluginRegistryInstanceCard', () => {
  it('reads a server that has not answered yet as not registered, and offers the retry', async () => {
    const { wrapper } = await mountScreen(PluginRegistryInstanceCard, { instance: null, busy: false, error: null })

    expect(wrapper.get('[data-testid="plugin-registry-instance-state"]').text()).toBe('Not registered yet')
    expect(wrapper.find('[data-testid="plugin-registry-instance-retry"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-registry-instance-problem"]').exists()).toBe(false)
  })

  it.each([
    [RegistryInstanceState.NotRegistered, 'Not registered yet', true],
    [RegistryInstanceState.Pending, 'Waiting for confirmation', true],
    [RegistryInstanceState.Verified, 'Verified', false],
    [RegistryInstanceState.Revoked, 'Revoked', true],
    [RegistryInstanceState.Error, 'Failed', true],
  ])('shows %s as "%s" and offers the retry: %s', async (state, label, retry) => {
    const { wrapper } = await mountScreen(PluginRegistryInstanceCard, { instance: instance({ state }), busy: false, error: null })

    expect(wrapper.get('[data-testid="plugin-registry-instance-state"]').text()).toBe(label)
    expect(wrapper.find('[data-testid="plugin-registry-instance-retry"]').exists()).toBe(retry)
  })

  it('shows when a verified server was confirmed, and its public address', async () => {
    const { wrapper } = await mountScreen(PluginRegistryInstanceCard, {
      instance: instance({ state: RegistryInstanceState.Verified, verifiedAt: '2026-11-08T12:00:00Z', baseUrl: 'https://panel.example.com' }),
      busy: false,
      error: null,
    })

    expect(wrapper.get('[data-testid="plugin-registry-instance-verified-at"]').text()).toContain('Confirmed on')
    expect(wrapper.get('[data-testid="plugin-registry-instance-address"]').text()).toContain('https://panel.example.com')
  })

  it("shows the server's sentence for the last failure and the next automatic attempt", async () => {
    const { wrapper } = await mountScreen(PluginRegistryInstanceCard, {
      instance: instance({
        state: RegistryInstanceState.Error,
        errorCode: 'PLUGIN_REGISTRY_INSTANCE_CHALLENGE_UNREACHABLE',
        message: 'The plugin registry could not read the confirmation text from this Hostpanel.',
        nextAttemptAt: '2026-11-08T12:01:00Z',
      }),
      busy: false,
      error: null,
    })

    expect(wrapper.get('[data-testid="plugin-registry-instance-problem"]').text()).toContain('could not read the confirmation text')
    expect(wrapper.get('[data-testid="plugin-registry-instance-next"]').text()).toContain('Next automatic attempt')
  })

  it('shows the sentence of a refused call before the last failure of the registration', async () => {
    const { wrapper } = await mountScreen(PluginRegistryInstanceCard, {
      instance: instance({ state: RegistryInstanceState.Error, message: 'Old sentence.' }),
      busy: false,
      error: 'The registry is off.',
    })

    expect(wrapper.get('[data-testid="plugin-registry-instance-problem"]').text()).toBe('The registry is off.')
  })

  it('shows progress on the retry while busy, and sends no click meanwhile', async () => {
    const { wrapper } = await mountScreen(PluginRegistryInstanceCard, {
      instance: instance({ state: RegistryInstanceState.Error }),
      busy: true,
      error: null,
    })
    const button = wrapper.get('[data-testid="plugin-registry-instance-retry"]')

    await button.trigger('click')

    expect(button.attributes('aria-busy')).toBe('true')
    expect(button.attributes('disabled')).toBeDefined()
    expect(wrapper.emitted('retry')).toBeUndefined()
  })

  it('asks the parent to register again when the retry is pressed', async () => {
    const { wrapper } = await mountScreen(PluginRegistryInstanceCard, {
      instance: instance({ state: RegistryInstanceState.Revoked }),
      busy: false,
      error: null,
    })

    await wrapper.get('[data-testid="plugin-registry-instance-retry"]').trigger('click')

    expect(wrapper.emitted('retry')).toHaveLength(1)
  })
})
