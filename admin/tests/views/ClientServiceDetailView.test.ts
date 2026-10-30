/**
 * Tests for ClientServiceDetailView: it renders against the real kit and settles on an API refusal,
 * each section of a loaded service saves through its own endpoint with only its own fields and
 * shows its outcome beside its own, distinctly named button, a pressed save keeps focus while it
 * runs, and a terminal status is confirmed before it is sent.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import ClientServiceDetailView from '@/views/ClientServiceDetailView.vue'
import { BillingCycle, ServiceStatus, type ServiceDetail } from '@/types/service'
import { mountClientView } from '../helpers/clientViews'
import { callOf, json, problem } from '../helpers/fetch'
import { createTestI18n } from '../helpers/i18n'
import { stubRoutes } from '../helpers/routes'

/** A loaded service, terminated so every section of the page is shown. */
const detail: ServiceDetail = {
  id: 9,
  clientId: 5,
  clientName: 'Ada Lovelace',
  productId: 2,
  productName: 'Starter',
  domain: 'site.example',
  status: ServiceStatus.Terminated,
  createdAt: '2030-01-01T00:00:00Z',
  terminatedAt: '2030-06-01T00:00:00Z',
  adminNotes: 'Old note',
  serverId: null,
  server: null,
  account: { dedicatedIp: null, username: 'siteuser', password: null, provisioningRef: 'acct1' },
  billing: {
    quantity: 1,
    firstPaymentAmount: 5,
    recurringAmount: 5,
    paymentMethod: null,
    promotionCode: null,
    subscriptionId: null,
    billingCycle: BillingCycle.Monthly,
    nextDueDate: '2031-03-04T00:00:00Z',
    autoRenew: true,
  },
  automation: { overrideAutoSuspend: false, suspendUntil: null, autoTerminateEndOfCycle: false, autoTerminateReason: null },
}

/**
 * Mounts the page on a service route with every endpoint answered: the detail, the lookups and a
 * 204 for any section save unless the caller answers the saves itself.
 *
 * @param service - The service the detail endpoint returns.
 * @param save - The answer to every section save.
 * @returns The wrapper and the fetch mock.
 */
const mountLoaded = async (
  service: ServiceDetail = detail,
  save: () => Response = () => new Response(null, { status: 204 }),
): Promise<{ wrapper: VueWrapper; mock: ReturnType<typeof vi.fn> }> => {
  const mock = stubRoutes({
    'GET /api/services/9': () => json(service),
    'GET /api/services': () => json({ items: [], totalCount: 0 }),
    'PUT /api/services/9/': save,
  })
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/clients/:id/services/:serviceId', component: { render: () => null } },
      { path: '/:pathMatch(.*)*', component: { render: () => null } },
    ],
  })
  await router.push('/clients/5/services/9')
  await router.isReady()
  const wrapper = mount(ClientServiceDetailView, {
    global: { plugins: [createTestI18n(), createPinia(), router], stubs: { teleport: true } },
  })
  await flushPromises()
  return { wrapper, mock }
}

/**
 * The recorded PUT calls, in order.
 *
 * @param mock - The fetch mock.
 * @returns Each PUT's url and parsed body.
 */
