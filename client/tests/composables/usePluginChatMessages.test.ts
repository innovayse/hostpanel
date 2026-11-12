/**
 * Tests for `usePluginChatMessages`: messages written in this session carry their time and history rows do not, the
 * list is capped, each stream event lands on the reply it belongs to, a link passes only the link-block rule, and the
 * turn helpers remove what a failed or retried turn leaves.
 *
 * @module tests/composables/usePluginChatMessages.test
 */

import { describe, expect, it } from 'vitest'
import { ref } from 'vue'
import { usePluginChatMessages } from '~/composables/usePluginChatMessages'
import { PluginChatEventType } from '~/types/pluginchateventtype'
import { PluginChatRole } from '~/types/pluginchatrole'
import type { PluginViewContext } from '~/types/pluginviewcontext'

/**
 * Builds the list over a minimal view context.
 *
 * @returns The list, and the sentences `error` events reported.
 */
const build = () => {
  const errors: string[] = []
  const context = {
    pluginId: 'acme', values: {}, externalOrigins: ['https://*.sites.example.test'], frameOrigins: [], refreshes: ref({}),
  } as unknown as PluginViewContext
  return { chat: usePluginChatMessages(context, () => [], sentence => errors.push(sentence)), errors }
}

describe('usePluginChatMessages', () => {
  it('stamps the time on a message of this session and on none of the history', () => {
    const { chat } = build()

    chat.add(PluginChatRole.User, 'Hi')
    chat.addHistory([{ role: 'assistant', text: 'Earlier' }, { role: 'user', text: 'Before' }, { text: 7 }, 'junk'])

    expect(chat.messages.value.map(message => message.sentAt !== undefined)).toEqual([true, false, false])
    expect(chat.messages.value.map(message => message.role)).toEqual([PluginChatRole.User, PluginChatRole.Assistant, PluginChatRole.User])
  })

  it('keeps the latest 200 messages', () => {
    const { chat } = build()

    for (let i = 0; i < 205; i += 1) {
      chat.add(PluginChatRole.User, `m${i}`)
    }

    expect(chat.messages.value).toHaveLength(200)
    expect(chat.messages.value[0]!.text).toBe('m5')
  })

  it('adds delta text, replaces it on a message, and tracks and clears the status', () => {
    const { chat } = build()
    const reply = chat.add(PluginChatRole.Assistant, '')

    chat.apply(reply, { type: PluginChatEventType.Delta, data: { text: 'One ' } })
    chat.apply(reply, { type: PluginChatEventType.Delta, data: { text: 'two' } })
    chat.apply(reply, { type: PluginChatEventType.Status, data: { text: 'Working' } })
    expect(reply.text).toBe('One two')
    expect(reply.status).toBe('Working')

    chat.apply(reply, { type: PluginChatEventType.Message, data: { text: 'Whole' } })
    chat.apply(reply, { type: PluginChatEventType.Done, data: {} })
    expect(reply.text).toBe('Whole')
    expect(reply.status).toBe('')
  })

  it('reports an error event\'s sentence, the host\'s message included, and ignores unknown events', () => {
    const { chat, errors } = build()
    const reply = chat.add(PluginChatRole.Assistant, '')

    chat.apply(reply, { type: PluginChatEventType.Error, data: { text: 'Busy.' } })
    chat.apply(reply, { type: PluginChatEventType.Error, data: { message: 'Failed.' } })
    chat.apply(reply, { type: 'something-new', data: { text: 'x' } })

    expect(errors).toEqual(['Busy.', 'Failed.'])
    expect(chat.hasContent(reply)).toBe(false)
  })

  it('adds a link only when the link rule admits it', () => {
    const { chat } = build()
    const reply = chat.add(PluginChatRole.Assistant, '')

    chat.apply(reply, { type: PluginChatEventType.Link, data: { text: 'Preview', url: 'https://blog.sites.example.test/' } })
    chat.apply(reply, { type: PluginChatEventType.Link, data: { text: 'Evil', url: 'https://evil.example.test/' } })
    chat.apply(reply, { type: PluginChatEventType.Link, data: { text: 'Script', url: 'javascript:alert(1)' } })
    chat.apply(reply, { type: PluginChatEventType.Link, data: { text: '', url: 'https://blog.sites.example.test/' } })

    expect(reply.links.map(link => link.text)).toEqual(['Preview'])
    expect(chat.hasContent(reply)).toBe(true)
  })

  it('updates a step in place, adds an issue, ignores malformed parts', () => {
    const { chat } = build()
    const reply = chat.add(PluginChatRole.Assistant, '')

    chat.apply(reply, { type: PluginChatEventType.Step, data: { id: 'a', label: 'Build', state: 'running' } })
    chat.apply(reply, { type: PluginChatEventType.Step, data: { id: 'a', label: 'Build', state: 'done' } })
    chat.apply(reply, { type: PluginChatEventType.Step, data: { nonsense: true } })
    chat.apply(reply, { type: PluginChatEventType.Issue, data: { text: 'Theme missing.' } })
    chat.apply(reply, { type: PluginChatEventType.Issue, data: { text: 7 } })

    expect(reply.steps).toHaveLength(1)
    expect(reply.steps[0]!.state).toBe('done')
    expect(reply.issues.map(issue => issue.text)).toEqual(['Theme missing.'])
  })

  it('removes given messages and everything from a key on, and clears', () => {
    const { chat } = build()
    const a = chat.add(PluginChatRole.User, 'a')
    const b = chat.add(PluginChatRole.Assistant, 'b')
    const c = chat.add(PluginChatRole.User, 'c')
    chat.add(PluginChatRole.Assistant, 'd')

    chat.remove(a.key, b.key)
    expect(chat.messages.value.map(message => message.text)).toEqual(['c', 'd'])

    chat.dropFrom(c.key)
    expect(chat.messages.value).toHaveLength(0)

    chat.add(PluginChatRole.User, 'e')
    chat.clear()
    expect(chat.messages.value).toHaveLength(0)
  })
})
