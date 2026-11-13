/**
 * Tests for FraudSettingsView: the stored settings fill the form, an edit is checked in the
 * browser (review below reject) before anything is sent, a save sends the whole document and shows
 * what the API stored, and a refusal shows under its field or in the banner.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import FraudSettingsView from '@/views/FraudSettingsView.vue'
import type { FraudSettings } from '@/types/fraud'
import { invalid, json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'

/** The settings the API starts with. */
const stored: FraudSettings = {
  enabled: true,
  manualReviewRiskScore: 50,
  autoRejectRiskScore: 80,
  velocityWindowMinutes: 60,
  velocityMaxOrders: 5,
  maxAccountsPerIp: 3,
}

/**
 * The native input of a numeric field.
 *
 * @param wrapper - The mounted screen.
 * @param field - The field's wire name.
 * @returns The input wrapper.
 */
const inputOf = (wrapper: VueWrapper, field: string): ReturnType<VueWrapper['get']> =>
  wrapper.get(`[data-testid="fraud-settings-${field}"] input`)

/**
 * Finds the body of the PUT a stub received.
 *
 * @param mock - The stubbed fetch.
 * @returns The parsed body, undefined when nothing was put.
 */
const putBody = (mock: ReturnType<typeof vi.fn>): unknown => {
  const call = mock.mock.calls.find(([, init]) => (init as RequestInit | undefined)?.method === 'PUT')
  return call === undefined ? undefined : JSON.parse((call[1] as RequestInit).body as string)
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('FraudSettingsView', () => {
  it('keeps the switch, the fields and the save button in place as placeholders while the settings load', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountScreen(FraudSettingsView)

    expect(wrapper.find('[data-testid="fraud-settings-form"]').exists()).toBe(true)
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]').length).toBeGreaterThan(0)
    expect(wrapper.get('[data-testid="fraud-settings-save"]').attributes('disabled')).toBeDefined()
  })

  it('fills the form with the stored settings', async () => {
    stubRouteTable({ 'GET /admin/fraud/settings': () => json(stored) })
    const { wrapper } = await mountScreen(FraudSettingsView)

    expect(wrapper.get('[data-testid="fraud-settings-enabled"]').attributes('aria-checked')).toBe('true')
    expect((inputOf(wrapper, 'manualReviewRiskScore').element as HTMLInputElement).value).toBe('50')
    expect((inputOf(wrapper, 'autoRejectRiskScore').element as HTMLInputElement).value).toBe('80')
    expect((inputOf(wrapper, 'velocityWindowMinutes').element as HTMLInputElement).value).toBe('60')
    expect((inputOf(wrapper, 'velocityMaxOrders').element as HTMLInputElement).value).toBe('5')
    expect((inputOf(wrapper, 'maxAccountsPerIp').element as HTMLInputElement).value).toBe('3')
  })

  it('saves the whole document and shows what the API stored', async () => {
    let current = stored
    const mock = stubRouteTable({
      'GET /admin/fraud/settings': () => json(current),
      'PUT /admin/fraud/settings': (body) => {
        current = body as FraudSettings
        return new Response(null, { status: 204 })
      },
    })
    const { wrapper } = await mountScreen(FraudSettingsView)

    await inputOf(wrapper, 'velocityMaxOrders').setValue('7')
    await wrapper.get('[data-testid="fraud-settings-enabled"]').trigger('click')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(putBody(mock)).toEqual({ ...stored, enabled: false, velocityMaxOrders: 7 })
    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/settings'])
    expect(wrapper.get('[data-testid="fraud-settings-result"]').text()).toBe('Fraud settings saved.')
    expect((inputOf(wrapper, 'velocityMaxOrders').element as HTMLInputElement).value).toBe('7')
  })

  it('shows a review threshold at or above the reject threshold under its field and blocks the save', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/settings': () => json(stored) })
    const { wrapper } = await mountScreen(FraudSettingsView)

    await inputOf(wrapper, 'manualReviewRiskScore').setValue('90')

    expect(wrapper.get('[data-testid="fraud-settings-manualReviewRiskScore-error"]').text()).toBe(
      'Must be lower than the auto reject threshold.',
    )
    expect(wrapper.get('[data-testid="fraud-settings-save"]').attributes('disabled')).toBeDefined()
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(putBody(mock)).toBeUndefined()
  })

  it('clears the message and allows the save once the thresholds are in order', async () => {
    stubRouteTable({ 'GET /admin/fraud/settings': () => json(stored) })
    const { wrapper } = await mountScreen(FraudSettingsView)
    await inputOf(wrapper, 'manualReviewRiskScore').setValue('90')

    await inputOf(wrapper, 'autoRejectRiskScore').setValue('95')

    expect(wrapper.find('[data-testid="fraud-settings-manualReviewRiskScore-error"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="fraud-settings-save"]').attributes('disabled')).toBeUndefined()
  })

  it('shows a refused field under the field and leaves the banner out', async () => {
    stubRouteTable({
      'GET /admin/fraud/settings': () => json(stored),
      'PUT /admin/fraud/settings': () => invalid('The settings are not valid.', { VelocityWindowMinutes: ['The window is too long for this plan.'] }),
    })
    const { wrapper } = await mountScreen(FraudSettingsView)

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="fraud-settings-velocityWindowMinutes-error"]').text()).toBe('The window is too long for this plan.')
    expect(wrapper.find('[data-testid="fraud-settings-result"]').exists()).toBe(false)
  })

  it('shows a refusal that names no field in the banner', async () => {
    stubRouteTable({
      'GET /admin/fraud/settings': () => json(stored),
      'PUT /admin/fraud/settings': () => problem('Not available in the demo.', 403),
    })
    const { wrapper } = await mountScreen(FraudSettingsView)

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="fraud-settings-result"]').text()).toBe('Not available in the demo.')
  })

  it('loads again from the Retry of a refused load', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/settings': () => problem('Not allowed.', 403) })
    const { wrapper } = await mountScreen(FraudSettingsView)
    const before = mock.mock.calls.length

    expect(wrapper.get('[data-testid="fraud-settings-error"]').text()).toContain('Not allowed.')
    await wrapper.get('[data-testid="fraud-settings-error-retry"]').trigger('click')
    await flushPromises()

    expect(mock.mock.calls.length).toBeGreaterThan(before)
  })
})
