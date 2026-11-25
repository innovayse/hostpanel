/**
 * Mount tests for AddExistingServiceModal: the form posts exactly the API's command for the client,
 * stays closed to an incomplete form, and shows the API's refusal instead of closing.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import type { VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { Pinia } from 'pinia'
import AddExistingServiceModal from '@/components/clients/AddExistingServiceModal.vue'
import UiSelect from '@/components/ui/UiSelect.vue'
import { BillingCycle } from '@/types/service'
import { createTestI18n } from '../../helpers/i18n'
import { json, problem } from '../../helpers/fetch'
import { stubRouteTable, stubRoutes } from '../../helpers/routes'
import type { RouteHandler } from '../../helpers/routes'

/** Request line of the create call. */
const CREATE_LINE = 'POST /api/services/existing'

/** The catalogue and the server list the selects offer. */
const lookups = {
  'GET /api/products': () => json([{ id: 4, name: 'Business Hosting' }]),
  'GET /api/admin/servers/options': () => json([{ id: 2, name: 'srv4', hostname: 'srv4.example.test' }]),
}

/**
 * Builds the API's refusal for a code the admin can confirm past.
 *
 * @param code - The error code.
 * @param detail - The sentence the API wrote.
 * @returns A 409 problem response.
 */
const confirmable = (code: string, detail: string): Response =>
  json({ type: code, title: 'Conflict', status: 409, detail }, 409)

/**
 * Answers the create call with a refusal until the request carries the given confirmation, then with the created id.
 *
 * @param code - The error code of the refusal.
 * @param flag - The confirmation field that lets the request through.
 * @returns The route handler.
 */
const refuseUntilConfirmed = (code: string, flag: 'confirmUnverified' | 'confirmSharedDomain'): RouteHandler => (body) =>
  (body as Record<string, unknown>)[flag] === true ? json(27, 201) : confirmable(code, 'Confirm it to add it anyway.')

/** The same lookups as {@link lookups}, for a route table, which keys the path without the `/api` prefix. */
const tableLookups: Record<string, RouteHandler> = {
  'GET /products': () => json([{ id: 4, name: 'Business Hosting' }]),
  'GET /admin/servers/options': () => json([{ id: 2, name: 'srv4', hostname: 'srv4.example.test' }]),
}

/** The Pinia the modal and the test share. */
let pinia: Pinia

/**
 * Mounts the modal for client 36 and waits for the lookups.
 *
 * @returns The wrapper.
 */
const mountModal = async (): Promise<VueWrapper> => {
  const wrapper = mount(AddExistingServiceModal, {
    props: { clientId: 36 },
    global: { plugins: [createTestI18n(), pinia], stubs: { teleport: true } },
  })
  await flushPromises()
  return wrapper
}

/**
 * Sets one of the modal's selects by its `data-testid`.
 *
 * @param wrapper - The mounted modal.
 * @param testid - The select's `data-testid`.
 * @param value - The value to choose.
 * @returns Promise resolving once the change has rendered.
 */
const choose = async (wrapper: VueWrapper, testid: string, value: unknown): Promise<void> => {
  const select = wrapper.findAllComponents(UiSelect).find(candidate => candidate.attributes('data-testid') === testid)
  if (select === undefined) {
    throw new Error(`No select ${testid}`)
  }
  select.vm.$emit('update:modelValue', value)
  await flushPromises()
}

/**
 * Fills every required field.
 *
 * @param wrapper - The mounted modal.
 * @returns Promise resolving once the form is filled.
 */
const fill = async (wrapper: VueWrapper): Promise<void> => {
  await choose(wrapper, 'add-existing-service-product', 4)
  await choose(wrapper, 'add-existing-service-server', 2)
  await wrapper.get('[data-testid="add-existing-service-domain"]').setValue(' artstone.am ')
  await wrapper.get('[data-testid="add-existing-service-username"]').setValue('artstone')
  await wrapper.get('[data-testid="add-existing-service-amount"] input').setValue('49.99')
}

/**
 * Reads the JSON body of the create call.
 *
 * @param mock - The stubbed `fetch`.
 * @returns The parsed body, undefined when the modal made no create call.
 */
