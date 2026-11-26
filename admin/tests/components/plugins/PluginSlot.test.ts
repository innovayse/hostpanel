/**
 * Unit tests for PluginSlot: nothing renders without widgets, one card per widget with its view as the body, a refused, a
 * pending and a healthy widget side by side leave each other alone, another record never shows
 * the previous record's widgets, and Hide goes through the slot's setting row.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import PluginSlot from '@/components/plugins/PluginSlot.vue'
import { PluginSlotId } from '@/types/pluginslot'
import type { PluginSlotWidgetDto } from '@/types/pluginslot'
import { json, problem } from '../../helpers/fetch'
import { mountWithApp } from '../../helpers/mount'
import { stubRoutes } from '../../helpers/routes'

/**
 * Builds a widget.
 *
 * @param id - Id of the widget; also its title.
 * @param order - Position in the slot.
 * @returns The widget.
 */
const widget = (id: string, order = 1): PluginSlotWidgetDto => ({
  pluginId: 'sites', id, view: `views/${id}.json`, title: id, order, contextKeys: ['clientId'],
})

/**
 * Builds the host's answer for the client panels slot.
 *
 * @param widgets - The widgets of the slot.
 * @returns The slot index.
 */
const clientPanels = (...widgets: PluginSlotWidgetDto[]) => ({ slotId: 'admin.client.panels', widgets })

/**
 * Builds the answer of a widget's view read: a view with one text block.
 *
 * @param text - The text the block shows.
 * @returns The widget with its view.
 */
const widgetView = (text: string) => ({
  pluginId: 'sites', id: 'any', title: null, externalOrigins: [], view: { schema: 1, blocks: [{ kind: 'text', text }] },
})

/** The page: a slot of the client panels. */
const Page = defineComponent({
  props: { clientId: { type: Number, required: true } },
  setup: (props) => () => h(PluginSlot, { slotId: PluginSlotId.AdminClientPanels, context: { clientId: props.clientId } }),
})

/** The hide setting row of the client panels slot. */
const hiddenRow = { id: 5, key: 'plugins.hidden_slot_widgets', value: null, isSecret: false, hasValue: false }

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
})

describe('PluginSlot', () => {
  it('renders nothing when no plugin offers a widget', async () => {
    stubRoutes({ 'GET /api/admin/plugins/slots/admin.client.panels?': () => json(clientPanels()) })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    expect(wrapper.find('[data-testid^="plugin-slot-"]').exists()).toBe(false)
    expect(wrapper.text()).toBe('')
  })

  it('renders nothing when the slot cannot be loaded', async () => {
    stubRoutes({ 'GET /api/admin/plugins/slots/admin.client.panels?': () => problem('Client not found.', 404) })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    expect(wrapper.text()).toBe('')
  })

  it('renders the body of each widget from its view', async () => {
    const mock = stubRoutes({
      'GET /api/admin/plugins/slots/admin.client.panels/widgets/sites/ok': () => json(widgetView('usage is fine')),
      'GET /api/admin/plugins/slots/admin.client.panels?': () => json(clientPanels(widget('ok', 1))),
    })
    const wrapper = mountWithApp(Page, { props: { clientId: 7 } })
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-widget-sites-ok"]').text()).toContain('usage is fine')
    expect(mock.mock.calls.map((call) => String(call[0]))).toContain('/api/admin/plugins/slots/admin.client.panels/widgets/sites/ok?clientId=7')
  })

  it('keeps a refused and a pending widget from breaking the healthy one', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) => {
      if (url.includes('/widgets/sites/boom')) {
        return Promise.resolve(problem('Plugin exploded.', 502))
      }
      if (url.includes('/widgets/sites/slow')) {
        return new Promise<Response>(() => undefined)
      }
      if (url.includes('/widgets/sites/ok')) {
        return Promise.resolve(json(widgetView('fine')))
      }
      return Promise.resolve(json(clientPanels(widget('boom', 1), widget('slow', 2), widget('ok', 3))))
    }))
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-widget-note-sites-boom"]').text()).toBe('This widget could not be shown.')
    expect(wrapper.find('[data-testid="plugin-widget-loading-sites-slow"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-widget-sites-ok"]').text()).toContain('fine')

    await vi.advanceTimersByTimeAsync(10_000)

    expect(wrapper.get('[data-testid="plugin-widget-note-sites-slow"]').text()).toBe('This widget took too long to load.')
    expect(wrapper.get('[data-testid="plugin-widget-sites-ok"]').text()).toContain('fine')
  })

  it('leaves no timer behind when it is destroyed while a widget is pending', async () => {
    vi.stubGlobal('fetch', vi.fn((url: string) =>
      url.includes('/widgets/')
        ? new Promise<Response>(() => undefined)
        : Promise.resolve(json(clientPanels(widget('slow', 1)))),
    ))
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()
    expect(vi.getTimerCount()).toBe(1)

    wrapper.unmount()

    expect(vi.getTimerCount()).toBe(0)
  })

  it('shows only the new record\'s widgets after the page moves on, even if the old answer is late', async () => {
    let releaseOld: (response: Response) => void = () => undefined
    const old = new Promise<Response>((resolve) => {
      releaseOld = resolve
    })
    const mock = vi.fn((url: string) =>
      url.includes('clientId=1') ? old : Promise.resolve(json(clientPanels(widget('two')))),
    )
    vi.stubGlobal('fetch', mock)
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    await wrapper.setProps({ clientId: 2 })
    await flushPromises()
    releaseOld(json(clientPanels(widget('one'))))
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-widget-sites-two"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="plugin-widget-sites-one"]').exists()).toBe(false)
  })

  it('hides a widget through the slot setting', async () => {
    const routes = stubRoutes({
      'GET /api/admin/plugins/slots/admin.client.panels?': () => json(clientPanels(widget('ok', 1), widget('other', 2))),
      'GET /api/admin/settings': () => json([hiddenRow]),
      'PUT /api/admin/settings/5': () => new Response(null, { status: 204 }),
    })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    await wrapper.get('[data-testid="plugin-widget-hide-sites-ok"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-testid="plugin-widget-sites-ok"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-widget-sites-other"]').exists()).toBe(true)
    const put = routes.mock.calls.find((call) => call[1]?.method === 'PUT')
    expect(JSON.parse(String(put?.[1]?.body))).toEqual({ value: 'admin.client.panels:sites:ok' })
  })

  it('shows the refusal and keeps the widget when the row was never seeded', async () => {
    stubRoutes({
      'GET /api/admin/plugins/slots/admin.client.panels?': () => json(clientPanels(widget('ok', 1))),
      'GET /api/admin/settings': () => json([]),
    })
    const wrapper = mountWithApp(Page, { props: { clientId: 1 } })
    await flushPromises()

    await wrapper.get('[data-testid="plugin-widget-hide-sites-ok"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-testid="plugin-slot-error"]').text()).toContain('does not exist on this server')
    expect(wrapper.find('[data-testid="plugin-widget-sites-ok"]').exists()).toBe(true)
  })
})