const putsOf = (mock: ReturnType<typeof vi.fn>) =>
  mock.mock.calls
    .map((_, index) => callOf(mock, index))
    .filter((call) => call.method === 'PUT')

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientServiceDetailView', () => {
  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientServiceDetailView)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientServiceDetailView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('saves the notes alone through the notes endpoint', async () => {
    const { wrapper, mock } = await mountLoaded()

    await wrapper.find('[data-testid="client-service-admin-notes"]').setValue('  New note  ')
    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()

    expect(putsOf(mock)).toEqual([
      expect.objectContaining({ url: '/api/services/9/notes', body: { adminNotes: 'New note' } }),
    ])
    expect(wrapper.get('[data-testid="client-service-outcome-notes"]').text()).toBe('Changes saved successfully.')
    expect(wrapper.get('[data-testid="client-service-outcome-billing"]').text()).toBe('')
  })

  it('shows a refusal beside the section that failed and clears it when the section is saved again', async () => {
    let refuse = true
    const { wrapper } = await mountLoaded(detail, () =>
      refuse ? problem('Notes are too long.', 400) : new Response(null, { status: 204 }),
    )

    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[data-testid="client-service-outcome-notes"]').text()).toBe('Notes are too long.')
    expect(wrapper.get('[data-testid="client-service-outcome-notes"]').attributes('data-tone')).toBe('danger')
    expect(wrapper.get('[data-testid="client-service-outcome-hosting-account"]').text()).toBe('')

    refuse = false
    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[data-testid="client-service-outcome-notes"]').text()).toBe('Changes saved successfully.')
  })

  it('gives every section save button its own name', async () => {
    const { wrapper } = await mountLoaded()
    const names = [
      'client-service-save-hosting',
      'client-service-save-billing',
      'client-service-save-automation',
      'client-service-save-notes',
      'client-service-save-termination-date',
    ].map((testid) => wrapper.get(`[data-testid="${testid}"]`).text())

    expect(new Set(names).size).toBe(names.length)
    expect(names).not.toContain('Save Changes')
  })

  it('keeps the pressed save focusable while it runs and marks its form busy', async () => {
    const { wrapper } = await mountLoaded()
    /** Settles the pending save; replaced once the request starts. */
    let release: (answer: Response) => void = () => undefined
    const pending = vi.fn(() => new Promise<Response>((resolve) => {
      release = resolve
    }))
    vi.stubGlobal('fetch', pending)

    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()
    const button = wrapper.get('[data-testid="client-service-save-notes"]')
    expect(button.attributes('disabled')).toBeUndefined()
    expect(button.attributes('aria-disabled')).toBe('true')
    expect(wrapper.get('[data-testid="client-service-notes-form"]').attributes('aria-busy')).toBe('true')

    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    expect(pending).toHaveBeenCalledTimes(1)

    release(new Response(null, { status: 204 }))
    await flushPromises()
    expect(wrapper.get('[data-testid="client-service-save-notes"]').attributes('aria-disabled')).toBe('false')
    expect(wrapper.get('[data-testid="client-service-notes-form"]').attributes('aria-busy')).toBe('false')
  })

  it('offers the control panel sign-in only on an active service', async () => {
    const { wrapper: terminated } = await mountLoaded(detail)
    const { wrapper: active } = await mountLoaded({ ...detail, status: ServiceStatus.Active })

    expect(terminated.find('[data-testid="client-service-panel"]').exists()).toBe(false)
    expect(active.find('[data-testid="client-service-panel"]').exists()).toBe(true)
  })

  it('confirms a terminal status with the danger prompt before sending it', async () => {
    const { wrapper, mock } = await mountLoaded({ ...detail, status: ServiceStatus.Active })

    const statusSelect = wrapper.findAllComponents(UiSelect).find((select) => select.attributes('data-testid') === 'client-service-status')
    statusSelect?.vm.$emit('update:modelValue', ServiceStatus.Terminated)
    await flushPromises()
    await wrapper.find('[data-testid="client-service-status-form"]').trigger('submit')
    await flushPromises()

    expect(putsOf(mock)).toEqual([])
    expect(wrapper.get('[data-testid="dialog-confirm"]').classes().join(' ')).toContain('status-red')

    await wrapper.get('[data-testid="dialog-confirm"]').trigger('click')
    await flushPromises()

    expect(putsOf(mock)).toEqual([
      expect.objectContaining({ url: '/api/services/9/status', body: { status: ServiceStatus.Terminated } }),
    ])
    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
  })

  it('moves to a non-terminal status without a prompt', async () => {
    const { wrapper, mock } = await mountLoaded()

    const statusSelect = wrapper.findAllComponents(UiSelect).find((select) => select.attributes('data-testid') === 'client-service-status')
    statusSelect?.vm.$emit('update:modelValue', ServiceStatus.Active)
    await flushPromises()
    await wrapper.find('[data-testid="client-service-status-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-testid="dialog-confirm"]').exists()).toBe(false)
    expect(putsOf(mock)).toEqual([
      expect.objectContaining({ url: '/api/services/9/status', body: { status: ServiceStatus.Active } }),
    ])
  })

  it('keeps unsaved edits in other sections after one section is saved', async () => {
    const { wrapper } = await mountLoaded()

    await wrapper.find('[data-testid="client-service-payment-method"]').setValue('Bank transfer')
    await wrapper.find('[data-testid="client-service-admin-notes"]').setValue('New note')
    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()

    const paymentMethod = wrapper.find('[data-testid="client-service-payment-method"]').element as HTMLInputElement
    expect(paymentMethod.value).toBe('Bank transfer')
  })

  it('refreshes the saved section from the reloaded service', async () => {
    const { wrapper } = await mountLoaded()

    await wrapper.find('[data-testid="client-service-admin-notes"]').setValue('  New note  ')
    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()

    const notes = wrapper.find('[data-testid="client-service-admin-notes"]').element as HTMLTextAreaElement
    expect(notes.value).toBe(detail.adminNotes)
  })

  it('saves the hosting account through its own endpoint', async () => {
    const { wrapper, mock } = await mountLoaded()

    await wrapper.find('[data-testid="client-service-hosting-form"]').trigger('submit')
    await flushPromises()

    expect(putsOf(mock)).toEqual([
      expect.objectContaining({
        url: '/api/services/9/hosting-account',
        body: { serverId: null, domain: 'site.example', provisioningRef: 'acct1', dedicatedIp: null, username: 'siteuser', password: null },
      }),
    ])
  })

  it('saves the billing terms with the product through the billing endpoint', async () => {
    const { wrapper, mock } = await mountLoaded()

    await wrapper.find('[data-testid="client-service-billing-form"]').trigger('submit')
    await flushPromises()

    expect(putsOf(mock)).toEqual([
      expect.objectContaining({
        url: '/api/services/9/billing',
        body: expect.objectContaining({ productId: 2, quantity: 1, billingCycle: BillingCycle.Monthly, recurringAmount: 5 }),
      }),
    ])
  })

  it('sends no hold date or reason while their switches are off', async () => {
    const { wrapper, mock } = await mountLoaded()

    await wrapper.find('[data-testid="client-service-automation-form"]').trigger('submit')
    await flushPromises()

    expect(putsOf(mock)).toEqual([
      expect.objectContaining({
        url: '/api/services/9/automation',
        body: { autoRenew: true, overrideAutoSuspend: false, suspendUntil: null, autoTerminateEndOfCycle: false, autoTerminateReason: null },
      }),
    ])
  })

  it('sends manual billing when the automatic billing switch is turned off', async () => {
    const { wrapper, mock } = await mountLoaded()

    await wrapper.find('[data-testid="client-service-auto-renew"]').trigger('click')
    await wrapper.find('[data-testid="client-service-automation-form"]').trigger('submit')
    await flushPromises()

    expect(putsOf(mock)).toEqual([
      expect.objectContaining({
        url: '/api/services/9/automation',
        body: expect.objectContaining({ autoRenew: false }),
      }),
    ])
  })

  it('corrects the termination date of a terminated service', async () => {
    const { wrapper, mock } = await mountLoaded()

    await wrapper.find('[data-testid="client-service-termination-form"]').trigger('submit')
    await flushPromises()

    expect(putsOf(mock)).toEqual([
      expect.objectContaining({ url: '/api/services/9/termination-date', body: { terminatedAt: '2030-06-01' } }),
    ])
  })
})
