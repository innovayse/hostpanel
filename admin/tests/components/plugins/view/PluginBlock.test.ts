/**
 * Unit tests for PluginBlock: it picks the component of every kind the admin panel draws, draws nothing
 * for a preview (a client page's alone), and throws when used outside a plugin view, which is a host bug.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import type { Component } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import PluginActionBlock from '@/components/plugins/view/PluginActionBlock.vue'
import PluginAlertBlock from '@/components/plugins/view/PluginAlertBlock.vue'
import PluginBadgeBlock from '@/components/plugins/view/PluginBadgeBlock.vue'
import PluginBlock from '@/components/plugins/view/PluginBlock.vue'
import PluginChatBlock from '@/components/plugins/view/PluginChatBlock.vue'
import PluginEmptyBlock from '@/components/plugins/view/PluginEmptyBlock.vue'
import PluginFormBlock from '@/components/plugins/view/PluginFormBlock.vue'
import PluginHeaderBlock from '@/components/plugins/view/PluginHeaderBlock.vue'
import PluginLinkBlock from '@/components/plugins/view/PluginLinkBlock.vue'
import PluginProgressBlock from '@/components/plugins/view/PluginProgressBlock.vue'
import PluginSplitBlock from '@/components/plugins/view/PluginSplitBlock.vue'
import PluginStatBlock from '@/components/plugins/view/PluginStatBlock.vue'
import PluginTableBlock from '@/components/plugins/view/PluginTableBlock.vue'
import PluginTabsBlock from '@/components/plugins/view/PluginTabsBlock.vue'
import PluginTextBlock from '@/components/plugins/view/PluginTextBlock.vue'
import { providePluginView } from '@/composables/usePluginView'
import { PluginBlockKind, PluginFieldType } from '@/types/pluginview'
import type { PluginBlock as PluginBlockData } from '@/types/pluginview'
import { mountWithApp } from '../../../helpers/mount'
import { stubRouteTable } from '../../../helpers/routes'
import { SAMPLE_PLUGIN } from '../../../helpers/pluginView'

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

/**
 * Mounts one block inside a bare view context.
 *
 * @param block - The block.
 * @returns The wrapper.
 */
const mountBlock = async (block: PluginBlockData) => {
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
      return () => h(PluginBlock, { block })
    },
  })
  const wrapper = mountWithApp(Host)
  await flushPromises()
  return wrapper
}

/** A minimal block of every kind the admin panel draws, with the component that must draw it. */
const KINDS: ReadonlyArray<{ block: PluginBlockData; component: Component }> = [
  { block: { kind: PluginBlockKind.Header, title: 'Title' }, component: PluginHeaderBlock },
  { block: { kind: PluginBlockKind.Text, text: 'Text' }, component: PluginTextBlock },
  { block: { kind: PluginBlockKind.Stat, label: 'Stat', value: 1 }, component: PluginStatBlock },
  { block: { kind: PluginBlockKind.Table, source: { route: { method: 'get', path: '/rows' } }, columns: [] }, component: PluginTableBlock },
  {
    block: { kind: PluginBlockKind.Form, fields: [{ key: 'name', label: 'Name', type: PluginFieldType.Text }], submit: { route: { method: 'post', path: '/save' }, label: 'Save' } },
    component: PluginFormBlock,
  },
  { block: { kind: PluginBlockKind.Action, label: 'Go', route: { method: 'post', path: '/go' } }, component: PluginActionBlock },
  { block: { kind: PluginBlockKind.Link, label: 'Link', href: '/clients' }, component: PluginLinkBlock },
  { block: { kind: PluginBlockKind.Badge, text: 'Live' }, component: PluginBadgeBlock },
  { block: { kind: PluginBlockKind.Empty, title: 'Empty' }, component: PluginEmptyBlock },
  { block: { kind: PluginBlockKind.Alert, text: 'Alert' }, component: PluginAlertBlock },
  { block: { kind: PluginBlockKind.Tabs, tabs: [{ id: 'a', title: 'A', blocks: [] }] }, component: PluginTabsBlock },
  { block: { kind: PluginBlockKind.Chat, send: { method: 'post', path: '/chat' } }, component: PluginChatBlock },
  { block: { kind: PluginBlockKind.Progress, label: 'Progress', value: 10 }, component: PluginProgressBlock },
  { block: { kind: PluginBlockKind.Split, panes: [{ blocks: [] }, { blocks: [] }] }, component: PluginSplitBlock },
]

describe('PluginBlock', () => {
  it.each(KINDS)('draws a $block.kind block through its own component', async ({ block, component }) => {
    const wrapper = await mountBlock(block)

    expect(wrapper.findComponent(component).exists()).toBe(true)
  })

  it('draws nothing for a preview, which only a client page may hold', async () => {
    const wrapper = await mountBlock({ kind: PluginBlockKind.Preview })

    expect(wrapper.html()).toBe('<!--v-if-->')
  })

  it('refuses to render outside a plugin view', () => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined)

    expect(() => mount(PluginBlock, { props: { block: { kind: PluginBlockKind.Text, text: 'x' } } })).toThrow('outside a plugin view')
  })
})
