/**
 * Tests for FraudSettingsView: the stored settings fill the form in two sections with units and a risk band preview, an
 * edit is checked in the browser (review below reject) before anything is sent, protection off disables the numbers
 * with a note, the shared footer saves the whole document or discards the edits, and a refusal shows under its field
 * or in the banner.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import FraudSettingsView from '@/views/FraudSettingsView.vue'
import schema from '@/views/FraudSettingsView.schema'
import type { FraudSettings } from '@/types/fraud'
import { useToastStore } from '@/stores/toast'
import { invalid, json, problem } from '../helpers/fetch'
import { mountScreen } from '../helpers/mount'
import { requestsOf, stubRouteTable } from '../helpers/routes'
import { toastMessages } from '../helpers/toasts'

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
 * The refusal line under a field of the form.
 *
 * @param wrapper - The mounted screen.
 * @param field - The field's wire name.
 * @returns The selector of the field's alert line.
 */
const errorOf = (wrapper: VueWrapper, field: string): ReturnType<VueWrapper['find']> =>
  wrapper.find(`[data-testid="ui-form-field-${field}"] [role="alert"]`)

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
  it('draws every field of its schema with the captions in place and placeholders for the controls while the settings load', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountScreen(FraudSettingsView)
    const fields = schema.columns.flat().flatMap((section) => section.fields)

    expect(wrapper.find('[data-testid="fraud-settings-form"]').exists()).toBe(true)
    fields.forEach((field) => {
      expect(wrapper.find(`[data-testid="ui-form-field-${field.key}"]`).exists()).toBe(true)
    })
    expect(wrapper.text()).toContain('Review threshold')
    expect(wrapper.find('[data-testid="fraud-settings-manualReviewRiskScore"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="ui-field-skeleton"]').length).toBeGreaterThan(0)
    expect(wrapper.find('[data-testid="settings-form-save"]').exists()).toBe(false)
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

  it('splits the numbers into a risk score card and a velocity card, each number with its unit', async () => {
    stubRouteTable({ 'GET /admin/fraud/settings': () => json(stored) })
    const { wrapper } = await mountScreen(FraudSettingsView)
    const thresholds = wrapper.get('[data-testid="ui-form-section-thresholds"]')
    const velocity = wrapper.get('[data-testid="ui-form-section-velocity"]')

    expect(thresholds.text()).toContain('Risk score thresholds')
    expect(thresholds.find('[data-testid="fraud-settings-manualReviewRiskScore"]').exists()).toBe(true)
    expect(thresholds.find('[data-testid="fraud-settings-autoRejectRiskScore"]').exists()).toBe(true)
    expect(velocity.text()).toContain('Velocity limits per IP address')
    expect(velocity.findAll('[data-testid="number-suffix"]').map((unit) => unit.text())).toEqual(['min', 'orders', 'accounts'])
    expect(thresholds.findAll('[data-testid="number-suffix"]').map((unit) => unit.text())).toEqual(['pts', 'pts'])
  })

  it('previews the Allow, Review and Reject bands of the thresholds and follows an edit', async () => {
    stubRouteTable({ 'GET /admin/fraud/settings': () => json(stored) })
    const { wrapper } = await mountScreen(FraudSettingsView)

    expect(wrapper.get('[data-testid="fraud-risk-band-caption-allow"]').text()).toBe('Allow, 0 to 49')
    expect(wrapper.get('[data-testid="fraud-risk-band-caption-review"]').text()).toBe('Review, 50 to 79')
    expect(wrapper.get('[data-testid="fraud-risk-band-caption-reject"]').text()).toBe('Reject, 80 to 100')

    await inputOf(wrapper, 'manualReviewRiskScore').setValue('30')

    expect(wrapper.get('[data-testid="fraud-risk-band-caption-allow"]').text()).toBe('Allow, 0 to 29')
    expect(wrapper.get('[data-testid="fraud-risk-band-caption-review"]').text()).toBe('Review, 30 to 79')
  })

  it('disables the numbers and says nothing is applied while protection is off, keeping their values', async () => {
    stubRouteTable({ 'GET /admin/fraud/settings': () => json({ ...stored, enabled: false }) })
    const { wrapper } = await mountScreen(FraudSettingsView)

    expect(wrapper.get('[data-testid="fraud-settings-off-note"]').text()).toContain('Fraud detection is off')
    expect(inputOf(wrapper, 'manualReviewRiskScore').attributes('disabled')).toBeDefined()
    expect(inputOf(wrapper, 'maxAccountsPerIp').attributes('disabled')).toBeDefined()
    expect((inputOf(wrapper, 'velocityMaxOrders').element as HTMLInputElement).value).toBe('5')

    await wrapper.get('[data-testid="fraud-settings-enabled"]').trigger('click')

    expect(wrapper.find('[data-testid="fraud-settings-off-note"]').exists()).toBe(false)
    expect(inputOf(wrapper, 'manualReviewRiskScore').attributes('disabled')).toBeUndefined()
  })

  it('keeps the shared footer idle until an edit, then counts it and lets Cancel discard it', async () => {
    stubRouteTable({ 'GET /admin/fraud/settings': () => json(stored) })
    const { wrapper } = await mountScreen(FraudSettingsView)
    expect(wrapper.get('[data-testid="settings-form-save"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="settings-form-status"]').text()).toBe('No unsaved changes')

    await inputOf(wrapper, 'velocityMaxOrders').setValue('7')

    expect(wrapper.get('[data-testid="settings-form-save"]').attributes('disabled')).toBeUndefined()
    expect(wrapper.get('[data-testid="settings-form-status"]').text()).toBe('1 unsaved change')
    await wrapper.get('[data-testid="settings-form-cancel"]').trigger('click')
    expect((inputOf(wrapper, 'velocityMaxOrders').element as HTMLInputElement).value).toBe('5')
    expect(wrapper.get('[data-testid="settings-form-save"]').attributes('disabled')).toBeDefined()
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
    await wrapper.get('[data-testid="settings-form-save"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(putBody(mock)).toEqual({ ...stored, enabled: false, velocityMaxOrders: 7 })
    expect(requestsOf(mock).slice(-1)).toEqual(['GET /admin/fraud/settings'])
    expect(useToastStore().toasts.map((toast) => toast.message)).toEqual(['Fraud settings saved.'])
    expect((inputOf(wrapper, 'velocityMaxOrders').element as HTMLInputElement).value).toBe('7')
  })

  it('shows a review threshold at or above the reject threshold under its field and blocks the save', async () => {
    const mock = stubRouteTable({ 'GET /admin/fraud/settings': () => json(stored) })
    const { wrapper } = await mountScreen(FraudSettingsView)

    await inputOf(wrapper, 'manualReviewRiskScore').setValue('90')

    expect(errorOf(wrapper, 'manualReviewRiskScore').text()).toBe('Must be lower than the auto reject threshold.')
    expect(wrapper.get('[data-testid="settings-form-save"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[data-testid="settings-form-cancel"]').attributes('disabled')).toBeUndefined()
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(putBody(mock)).toBeUndefined()
  })

  it('clears the message and allows the save once the thresholds are in order', async () => {
    stubRouteTable({ 'GET /admin/fraud/settings': () => json(stored) })
    const { wrapper } = await mountScreen(FraudSettingsView)
    await inputOf(wrapper, 'manualReviewRiskScore').setValue('90')

    await inputOf(wrapper, 'autoRejectRiskScore').setValue('95')

    expect(errorOf(wrapper, 'manualReviewRiskScore').exists()).toBe(false)
    expect(wrapper.get('[data-testid="settings-form-save"]').attributes('disabled')).toBeUndefined()
  })

  it('shows a refused field under the field and leaves the banner out', async () => {
    stubRouteTable({
      'GET /admin/fraud/settings': () => json(stored),
      'PUT /admin/fraud/settings': () => invalid('The settings are not valid.', { VelocityWindowMinutes: ['The window is too long for this plan.'] }),
    })
    const { wrapper } = await mountScreen(FraudSettingsView)

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(errorOf(wrapper, 'velocityWindowMinutes').text()).toBe('The window is too long for this plan.')
    expect(toastMessages()).toEqual([])
  })

  it('shows a refusal that names no field in the banner', async () => {
    stubRouteTable({
      'GET /admin/fraud/settings': () => json(stored),
      'PUT /admin/fraud/settings': () => problem('Not available in the demo.', 403),
    })
    const { wrapper } = await mountScreen(FraudSettingsView)

    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(toastMessages()).toContain('Not available in the demo.')
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
