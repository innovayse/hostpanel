/**
 * Unit tests for PluginChatCardActions and PluginChatCardAction: a resend, a checked link, a call drawn
 * as an action block, and a row that hides itself while every conditional action is hidden.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { flushPromises } from '@vue/test-utils'
import PluginChatCardAction from '@/components/plugins/view/PluginChatCardAction.vue'
import PluginChatCardActions from '@/components/plugins/view/PluginChatCardActions.vue'
import { providePluginView } from '@/composables/usePluginView'
import { PluginBlockKind, PluginBlockTone } from '@/types/pluginview'
import type { PluginChatResult } from '@/types/pluginview'
import { json } from '../../../helpers/fetch'
import { mountWithApp } from '../../../helpers/mount'
import { stubRouteTable } from '../../../helpers/routes'
import { ext, SAMPLE_PLUGIN } from '../../../helpers/pluginView'

afterEach(() => {
  vi.unstubAllGlobals()
})

/**
 * Mounts the row inside a bare view context.
 *
 * @param result - The card.
 * @returns The wrapper.
 */
const mountRow = async (result: PluginChatResult) => {
  const Host = defineComponent({
    emits: ['resend'],
    setup: (_props, { emit }) => {
      providePluginView({
        pluginId: SAMPLE_PLUGIN,
        values: {},
        externalOrigins: [],
        refreshes: ref({}),
        refresh: () => undefined,
        open: () => undefined,
        adopt: () => () => undefined,
      })
      return () => h(PluginChatCardActions, { result, refresh: [], disabled: false, onResend: () => emit('resend') })
    },
  })
  const wrapper = mountWithApp(Host)
  await flushPromises()
  return wrapper
}

/**
 * Builds a card.
 *
 * @param actions - Its actions.
 * @returns The card.
 */
const card = (actions: PluginChatResult['actions']): PluginChatResult => ({ key: 1, tone: PluginBlockTone.Info, title: 'Done', text: '', details: [], actions })

describe('PluginChatCardActions', () => {
  it('draws a resend, a link and a call', async () => {
    stubRouteTable({})
    const wrapper = await mountRow(card([
      { key: 0, label: 'Try again', link: null, block: null, resend: true },
      { key: 1, label: 'Open', link: { href: '/clients', external: false }, block: null },
      { key: 2, label: 'Undo', link: null, block: { kind: PluginBlockKind.Action, label: 'Undo', route: { method: 'post', path: '/undo' } } },
    ]))

    await wrapper.get('[data-testid="plugin-chat-result-resend"]').trigger('click')
    expect(wrapper.emitted('resend')).toHaveLength(1)
    expect(wrapper.get('[data-testid="plugin-chat-result-link"]').attributes('href')).toBe('/clients')
    expect(wrapper.get('[data-testid="plugin-action-button"]').text()).toBe('Undo')
  })

  it('hides the row while its only conditional action does not hold', async () => {
    stubRouteTable({ [`GET ${ext('state')}`]: () => json({ live: 3 }) })
    const wrapper = await mountRow(card([
      {
        key: 0,
        label: 'Publish 2',
        link: null,
        block: { kind: PluginBlockKind.Action, label: 'Publish 2', route: { method: 'post', path: '/publish' } },
        visibleWhen: { route: { method: 'get', path: '/state' }, field: 'live', equals: [1] },
      },
    ]))

    expect(wrapper.find('[data-testid="plugin-action-button"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-chat-result-actions"]').attributes('style')).toContain('display: none')
  })
})

describe('PluginChatCardAction', () => {
  it('tells whether it shows as soon as it mounts', async () => {
    stubRouteTable({})
    const Host = defineComponent({
      setup: () => {
        providePluginView({
          pluginId: SAMPLE_PLUGIN,
          values: {},
          externalOrigins: [],
          refreshes: ref({}),
          refresh: () => undefined,
          open: () => undefined,
          adopt: () => () => undefined,
        })
        return () => h(PluginChatCardAction, { action: { key: 0, label: 'Open', link: { href: '/clients', external: false }, block: null } })
      },
    })
    const wrapper = mountWithApp(Host)
    await flushPromises()

    expect(wrapper.findComponent(PluginChatCardAction).emitted('visibility')).toEqual([[true]])
  })
})
