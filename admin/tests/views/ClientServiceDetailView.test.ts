/**
 * Tests for ClientServiceDetailView: it renders against the real kit and settles on an API refusal,
 * each section of a loaded service saves through its own endpoint with only its own fields and
 * toasts its outcome, a pressed save keeps focus while it
 * runs, a terminal status is confirmed before it is sent, and the sections sit on the account, billing and
 * notes tabs under a header that names the service and its status.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import UiSelect from '@/components/ui/UiSelect.vue'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { useToastStore } from '@/stores/toast'
import { UiTone } from '@/utils/ui/tone'
import ClientServiceDetailView from '@/views/ClientServiceDetailView.vue'
import schema, { BILLING_COLUMNS, NOTES_COLUMNS } from '@/views/ClientServiceDetailView.schema'
import { BillingCycle, PanelSyncField, PanelSyncOutcome, ServiceStatus, type ServiceDetail, type ServiceListItem, type ServiceSyncResult } from '@/types/service'
import { mountClientView } from '../helpers/clientViews'
import { callOf, json, problem } from '../helpers/fetch'
import { mountRouted } from '../helpers/mount'
import { createTestI18n } from '../helpers/i18n'
import { stubRoutes } from '../helpers/routes'
import { toastMessages, toastsOnScreen } from '../helpers/toasts'

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

/** One row of the client's service lookup, a service other than the open one. */
const listed: ServiceListItem = {
  id: 2,
  clientId: 5,
  clientName: 'Ada Lovelace',
  productName: 'Starter',
  domain: null,
  price: 5,
  priceCurrency: 'USD',
  billingCycle: BillingCycle.Monthly,
  status: ServiceStatus.Active,
  nextDueDate: null,
}

/**
 * Mounts the page on a service route with every endpoint answered: the detail, the lookups and a
 * 204 for any section save unless the caller answers the saves itself.
 *
 * @param service - The service the detail endpoint returns.
 * @param save - The answer to every section save.
 * @param extra - Answers for further endpoints, tried before the page's own.
 * @returns The wrapper and the fetch mock.
 */