const createBody = (mock: ReturnType<typeof vi.fn>): unknown => {
  const call = mock.mock.calls.find(
    ([url, init]) => `${(init as RequestInit | undefined)?.method ?? 'GET'} ${String(url)}` === CREATE_LINE,
  )
  return call === undefined ? undefined : JSON.parse(String((call[1] as RequestInit).body))
}

describe('AddExistingServiceModal', () => {
  beforeEach(() => {
    pinia = createPinia()
    setActivePinia(pinia)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts the existing account for the client and reports it added', async () => {
    const mock = stubRoutes({ ...lookups, [CREATE_LINE]: () => json(27, 201) })
    const wrapper = await mountModal()

    await fill(wrapper)
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(createBody(mock)).toEqual({
      clientId: 36,
      productId: 4,
      billingCycle: BillingCycle.Annual,
      recurringAmount: 49.99,
      serverId: 2,
      domain: 'artstone.am',
      username: 'artstone',
      startedAt: null,
      confirmUnverified: false,
      confirmSharedDomain: false,
    })
    expect(wrapper.emitted('added')).toHaveLength(1)
  })

  it('does not post an incomplete form', async () => {
    const mock = stubRoutes({ ...lookups, [CREATE_LINE]: () => json(27, 201) })
    const wrapper = await mountModal()

    await wrapper.get('[data-testid="add-existing-service-domain"]').setValue('artstone.am')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(createBody(mock)).toBeUndefined()
    expect(wrapper.get('[data-testid="add-existing-service-save"]').attributes('disabled')).toBeDefined()
  })

  it('shows the API refusal and stays open', async () => {
    stubRoutes({
      ...lookups,
      [CREATE_LINE]: () => problem('A service already holds this account on this server.', 409),
    })
    const wrapper = await mountModal()

    await fill(wrapper)
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[data-testid="add-existing-service-error"]').text()).toContain('already holds this account')
    expect(wrapper.emitted('added')).toBeUndefined()
  })

  it('offers the confirmation of an account the panel cannot check and sends it on the next submit', async () => {
    const mock = stubRouteTable({
      ...tableLookups,
      'POST /services/existing': refuseUntilConfirmed('SERVICE_HOSTING_ACCOUNT_UNVERIFIED', 'confirmUnverified'),
    })
    const wrapper = await mountModal()
    await fill(wrapper)
    expect(wrapper.find('[data-testid="add-existing-service-confirm-unverified"]').exists()).toBe(false)

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[data-testid="add-existing-service-error"]').text()).toContain('Confirm it')
    await wrapper.get('[data-testid="add-existing-service-confirm-unverified"]').trigger('click')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(createBody(mock)).toMatchObject({ confirmUnverified: true, confirmSharedDomain: false })
    expect(wrapper.emitted('added')).toHaveLength(1)
  })

  it('offers the confirmation of a shared domain and sends it on the next submit', async () => {
    const mock = stubRouteTable({
      ...tableLookups,
      'POST /services/existing': refuseUntilConfirmed('SERVICE_DOMAIN_ALREADY_SERVED', 'confirmSharedDomain'),
    })
    const wrapper = await mountModal()
    await fill(wrapper)

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.find('[data-testid="add-existing-service-confirm-unverified"]').exists()).toBe(false)
    await wrapper.get('[data-testid="add-existing-service-confirm-shared-domain"]').trigger('click')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(createBody(mock)).toMatchObject({ confirmUnverified: false, confirmSharedDomain: true })
    expect(wrapper.emitted('added')).toHaveLength(1)
  })

  it('takes the confirmation back when the username changes', async () => {
    stubRouteTable({
      ...tableLookups,
      'POST /services/existing': refuseUntilConfirmed('SERVICE_HOSTING_ACCOUNT_UNVERIFIED', 'confirmUnverified'),
    })
    const wrapper = await mountModal()
    await fill(wrapper)
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    await wrapper.get('[data-testid="add-existing-service-confirm-unverified"]').trigger('click')

    await wrapper.get('[data-testid="add-existing-service-username"]').setValue('other')
    await flushPromises()

    expect(wrapper.find('[data-testid="add-existing-service-confirm-unverified"]').exists()).toBe(false)
  })

  it('closes on cancel', async () => {
    stubRoutes(lookups)
    const wrapper = await mountModal()

    await wrapper.get('[data-testid="add-existing-service-cancel"]').trigger('click')

    expect(wrapper.emitted('close')).toHaveLength(1)
  })
})
