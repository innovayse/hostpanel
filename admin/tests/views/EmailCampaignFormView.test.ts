/**
 * Tests for EmailCampaignFormView through the real stores and transport with `fetch` stubbed: a new
 * campaign is created by the first save, the audience is counted live, Preview / Schedule / Send now
 * save first and then act, a campaign past the draft stage is shown read-only with its counters, and
 * a scheduled one can be cancelled. The date-time picker library is replaced by a stub.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import EmailCampaignFormView from '@/views/EmailCampaignFormView.vue'
import { useAuthStore } from '@/stores/auth'
import { AuthMode } from '@/types/auth'
import { CampaignFilterType, CampaignStatus } from '@/types/emailcampaign'
import { callOf, json, problem } from '../helpers/fetch'
import type { RecordedCall } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { pickOption } from '../helpers/select'
import { stubRoutes } from '../helpers/routes'

vi.mock('@vuepic/vue-datepicker', async () => {
  const { defineComponent, h } = await import('vue')
  return {
    VueDatePicker: defineComponent({
      name: 'VueDatePicker',
      props: ['modelValue', 'placeholder', 'minDate'],
      emits: ['update:modelValue'],
      setup: (props, { emit }) => () =>
        h('button', {
          'data-testid': 'email-campaign-schedule-at',
          onClick: () => emit('update:modelValue', new Date('2030-05-06T07:08:00.000Z')),
        }),
    }),
  }
})

/** The route the form is registered under; the id is optional so one pattern serves new and saved. */
const PATTERN = '/support/email-campaigns/:id?'

/** The delivery counters of a campaign nothing has been sent for. */
const untouched = { total: 0, pending: 0, sent: 0, failed: 0, unsubscribed: 0, opened: 0, uniqueOpened: 0, clicked: 0, uniqueClicked: 0, openRate: 0, clickRate: 0 }

/**
 * A campaign as the detail endpoint returns it.
 *
 * @param changes - What differs from a draft for everyone in Armenia.
 * @returns The campaign.
 */
const campaign = (changes: Record<string, unknown> = {}): Record<string, unknown> => ({
  id: 7,
  subject: 'Spring offer',
  body: '<p>Hello</p>',
  status: CampaignStatus.Draft,
  scheduledAt: null,
  sentAt: null,
  createdAt: '2026-10-01T10:30:00Z',
  createdBy: 'admin@example.com',
  segments: [{ filterType: CampaignFilterType.Country, filterValue: 'AM' }],
  stats: untouched,
  ...changes,
})

/** The wrappers mounted by the running test, unmounted afterwards so no audience count outlives it. */
const mounted: VueWrapper[] = []

/**
 * Mounts the form at an address and waits for its loads.
 *
 * @param url - Address to open; the bare list address opens a new campaign.
 * @returns The wrapper.
 */
const open = async (url: string): Promise<VueWrapper> => {
  const { wrapper } = await mountRouted(EmailCampaignFormView, PATTERN, url)
  mounted.push(wrapper)
  await flushPromises()
  await flushPromises()
  return wrapper
}

/**
 * The requests a stubbed `fetch` received for one address.
 *
 * @param mock - The stubbed `fetch`.
 * @param method - HTTP verb.
 * @param url - Exact request URL.
 * @returns The matching calls, in order.
 */
const callsTo = (mock: ReturnType<typeof vi.fn>, method: string, url: string): RecordedCall[] =>
  mock.mock.calls
    .map((_, index) => callOf(mock, index))
    .filter((call) => call.method === method && call.url === url)

/**
 * Fills the subject and the body.
 *
 * @param wrapper - The mounted form.
 */
const fill = async (wrapper: VueWrapper): Promise<void> => {
  await wrapper.get('[data-testid="email-campaign-subject"]').setValue('  Spring offer ')
  await wrapper.get('[data-testid="email-campaign-body"]').setValue('<p>Hello</p>')
}

/** The routes every test needs: the products and the audience count. */
const common = {
  'POST /api/admin/email-campaigns/audience-count': () => json({ count: 12 }),
  'GET /api/products': () => json([{ id: 3, name: 'Basic hosting' }, { id: 4, name: 'Business hosting' }]),
  'GET /api/admin/client-groups': () => json([{ id: 2, name: 'Wholesale' }, { id: 5, name: 'VIP' }]),
}

