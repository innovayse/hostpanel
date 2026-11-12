/**
 * Tests for PluginSlotWidget against a real stub backend (no mocked api or store).
 *
 * A working widget reads its own view with the slot's context and renders it. A widget that throws,
 * one whose view is refused, one whose view is slow and one whose first data is slow each give way to
 * a neutral note within the widget's own limit, and the host content and the other widgets are left
 * intact. The limit is cleared once the widget is ready. A late reply is dropped. A new context starts a
 * fresh attempt and ignores the replies to the old one.
 *
 * Timing tests fake only `setTimeout`/`clearTimeout`, move the clock with `vi.advanceTimersByTime` alone, and wait for
 * the socket with `until`/`settle` (`setImmediate`), never `vi.waitFor`, which polls on the faked timer.
 *
 * @module tests/components/plugin/SlotWidget.test
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { VueWrapper } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import PluginSlotWidget from '~/components/plugin/SlotWidget.vue'
import { mountClient } from '~/tests/helpers/mountClient'
import { PLUGIN_COMPONENTS } from '~/tests/helpers/mountPlugin'
import {
  readingView, slotEntry, slotView, slotWidgetPath, STATUS_ROUTE_PREFIX, throwingView, WIDGET_NOT_FOUND, workingView,
} from '~/tests/helpers/pluginSlotFixture'
import { settle, until } from '~/tests/helpers/settle'
import { startTransport } from '~/tests/helpers/stubTransport'
import { heldReply, pathOf, queryOf, requestsTo, routeTable } from '~/tests/helpers/stubRoutes'
import type { StubRouteTable } from '~/tests/helpers/stubRoutes'
import type { StubReply, StubServer } from '~/tests/helpers/stubServer'
import { PluginBlockKind } from '~/types/pluginblockkind'
import { PluginSlotId } from '~/types/pluginslotid'
import { waitUntil } from '~/tests/helpers/waitUntil'

/** The stub playing the BFF. */
let stub: StubServer

/** The replies of the current test; a test changes them before it mounts. */
let table: StubRouteTable

/** What the plugin's own routes answer; a test may hold them back. */
let routeReply: () => StubReply | Promise<StubReply>

/** The options a test may pass when it mounts widgets. */
interface MountOptions {
  /** The slot the widgets sit in. */
  slotId?: PluginSlotId
  /** The slot's context. */
  context?: Record<string, string>
  /** Each widget's time limit; the component's default when omitted. */
  timeoutMs?: number
  /** Collects whatever reaches the app's error handler. */
  errors?: unknown[]
}

/**
 * Mounts widgets beside a host paragraph, the way a host page holds them.
 *
 * @param ids - The widget ids.
 * @param options - Slot, context, time limit and error sink.
 * @returns The mounted page.
 */
const mountPageWith = (ids: string[], options: MountOptions = {}): VueWrapper => mountClient(defineComponent({
  setup: () => () => h('div', [
    h('p', { 'data-testid': 'host-content' }, 'Host page'),
    ...ids.map(id => h(PluginSlotWidget, {
      key: id,
      slotId: options.slotId ?? PluginSlotId.DashboardCards,
      widget: slotEntry(id),
      ...(options.context === undefined ? {} : { context: options.context }),
      ...(options.timeoutMs === undefined ? {} : { timeoutMs: options.timeoutMs }),
    })),
  ]),
}), { components: PLUGIN_COMPONENTS, errorHandler: error => options.errors?.push(error) })

/**
 * Mounts one widget on its own, so a test can change its props.
 *
 * @param id - The widget id.
 * @param slotId - The slot.
 * @param context - The slot's context.
 * @returns The mounted widget.
 */
const mountWidget = (id: string, slotId: PluginSlotId, context: Record<string, string>): VueWrapper =>
  mountClient(PluginSlotWidget, { props: { slotId, widget: slotEntry(id), context }, components: PLUGIN_COMPONENTS })

/**
 * The selector of a widget's note.
 *
 * @param id - The widget id.
 * @returns The selector.
 */
const note = (id: string): string => `[data-testid="plugin-slot-unavailable-acme-sites-${id}"]`

/**
 * The selector of a widget's loading state.
 *
 * @param id - The widget id.
 * @returns The selector.
 */
const loading = (id: string): string => `[data-testid="plugin-slot-loading-acme-sites-${id}"]`

/**
 * The selector of a widget's rendered view.
 *
 * @param id - The widget id.
 * @returns The selector.
 */