const mountLoaded = async (
  service: ServiceDetail = detail,
  save: () => Response = () => new Response(null, { status: 204 }),
  extra: Record<string, () => Response> = {},
): Promise<{ wrapper: VueWrapper; mock: ReturnType<typeof vi.fn> }> => {
  const mock = stubRoutes({
    ...extra,
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

/**
 * Opens a tab of the page.
 *
 * @param wrapper - The mounted page.
 * @param tab - The tab to open.
 */
const openTab = async (wrapper: VueWrapper, tab: 'account' | 'billing' | 'notes') => {
  await wrapper.get(`[data-testid="client-service-tab-${tab}"]`).trigger('click')
  await flushPromises()
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('ClientServiceDetailView', () => {
  it('keeps the real header, tabs and the cards of the schema as placeholders while the service loads', async () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)))
    const { wrapper } = await mountRouted(ClientServiceDetailView, '/clients/:id/services/:serviceId', '/clients/5/services/9')

    expect(wrapper.find('[data-testid="client-service-loading"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-service-tab-billing"]').exists()).toBe(true)
    for (const card of schema.columns.flat()) {
      expect(wrapper.get(`[data-testid="ui-detail-card-${card.key}"]`).attributes('aria-busy')).toBe('true')
    }
    expect(wrapper.get('[data-testid="ui-detail-card-hosting"]').text()).toContain('Service Info')
    expect(wrapper.get('[data-testid="ui-detail-card-hosting"]').text()).toContain('Domain')
    expect(wrapper.find('[data-testid="client-service-hosting-form"]').exists()).toBe(false)
  })

  it('draws the cards of the billing and notes tabs from their schema columns', async () => {
    const { wrapper } = await mountLoaded()

    await openTab(wrapper, 'billing')
    for (const card of BILLING_COLUMNS.flat()) {
      expect(wrapper.find(`[data-testid="ui-detail-card-${card.key}"]`).exists()).toBe(true)
    }

    await openTab(wrapper, 'notes')
    for (const card of NOTES_COLUMNS.flat()) {
      expect(wrapper.find(`[data-testid="ui-detail-card-${card.key}"]`).exists()).toBe(true)
    }
  })

  it('mounts without throwing', async () => {
    const wrapper = await mountClientView(ClientServiceDetailView)

    expect(wrapper.exists()).toBe(true)
  })

  it('shows the refusal sentence from the API', async () => {
    const wrapper = await mountClientView(ClientServiceDetailView, 'Server said no')

    expect(wrapper.text()).toContain('Server said no')
  })

  it('names the service and its status in the header', async () => {
    const { wrapper } = await mountLoaded()

    expect(wrapper.get('[data-testid="client-service-header"]').text()).toContain('Starter')
    expect(wrapper.get('[data-testid="client-service-status-badge"]').text()).toBe('Terminated')
  })

  it('shows one tab at a time', async () => {
    const { wrapper } = await mountLoaded()

    expect(wrapper.find('[data-testid="client-service-hosting-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-service-billing-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-service-notes-form"]').exists()).toBe(false)

    await openTab(wrapper, 'billing')
    expect(wrapper.find('[data-testid="client-service-hosting-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-service-billing-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="client-service-automation-form"]').exists()).toBe(true)

    await openTab(wrapper, 'notes')
    expect(wrapper.find('[data-testid="client-service-notes-form"]').exists()).toBe(true)
  })

  it('links to the plan change history of the service', async () => {
    const { wrapper } = await mountLoaded()

    expect(wrapper.get('[data-testid="client-service-plan-changes"]').attributes('href')).toBe('/clients/plan-changes?serviceId=9')
  })

  it('lists the plugin widgets of the service in its slot', async () => {
    const { wrapper } = await mountLoaded(detail, undefined, {
      'GET /api/admin/plugins/slots/admin.service.panels?serviceId=9': () =>
        json({ slotId: 'admin.service.panels', widgets: [{ pluginId: 'sites', id: 'usage', view: 'views/usage.json', title: 'Site usage', order: 1, contextKeys: ['serviceId'] }] }),
    })

    expect(wrapper.get('[data-testid="plugin-slot-admin.service.panels"]').text()).toContain('Site usage')
  })

  it('shows no slot when no plugin offers a widget for the service', async () => {
    const { wrapper } = await mountLoaded(detail, undefined, {
      'GET /api/admin/plugins/slots/admin.service.panels?serviceId=9': () => json({ slotId: 'admin.service.panels', widgets: [] }),
    })

    expect(wrapper.find('[data-testid^="plugin-slot-"]').exists()).toBe(false)
  })

  it('saves the notes alone through the notes endpoint', async () => {
    const { wrapper, mock } = await mountLoaded()
    await openTab(wrapper, 'notes')

    await wrapper.find('[data-testid="client-service-admin-notes"]').setValue('  New note  ')
    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()

    expect(putsOf(mock)).toEqual([
      expect.objectContaining({ url: '/api/services/9/notes', body: { adminNotes: 'New note' } }),
    ])
    expect(toastMessages()).toEqual(['Changes saved successfully.'])
  })

  it('toasts a refusal as a danger toast, then the success when the section is saved again', async () => {
    let refuse = true
    const { wrapper } = await mountLoaded(detail, () =>
      refuse ? problem('Notes are too long.', 400) : new Response(null, { status: 204 }),
    )
    await openTab(wrapper, 'notes')

    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()
    expect(toastsOnScreen()).toEqual([['Notes are too long.', UiTone.Danger]])

    refuse = false
    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()
    expect(toastsOnScreen()).toEqual([
      ['Notes are too long.', UiTone.Danger],
      ['Changes saved successfully.', UiTone.Success],
    ])
  })

  it('labels every section save button with the shared Save', async () => {
    const { wrapper } = await mountLoaded()
    const saves = [
      ['account', 'client-service-save-hosting'],
      ['billing', 'client-service-save-billing'],
      ['billing', 'client-service-save-automation'],
      ['notes', 'client-service-save-notes'],
      ['account', 'client-service-save-termination-date'],
    ] as const
    const names: string[] = []
    for (const [tab, testid] of saves) {
      await openTab(wrapper, tab)
      names.push(wrapper.get(`[data-testid="${testid}"]`).text())
    }

    expect(names.every((name) => name === 'Save')).toBe(true)
  })

  it('keeps the pressed save focusable while it runs and marks its form busy', async () => {
    const { wrapper } = await mountLoaded()
    await openTab(wrapper, 'notes')
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

  it('offers staff the control panel sign-in on every service but a terminated one', async () => {
    const { wrapper: terminated } = await mountLoaded(detail)
    const { wrapper: active } = await mountLoaded({ ...detail, status: ServiceStatus.Active })
    const { wrapper: suspended } = await mountLoaded({ ...detail, status: ServiceStatus.Suspended })

    expect(terminated.find('[data-testid="client-service-panel"]').exists()).toBe(false)
    expect(active.find('[data-testid="client-service-panel"]').exists()).toBe(true)
    expect(suspended.find('[data-testid="client-service-panel"]').exists()).toBe(true)
  })

  it('draws the icon of the plugin that serves the server on the Open panel button, named after the plugin', async () => {
    const server = { name: 'srv', hostname: 'srv.example', ipAddress: null, panelPlugin: { id: 'acme-panel', name: 'Acme Panel', iconVersion: '1.2.0' } }
    const { wrapper } = await mountLoaded({ ...detail, status: ServiceStatus.Active, server })

    const icon = wrapper.get('[data-testid="client-service-panel"]').get('[data-testid="plugin-avatar-image"]')
    expect(icon.attributes('src')).toBe('/api/admin/plugins/acme-panel/icon?v=1.2.0')
    expect(icon.attributes('alt')).toBe('Acme Panel')
  })

  it('keeps the generic icon on the Open panel button when no plugin serves the server or the plugin has no icon', async () => {
    const plain = { name: 'srv', hostname: 'srv.example', ipAddress: null, panelPlugin: null }
    const iconless = { ...plain, panelPlugin: { id: 'acme-panel', name: 'Acme Panel', iconVersion: null } }
    const { wrapper: builtIn } = await mountLoaded({ ...detail, status: ServiceStatus.Active, server: plain })
    const { wrapper: noIcon } = await mountLoaded({ ...detail, status: ServiceStatus.Active, server: iconless })

    for (const wrapper of [builtIn, noIcon]) {
      const button = wrapper.get('[data-testid="client-service-panel"]')
      expect(button.find('[data-testid="plugin-avatar"]').exists()).toBe(false)
      expect(button.find('svg').exists()).toBe(true)
    }
  })

  it('reports a refused control panel sign-in as a danger toast, not as a line in the header', async () => {
    const { wrapper } = await mountLoaded(
      { ...detail, status: ServiceStatus.Active },
      undefined,
      { 'GET /api/provisioning/9/panel-sso': () => problem('A control panel session could not be started.', 502) },
    )
    const toasts = useToastStore(wrapper.vm.$pinia)

    await wrapper.get('[data-testid="client-service-panel"]').trigger('click')
    await flushPromises()

    expect(toasts.toasts).toEqual([expect.objectContaining({ message: 'A control panel session could not be started.', tone: UiTone.Danger })])
    expect(wrapper.find('[data-testid="client-service-panel-error"]').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('A control panel session could not be started.')
  })

  it('opens the panel the API signs in to, with no dialog', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null)
    const { wrapper } = await mountLoaded(
      { ...detail, status: ServiceStatus.Active },
      undefined,
      { 'GET /api/provisioning/9/panel-sso': () => json({ url: 'https://panel.example/sso' }) },
    )

    await wrapper.get('[data-testid="client-service-panel"]').trigger('click')
    await flushPromises()

    expect(open).toHaveBeenCalledWith('https://panel.example/sso', '_blank')
    expect(wrapper.find('[data-testid="client-service-panel-login-dialog"]').exists()).toBe(false)
    open.mockRestore()
  })

  it('shows how to sign in by hand when the panel cannot sign anybody in but has login details, and never a password', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null)
    const { wrapper } = await mountLoaded(
      { ...detail, status: ServiceStatus.Active },
      undefined,
      {
        'GET /api/provisioning/9/panel-sso': () => json({ type: 'PANEL_OPERATION_NOT_SUPPORTED', title: 'Not supported', status: 409, detail: 'The panel does not offer single sign on.' }, 409),
        'GET /api/provisioning/9/panel-login': () => json({ username: 'ada', domain: 'site.example', serverAddress: '203.0.113.9', loginUrl: 'https://panel.example:8083/login/' }),
      },
    )

    await wrapper.get('[data-testid="client-service-panel"]').trigger('click')
    await flushPromises()

    const dialog = wrapper.get('[data-testid="client-service-panel-login-dialog"]')
    const link = dialog.get('[data-testid="client-service-panel-login-link"]')
    expect(link.attributes('href')).toBe('https://panel.example:8083/login/')
    expect(link.attributes('target')).toBe('_blank')
    expect(link.attributes('rel')).toContain('noopener')
    expect(dialog.get('[data-testid="client-service-panel-login-username"]').text()).toBe('ada')
    expect(dialog.get('[data-testid="client-service-panel-login-server"]').text()).toBe('203.0.113.9')
    expect(dialog.find('input').exists()).toBe(false)
    expect(open).not.toHaveBeenCalled()
    expect(toastMessages()).toEqual([])
    open.mockRestore()
  })

  it('toasts the sign on sentence and shows no dialog when the panel offers neither a link nor login details', async () => {
    const { wrapper } = await mountLoaded(
      { ...detail, status: ServiceStatus.Active },
      undefined,
      {
        'GET /api/provisioning/9/panel-sso': () => json({ type: 'PANEL_OPERATION_NOT_SUPPORTED', title: 'Not supported', status: 409, detail: 'The panel does not offer single sign on.' }, 409),
        'GET /api/provisioning/9/panel-login': () => json({ type: 'PANEL_OPERATION_NOT_SUPPORTED', title: 'Not supported', status: 409, detail: 'No login details.' }, 409),
      },
    )

    await wrapper.get('[data-testid="client-service-panel"]').trigger('click')
    await flushPromises()

    expect(toastMessages()).toEqual(['The panel does not offer single sign on.'])
    expect(wrapper.find('[data-testid="client-service-panel-login-dialog"]').exists()).toBe(false)
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

    await openTab(wrapper, 'billing')
    await wrapper.find('[data-testid="client-service-payment-method"]').setValue('Bank transfer')
    await openTab(wrapper, 'notes')
    await wrapper.find('[data-testid="client-service-admin-notes"]').setValue('New note')
    await wrapper.find('[data-testid="client-service-notes-form"]').trigger('submit')
    await flushPromises()

    await openTab(wrapper, 'billing')
    const paymentMethod = wrapper.find('[data-testid="client-service-payment-method"]').element as HTMLInputElement
    expect(paymentMethod.value).toBe('Bank transfer')
  })

  it('refreshes the saved section from the reloaded service', async () => {
    const { wrapper } = await mountLoaded()

    await openTab(wrapper, 'notes')
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
    await openTab(wrapper, 'billing')

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
    await openTab(wrapper, 'billing')

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
    await openTab(wrapper, 'billing')

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

  it('offers the panel sync on a live service that has an account, and on no other', async () => {
    const { wrapper: active } = await mountLoaded({ ...detail, status: ServiceStatus.Active })
    const { wrapper: suspended } = await mountLoaded({ ...detail, status: ServiceStatus.Suspended })
    const { wrapper: terminated } = await mountLoaded(detail)
    const { wrapper: pending } = await mountLoaded({ ...detail, status: ServiceStatus.Pending })
    const { wrapper: unprovisioned } = await mountLoaded({
      ...detail,
      status: ServiceStatus.Active,
      account: { ...detail.account, provisioningRef: null },
    })

    expect(active.get('[data-testid="client-service-sync-panel"]').attributes('disabled')).toBeUndefined()
    expect(suspended.find('[data-testid="client-service-sync-panel"]').exists()).toBe(true)
    expect(terminated.find('[data-testid="client-service-sync-panel"]').exists()).toBe(false)
    expect(pending.find('[data-testid="client-service-sync-panel"]').exists()).toBe(false)
    expect(unprovisioned.get('[data-testid="client-service-sync-panel"]').attributes('disabled')).toBeDefined()
  })

  it('confirms the panel sync first, and sends nothing until it is confirmed', async () => {
    const { wrapper, mock } = await mountLoaded({ ...detail, status: ServiceStatus.Active })

    await wrapper.get('[data-testid="client-service-sync-panel"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-service-sync-dialog"]').exists()).toBe(true)
    expect(mock.mock.calls.some(([url]) => String(url).includes('sync-panel'))).toBe(false)
  })

  it('syncs without the password by default, shows what was done and refreshes the assigned address', async () => {
    const active: ServiceDetail = { ...detail, status: ServiceStatus.Active, account: { ...detail.account } }
    const answer: ServiceSyncResult = {
      fields: [
        { field: PanelSyncField.Status, outcome: PanelSyncOutcome.InSync, panelValue: 'Active' },
        { field: PanelSyncField.IpAddress, outcome: PanelSyncOutcome.Applied, panelValue: '203.0.113.9' },
      ],
    }
    const { wrapper, mock } = await mountLoaded(active, undefined, {
      'POST /api/admin/services/9/sync-panel': () => {
        active.account.dedicatedIp = '203.0.113.9'
        return json(answer)
      },
    })

    await wrapper.get('[data-testid="client-service-sync-panel"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="client-service-sync-confirm"]').trigger('click')
    await flushPromises()

    const sync = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.url === '/api/admin/services/9/sync-panel')
    expect(sync).toMatchObject({ method: 'POST', body: { pushPassword: false } })
    expect(wrapper.get('[data-testid="client-service-sync-outcome-IpAddress"]').text()).toBe('Applied')
    expect((wrapper.get('[data-testid="client-service-dedicated-ip"]').element as HTMLInputElement).value).toBe('203.0.113.9')
    expect(toastMessages()).toEqual(['Service synced with the panel.'])

    await wrapper.get('[data-testid="client-service-sync-close"]').trigger('click')
    expect(wrapper.find('[data-testid="client-service-sync-dialog"]').exists()).toBe(false)
  })

  it('sends the password choice when the box is ticked', async () => {
    const { wrapper, mock } = await mountLoaded({ ...detail, status: ServiceStatus.Suspended }, undefined, {
      'POST /api/admin/services/9/sync-panel': () => json({ fields: [] }),
    })

    await wrapper.get('[data-testid="client-service-sync-panel"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="client-service-sync-push-password"]').trigger('click')
    await wrapper.get('[data-testid="client-service-sync-confirm"]').trigger('click')
    await flushPromises()

    const sync = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.url === '/api/admin/services/9/sync-panel')
    expect(sync).toMatchObject({ body: { pushPassword: true } })
  })

  it('closes the dialog and shows the sentence when the panel cannot be read', async () => {
    const { wrapper } = await mountLoaded({ ...detail, status: ServiceStatus.Active }, undefined, {
      'POST /api/admin/services/9/sync-panel': () => problem('The hosting account could not be read from the control panel.', 502),
    })

    await wrapper.get('[data-testid="client-service-sync-panel"]').trigger('click')
    await flushPromises()
    await wrapper.get('[data-testid="client-service-sync-confirm"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-service-sync-dialog"]').exists()).toBe(false)
    expect(toastMessages()).toEqual(['The hosting account could not be read from the control panel.'])
  })

  it('offers the packages the panel lists in a select when the change package dialog opens', async () => {
    const { wrapper, mock } = await mountLoaded({ ...detail, status: ServiceStatus.Active }, undefined, {
      'GET /api/provisioning/9/packages': () => json({ names: ['Starter', 'Business'] }),
      'POST /api/provisioning/9/change-package': () => new Response(null, { status: 204 }),
    })

    await wrapper.get('[data-testid="client-service-change-package"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-service-new-package"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="client-service-package-prompt"]').text()).toBe('Choose the new hosting package.')
    const select = wrapper.findAllComponents(UiSelect).find((candidate) => candidate.attributes('data-testid') === 'client-service-package-select')!
    expect((select.vm.$props as { options: unknown[] }).options).toEqual([{ value: 'Starter', label: 'Starter' }, { value: 'Business', label: 'Business' }])
    select.vm.$emit('update:modelValue', 'Business')
    await flushPromises()
    await wrapper.get('[data-testid="client-service-package-confirm"]').trigger('click')
    await flushPromises()

    const change = mock.mock.calls.map((_, index) => callOf(mock, index)).find((call) => call.url === '/api/provisioning/9/change-package')
    expect(change).toMatchObject({ method: 'POST', body: { newPackage: 'Business' } })
  })

  it('keeps the free text field when the panel cannot list its packages', async () => {
    const { wrapper } = await mountLoaded({ ...detail, status: ServiceStatus.Active }, undefined, {
      'GET /api/provisioning/9/packages': () => problem('The control panel cannot do that.', 409),
    })

    await wrapper.get('[data-testid="client-service-change-package"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="client-service-package-select"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="client-service-new-package"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="client-service-package-prompt"]').text()).toBe('Enter the new hosting package name.')
  })

  it('labels the address as assigned, not dedicated', async () => {
    const { wrapper } = await mountLoaded({ ...detail, status: ServiceStatus.Active })

    expect(wrapper.text()).toContain('Assigned IP')
    expect(wrapper.text()).not.toContain('Dedicated IP')
  })

  it('hints the address field with an example from the documentation range, never a private address', async () => {
    const { wrapper } = await mountLoaded({ ...detail, status: ServiceStatus.Active })

    expect(wrapper.get('[data-testid="client-service-dedicated-ip"]').attributes('placeholder')).toBe('203.0.113.10')
  })

  it('names the open service in the switcher although the client's lookup page does not list it', async () => {
    const others = [
      { ...listed, id: 3, productName: 'Basic' },
      { ...listed, id: 4, productName: 'Plus' },
    ]
    const { wrapper } = await mountLoaded(detail, undefined, {
      'GET /api/services?': () => json({ items: others, totalCount: 2 }),
    })

    const switcher = wrapper.get('[data-testid="client-service-switcher"]')
    expect(switcher.text()).toContain('#9 Starter')
    expect(switcher.text()).not.toContain('Product/Service')
  })

  it('returns to the services of its client with the back pill', async () => {
    const { wrapper } = await mountLoaded()
    const back = wrapper.get('[data-testid="client-service-back"]')

    expect(back.attributes('href')).toMatch(/^\/clients\/[^/]+\/services$/)
    expect(back.text()).toBe('Services')
  })
})