afterEach(() => {
  while (mounted.length > 0) {
    mounted.pop()?.unmount()
  }
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('EmailCampaignFormView, a new campaign', () => {
  it('starts with one all-active rule and cannot be saved until it has a subject and a body', async () => {
    stubRoutes(common)
    const wrapper = await open('/support/email-campaigns')

    expect(wrapper.find('[data-testid="email-campaign-segment-1-type"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="email-campaign-save"]').attributes('disabled')).toBeDefined()

    await fill(wrapper)

    expect(wrapper.get('[data-testid="email-campaign-save"]').attributes('disabled')).toBeUndefined()
  })

  it('counts the audience once the rules stop changing, and holds the count back for an unfinished rule', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const mock = stubRoutes(common)
    const wrapper = await open('/support/email-campaigns')
    expect(wrapper.get('[data-testid="email-campaign-audience"]').text()).toBe('Counting matching clients...')

    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    expect(wrapper.get('[data-testid="email-campaign-audience"]').text()).toBe('Matching clients: 12')
    expect(callsTo(mock, 'POST', '/api/admin/email-campaigns/audience-count')[0]?.body)
      .toEqual({ segments: [{ filterType: CampaignFilterType.AllActive, filterValue: null }] })

    await pickOption(wrapper, 'email-campaign-segment-1-type', 'Clients with a service of a product')
    expect(wrapper.get('[data-testid="email-campaign-audience"]').text()).toBe('Finish every rule to see how many clients match.')
    await vi.advanceTimersByTimeAsync(300)
    expect(callsTo(mock, 'POST', '/api/admin/email-campaigns/audience-count')).toHaveLength(1)

    await pickOption(wrapper, 'email-campaign-segment-1-value', 'Business hosting')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()
    expect(callsTo(mock, 'POST', '/api/admin/email-campaigns/audience-count')[1]?.body)
      .toEqual({ segments: [{ filterType: CampaignFilterType.Product, filterValue: '4' }] })
  })

  it('counts the members of a client group picked by name', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
    const mock = stubRoutes(common)
    const wrapper = await open('/support/email-campaigns')

    await pickOption(wrapper, 'email-campaign-segment-1-type', 'Clients in a client group')
    await pickOption(wrapper, 'email-campaign-segment-1-value', 'VIP')
    await vi.advanceTimersByTimeAsync(300)
    await flushPromises()

    expect(callsTo(mock, 'POST', '/api/admin/email-campaigns/audience-count').at(-1)?.body)
      .toEqual({ segments: [{ filterType: CampaignFilterType.ClientGroup, filterValue: '5' }] })
  })

  it('leaves the sign-in rule out of the kinds on offer until the deployment is known to own its accounts', async () => {
    stubRoutes(common)
    const wrapper = await open('/support/email-campaigns')

    await wrapper.get('[data-testid="email-campaign-segment-1-type"] button').trigger('click')
    const labels = wrapper.findAll('[data-testid="email-campaign-segment-1-type"] [role="option"]').map((option) => option.text())

    expect(labels).toContain('Clients in a client group')
    expect(labels).not.toContain('Clients who have not signed in for a while')
  })

  it('offers the sign-in rule where the deployment owns its accounts', async () => {
    stubRoutes(common)
    const wrapper = await open('/support/email-campaigns')
    useAuthStore().mode = AuthMode.Local
    await flushPromises()

    await wrapper.get('[data-testid="email-campaign-segment-1-type"] button').trigger('click')
    const labels = wrapper.findAll('[data-testid="email-campaign-segment-1-type"] [role="option"]').map((option) => option.text())

    expect(labels).toContain('Clients who have not signed in for a while')
  })

  it('adds and removes rules', async () => {
    stubRoutes(common)
    const wrapper = await open('/support/email-campaigns')

    await wrapper.get('[data-testid="email-campaign-add-segment"]').trigger('click')
    expect(wrapper.find('[data-testid="email-campaign-segment-2"]').exists()).toBe(true)

    await wrapper.get('[data-testid="email-campaign-segment-1-remove"]').trigger('click')
    expect(wrapper.find('[data-testid="email-campaign-segment-1"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="email-campaign-segment-2"]').exists()).toBe(true)
  })

  it('creates the draft with the trimmed subject and the rules, then opens it', async () => {
    const mock = stubRoutes({
      'POST /api/admin/email-campaigns/audience-count': common['POST /api/admin/email-campaigns/audience-count'],
      'POST /api/admin/email-campaigns': () => json(7, 201),
      'GET /api/admin/email-campaigns/7': () => json(campaign({ segments: [{ filterType: CampaignFilterType.AllActive, filterValue: null }] })),
      'GET /api/products': common['GET /api/products'],
    })
    const wrapper = await open('/support/email-campaigns')

    await fill(wrapper)
    await wrapper.get('[data-testid="email-campaign-save"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(callsTo(mock, 'POST', '/api/admin/email-campaigns')[0]?.body).toEqual({
      subject: 'Spring offer',
      body: '<p>Hello</p>',
      segments: [{ filterType: CampaignFilterType.AllActive, filterValue: null }],
    })
    expect(callsTo(mock, 'GET', '/api/admin/email-campaigns/7')).toHaveLength(1)
    expect(wrapper.get('[data-testid="email-campaign-feedback"]').text()).toBe('Draft saved.')
    expect((wrapper.get('[data-testid="email-campaign-subject"]').element as HTMLInputElement).value).toBe('Spring offer')
  })

  it('shows the API sentence and the field message when the save is refused, and stays on the form', async () => {
    stubRoutes({
      ...common,
      'POST /api/admin/email-campaigns': () => json({
        type: 'VALIDATION_ERROR', title: 'Validation failed', status: 400, detail: 'Check the form.',
        errors: { Subject: ['The subject is too long.'] },
      }, 400),
    })
    const wrapper = await open('/support/email-campaigns')

    await fill(wrapper)
    await wrapper.get('[data-testid="email-campaign-save"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="email-campaign-error"]').text()).toBe('Check the form.')
    expect(wrapper.text()).toContain('The subject is too long.')
    expect(wrapper.find('[data-testid="email-campaign-save"]').exists()).toBe(true)
  })

  it('sends now after the confirmation: saves, schedules for now, and shows the campaign as sending', async () => {
    const mock = stubRoutes({
      'POST /api/admin/email-campaigns/7/schedule': () => new Response(null, { status: 204 }),
      'POST /api/admin/email-campaigns/audience-count': common['POST /api/admin/email-campaigns/audience-count'],
      'POST /api/admin/email-campaigns': () => json(7, 201),
      'GET /api/admin/email-campaigns/7': () => json(campaign({ status: CampaignStatus.Sending, stats: { ...untouched, total: 12, pending: 12 } })),
      'GET /api/products': common['GET /api/products'],
    })
    const wrapper = await open('/support/email-campaigns')
    await fill(wrapper)

    await wrapper.get('[data-testid="email-campaign-send-now"]').trigger('click')
    expect(wrapper.find('[role="dialog"]').exists()).toBe(true)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(callsTo(mock, 'POST', '/api/admin/email-campaigns/7/schedule')[0]?.body).toEqual({ at: null })
    expect(wrapper.get('[data-testid="email-campaign-status"]').text()).toBe('Sending')
    expect(wrapper.get('[data-testid="email-campaign-stat-pending"]').text()).toContain('12')
    expect(wrapper.find('[data-testid="email-campaign-save"]').exists()).toBe(false)
  })

  it('keeps the saved draft when scheduling is refused after the creation', async () => {
    stubRoutes({
      'POST /api/admin/email-campaigns/7/schedule': () => problem('The time is in the past.', 400),
      'POST /api/admin/email-campaigns/audience-count': common['POST /api/admin/email-campaigns/audience-count'],
      'POST /api/admin/email-campaigns': () => json(7, 201),
      'GET /api/admin/email-campaigns/7': () => json(campaign()),
      'GET /api/products': common['GET /api/products'],
    })
    const wrapper = await open('/support/email-campaigns')
    await fill(wrapper)

    await wrapper.get('[data-testid="email-campaign-schedule"]').trigger('click')
    await wrapper.get('[data-testid="email-campaign-schedule-at"]').trigger('click')
    await wrapper.get('[data-testid="email-campaign-schedule-confirm"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(wrapper.get('[data-testid="email-campaign-error"]').text()).toBe('The time is in the past.')
    expect(wrapper.find('[data-testid="email-campaign-save"]').exists()).toBe(true)
  })
})