const rendered = (id: string): string => `[data-testid="plugin-slot-widget-acme-sites-${id}"]`

/**
 * Moves the faked clock and lets the component react.
 *
 * @param ms - How far.
 * @returns Resolves after the reaction.
 */
const advance = async (ms: number): Promise<void> => {
  vi.advanceTimersByTime(ms)
  await settle()
}

/**
 * Fakes the clock the time limit runs on, leaving `setImmediate` real for the socket.
 *
 * @returns Nothing.
 */
const fakeClock = (): void => {
  vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] })
}

describe('PluginSlotWidget', () => {
  beforeEach(async () => {
    table = {
      [`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'ok')}`]: { body: workingView() },
      [`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'broken')}`]: { body: throwingView() },
      [`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'slow')}`]: { body: readingView() },
      [`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'gone')}`]: WIDGET_NOT_FOUND,
    }
    routeReply = () => ({ body: { status: 'live' } })
    const answer = routeTable(table)
    stub = await startTransport(request => pathOf(request).startsWith(STATUS_ROUTE_PREFIX) ? routeReply() : answer(request))
  })

  afterEach(async () => {
    vi.useRealTimers()
    await stub.close()
  })

  describe('with real time', () => {
    it('shows its own loading state, then renders its view', async () => {
      const page = mountPageWith(['ok'])
      expect(page.find(loading('ok')).exists()).toBe(true)

      await waitUntil(() => expect(page.find(rendered('ok')).exists()).toBe(true))
      expect(page.text()).toContain('Site is live')
    })

    it('reads its view with the slot\'s context and language, and fills its routes from it', async () => {
      table[`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'slow')}`] = { body: readingView() }

      const page = mountPageWith(['slow'], { slotId: PluginSlotId.ServiceTabs, context: { serviceId: '4' } })

      await waitUntil(() => expect(page.find(rendered('slow')).text()).toContain('live'))
      const [read] = requestsTo(stub.requests, slotWidgetPath(PluginSlotId.ServiceTabs, 'slow'))
      expect(queryOf(read!)).toEqual({ serviceId: '4' })
      expect(read?.headers['x-language']).toBe('en')
    })

    it('isolates a widget that throws: a note in its place, the host and the other widget intact, nothing reported', async () => {
      const errors: unknown[] = []
      const page = mountPageWith(['broken', 'ok'], { errors })

      await waitUntil(() => expect(page.find(note('broken')).exists()).toBe(true))
      await waitUntil(() => expect(page.find(rendered('ok')).text()).toBe('Site is live'))
      expect(page.find(note('broken')).text()).toBe('plugins.unavailable')
      expect(page.find(rendered('broken')).exists()).toBe(false)
      expect(page.get('[data-testid="host-content"]').text()).toBe('Host page')
      expect(errors).toEqual([])
    })
  })

  describe('with a faked clock', () => {
    it('1. a held view: still loading at 4999 ms, the note at 5000 ms, the other widget untouched', async () => {
      table[`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'slow')}`] = { hold: true }
      fakeClock()
      const page = mountPageWith(['slow', 'ok'])
      await until(() => page.find(rendered('ok')).exists(), 'the working widget rendered')

      await advance(4999)
      expect(page.find(note('slow')).exists()).toBe(false)
      expect(page.find(loading('slow')).exists()).toBe(true)

      await advance(1)
      expect(page.find(note('slow')).exists()).toBe(true)
      expect(page.find(note('ok')).exists()).toBe(false)
      expect(page.find('[data-testid="host-content"]').exists()).toBe(true)
    })

    it('2. the view in time but a block\'s first read held: the note at 5000 ms', async () => {
      const held = heldReply()
      routeReply = held.reply
      fakeClock()
      const page = mountPageWith(['slow', 'ok'])
      await until(() => page.find(rendered('slow')).exists() && held.waiting() === 1, 'the view rendered and its first read started')

      await advance(4999)
      expect(page.find(note('slow')).exists()).toBe(false)

      await advance(1)
      expect(page.find(note('slow')).exists()).toBe(true)
      expect(page.find(rendered('ok')).text()).toBe('Site is live')
    })

    it('3. the view and its first reads in time: the limit is cleared at once, and the content stays', async () => {
      fakeClock()
      const page = mountPageWith(['slow'])
      await until(() => page.find(rendered('slow')).exists() && page.find(rendered('slow')).text().includes('live'), 'the first read answered')
      await settle()

      expect(vi.getTimerCount()).toBe(0)
      await advance(10_000)
      expect(page.find(note('slow')).exists()).toBe(false)
      expect(page.find(rendered('slow')).text()).toContain('live')
    })

    it('3b. a view with no reads is ready as soon as it renders, so no limit is left', async () => {
      fakeClock()
      const page = mountPageWith(['ok'])
      await until(() => page.find(rendered('ok')).exists(), 'the view rendered')
      await settle()

      expect(vi.getTimerCount()).toBe(0)
    })

    it('4. a view that arrives after the limit is dropped: the note stays', async () => {
      const held = heldReply()
      table[`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'ok')}`] = held.reply
      fakeClock()
      const page = mountPageWith(['ok'])
      await until(() => held.waiting() === 1, 'the view read reached the stub')

      await advance(5000)
      expect(page.find(note('ok')).exists()).toBe(true)

      held.release({ body: workingView() })
      await settle()
      expect(page.find(note('ok')).exists()).toBe(true)
      expect(page.find(rendered('ok')).exists()).toBe(false)
    })

    it('5. a new context reads again with it, and the late reply to the old one is ignored', async () => {
      const first = heldReply()
      const path = `GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')}`
      table[path] = request => queryOf(request).serviceId === '4'
        ? first.reply()
        : { body: slotView('ok', [{ kind: PluginBlockKind.Text, text: 'Service 5' }]) }
      fakeClock()
      const widget = mountWidget('ok', PluginSlotId.ServiceTabs, { serviceId: '4' })
      await until(() => first.waiting() === 1, 'the first view read reached the stub')

      await widget.setProps({ context: { serviceId: '5' } })
      await until(() => widget.find(rendered('ok')).exists(), 'the second view rendered')
      first.release({ body: slotView('ok', [{ kind: PluginBlockKind.Text, text: 'Service 4' }]) })
      await settle()

      expect(requestsTo(stub.requests, slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')).map(queryOf))
        .toEqual([{ serviceId: '4' }, { serviceId: '5' }])
      expect(widget.find(rendered('ok')).text()).toBe('Service 5')
      expect(widget.find(note('ok')).exists()).toBe(false)
    })

    it('5b. a new context gives a widget that showed the note a fresh attempt', async () => {
      table[`GET ${slotWidgetPath(PluginSlotId.ServiceTabs, 'ok')}`] = request => queryOf(request).serviceId === '4'
        ? WIDGET_NOT_FOUND
        : { body: workingView() }
      fakeClock()
      const widget = mountWidget('ok', PluginSlotId.ServiceTabs, { serviceId: '4' })
      await until(() => widget.find(note('ok')).exists(), 'the refusal reached the widget')

      await widget.setProps({ context: { serviceId: '5' } })
      await until(() => widget.find(rendered('ok')).exists(), 'the second view rendered')

      expect(widget.find(note('ok')).exists()).toBe(false)
    })

    it('6. a refused view read shows the note at once and leaves no limit running', async () => {
      fakeClock()
      const page = mountPageWith(['gone', 'ok'])
      await until(() => page.find(note('gone')).exists(), 'the refusal reached the widget')
      await until(() => page.find(rendered('ok')).exists(), 'the working widget rendered')
      await settle()

      expect(vi.getTimerCount()).toBe(0)
    })

    it('7. a throwing block shows the note and leaves no limit running', async () => {
      fakeClock()
      const page = mountPageWith(['broken'])
      await until(() => page.find(note('broken')).exists(), 'the error was caught')
      await settle()

      expect(vi.getTimerCount()).toBe(0)
    })

    it('times each widget on its own limit', async () => {
      table[`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'slow')}`] = { hold: true }
      fakeClock()
      const page = mountPageWith(['slow'], { timeoutMs: 1000 })

      await advance(999)
      expect(page.find(note('slow')).exists()).toBe(false)
      await advance(1)
      expect(page.find(note('slow')).exists()).toBe(true)
    })

    it('leaves no timer behind once unmounted', async () => {
      table[`GET ${slotWidgetPath(PluginSlotId.DashboardCards, 'slow')}`] = { hold: true }
      fakeClock()
      const page = mountPageWith(['slow'])
      expect(vi.getTimerCount()).toBe(1)

      page.unmount()

      expect(vi.getTimerCount()).toBe(0)
    })
  })
})