describe('EmailCampaignFormView, a draft', () => {
  it('loads the draft into the form and saves it with a PUT', async () => {
    const mock = stubRoutes({
      ...common,
      'GET /api/admin/email-campaigns/7': () => json(campaign()),
      'PUT /api/admin/email-campaigns/7': () => new Response(null, { status: 204 }),
    })
    const wrapper = await open('/support/email-campaigns/7')
    const subject = wrapper.get('[data-testid="email-campaign-subject"]')
    expect((subject.element as HTMLInputElement).value).toBe('Spring offer')
    expect((wrapper.get('[data-testid="email-campaign-body"]').element as HTMLTextAreaElement).value).toBe('<p>Hello</p>')

    await subject.setValue('Autumn offer')
    await wrapper.get('[data-testid="email-campaign-save"]').trigger('click')
    await flushPromises()

    expect(callsTo(mock, 'PUT', '/api/admin/email-campaigns/7')[0]?.body).toEqual({
      subject: 'Autumn offer',
      body: '<p>Hello</p>',
      segments: [{ filterType: CampaignFilterType.Country, filterValue: 'AM' }],
    })
    expect(wrapper.get('[data-testid="email-campaign-feedback"]').text()).toBe('Draft saved.')
  })

  it('saves and then shows the rendered mail in the preview dialog', async () => {
    const mock = stubRoutes({
      ...common,
      'GET /api/admin/email-campaigns/7/preview': () => json({ subject: 'Spring offer', html: '<p>Hello Ada</p>' }),
      'GET /api/admin/email-campaigns/7': () => json(campaign()),
      'PUT /api/admin/email-campaigns/7': () => new Response(null, { status: 204 }),
    })
    const wrapper = await open('/support/email-campaigns/7')

    await wrapper.get('[data-testid="email-campaign-preview"]').trigger('click')
    await flushPromises()

    expect(callsTo(mock, 'PUT', '/api/admin/email-campaigns/7')).toHaveLength(1)
    expect(wrapper.get('[data-testid="email-campaign-preview-frame"]').attributes('srcdoc')).toBe('<p>Hello Ada</p>')

    await wrapper.get('[data-testid="dialog-close"]').trigger('click')
    expect(wrapper.find('[data-testid="email-campaign-preview-frame"]').exists()).toBe(false)
  })

  it('schedules for the picked moment as ISO text and reloads the campaign as scheduled', async () => {
    let status = CampaignStatus.Draft
    const mock = stubRoutes({
      ...common,
      'POST /api/admin/email-campaigns/7/schedule': () => {
        status = CampaignStatus.Scheduled
        return new Response(null, { status: 204 })
      },
      'GET /api/admin/email-campaigns/7': () => json(campaign({ status, scheduledAt: '2030-05-06T07:08:00.000Z' })),
      'PUT /api/admin/email-campaigns/7': () => new Response(null, { status: 204 }),
    })
    const wrapper = await open('/support/email-campaigns/7')

    await wrapper.get('[data-testid="email-campaign-schedule"]').trigger('click')
    await wrapper.get('[data-testid="email-campaign-schedule-at"]').trigger('click')
    await wrapper.get('[data-testid="email-campaign-schedule-confirm"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(callsTo(mock, 'POST', '/api/admin/email-campaigns/7/schedule')[0]?.body).toEqual({ at: '2030-05-06T07:08:00.000Z' })
    expect(wrapper.get('[data-testid="email-campaign-status"]').text()).toBe('Scheduled')
    expect(wrapper.get('[data-testid="email-campaign-scheduled-at"]').text()).toContain('Scheduled for')
    expect(wrapper.find('[data-testid="email-campaign-schedule-confirm"]').exists()).toBe(false)
  })

  it('shows the load error with a Retry instead of the form', async () => {
    let refused = true
    stubRoutes({
      ...common,
      'GET /api/admin/email-campaigns/7': () => (refused ? problem('Campaign is unavailable.', 503) : json(campaign())),
    })
    const wrapper = await open('/support/email-campaigns/7')

    expect(wrapper.get('[data-testid="email-campaign-load-error"]').text()).toBe('Campaign is unavailable.')
    expect(wrapper.find('[data-testid="email-campaign-save"]').exists()).toBe(false)

    refused = false
    await wrapper.get('[data-testid="email-campaign-load-error-retry"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="email-campaign-load-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="email-campaign-save"]').exists()).toBe(true)
  })
})

describe('EmailCampaignFormView, past the draft stage', () => {
  it('shows a sent campaign read-only with its counters and no way to save or send', async () => {
    const mock = stubRoutes({
      ...common,
      'GET /api/admin/email-campaigns/7': () => json(campaign({
        status: CampaignStatus.Sent,
        sentAt: '2026-10-02T08:05:00Z',
        segments: [
          { filterType: CampaignFilterType.Product, filterValue: '4' },
          { filterType: CampaignFilterType.ServiceExpiringInDays, filterValue: '30' },
          { filterType: CampaignFilterType.ClientGroup, filterValue: '5' },
          { filterType: CampaignFilterType.NotSignedInForDays, filterValue: '90' },
        ],
        stats: { total: 40, pending: 0, sent: 36, failed: 1, unsubscribed: 3, opened: 20, uniqueOpened: 15, clicked: 9, uniqueClicked: 6, openRate: 0.4167, clickRate: 0.1667 },
      })),
    })
    const wrapper = await open('/support/email-campaigns/7')

    expect(wrapper.get('[data-testid="email-campaign-status"]').text()).toBe('Sent')
    expect(wrapper.get('[data-testid="email-campaign-stat-total"]').text()).toContain('40')
    expect(wrapper.get('[data-testid="email-campaign-stat-sent"]').text()).toContain('36')
    expect(wrapper.get('[data-testid="email-campaign-stat-failed"]').text()).toContain('1')
    expect(wrapper.get('[data-testid="email-campaign-stat-unsubscribed"]').text()).toContain('3')
    expect(wrapper.get('[data-testid="email-campaign-subject"]').attributes('readonly')).toBeDefined()
    expect(wrapper.get('[data-testid="email-campaign-body"]').attributes('readonly')).toBeDefined()
    expect(wrapper.get('[data-testid="email-campaign-segment-summary-0"]').text()).toBe('Clients with a service of Business hosting')
    expect(wrapper.get('[data-testid="email-campaign-segment-summary-1"]').text()).toBe('Clients with a service expiring within 30 days')
    expect(wrapper.get('[data-testid="email-campaign-segment-summary-2"]').text()).toBe('Clients in the group VIP')
    expect(wrapper.get('[data-testid="email-campaign-segment-summary-3"]').text()).toBe('Clients who have not signed in for 90 days')
    expect(wrapper.find('[data-testid="email-campaign-save"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="email-campaign-send-now"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="email-campaign-cancel-schedule"]').exists()).toBe(false)
    expect(callsTo(mock, 'POST', '/api/admin/email-campaigns/audience-count')).toHaveLength(0)
  })

  it('previews a sent campaign without saving it', async () => {
    const mock = stubRoutes({
      ...common,
      'GET /api/admin/email-campaigns/7/preview': () => json({ subject: 'Spring offer', html: '<p>Hello Ada</p>' }),
      'GET /api/admin/email-campaigns/7': () => json(campaign({ status: CampaignStatus.Sent })),
    })
    const wrapper = await open('/support/email-campaigns/7')

    await wrapper.get('[data-testid="email-campaign-preview"]').trigger('click')
    await flushPromises()

    expect(callsTo(mock, 'PUT', '/api/admin/email-campaigns/7')).toHaveLength(0)
    expect(wrapper.find('[data-testid="email-campaign-preview-frame"]').exists()).toBe(true)
  })

  it('cancels the schedule of a scheduled campaign after the confirmation, which makes it a draft again', async () => {
    let status = CampaignStatus.Scheduled
    const mock = stubRoutes({
      ...common,
      'POST /api/admin/email-campaigns/7/cancel': () => {
        status = CampaignStatus.Draft
        return new Response(null, { status: 204 })
      },
      'GET /api/admin/email-campaigns/7': () => json(campaign({ status, scheduledAt: status === CampaignStatus.Scheduled ? '2030-05-06T07:08:00.000Z' : null })),
    })
    const wrapper = await open('/support/email-campaigns/7')
    expect(wrapper.get('[data-testid="email-campaign-status"]').text()).toBe('Scheduled')

    await wrapper.get('[data-testid="email-campaign-cancel-schedule"]').trigger('click')
    expect(wrapper.find('[role="dialog"]').exists()).toBe(true)
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(callsTo(mock, 'POST', '/api/admin/email-campaigns/7/cancel')).toHaveLength(1)
    expect(wrapper.get('[data-testid="email-campaign-feedback"]').text()).toBe('The schedule is cancelled.')
    expect(wrapper.find('[data-testid="email-campaign-save"]').exists()).toBe(true)
  })

  it('shows the API sentence when cancelling the schedule is refused', async () => {
    stubRoutes({
      ...common,
      'POST /api/admin/email-campaigns/7/cancel': () => problem('The campaign is already sending.', 409),
      'GET /api/admin/email-campaigns/7': () => json(campaign({ status: CampaignStatus.Scheduled, scheduledAt: '2030-05-06T07:08:00.000Z' })),
    })
    const wrapper = await open('/support/email-campaigns/7')

    await wrapper.get('[data-testid="email-campaign-cancel-schedule"]').trigger('click')
    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(wrapper.get('[data-testid="email-campaign-error"]').text()).toBe('The campaign is already sending.')
  })
})
