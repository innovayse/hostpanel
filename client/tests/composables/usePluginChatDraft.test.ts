/**
 * Tests for `usePluginChatDraft`: the draft is restored once mounted, mirrored to the tab's storage as it is typed,
 * held while a send is out, forgotten on request, given back in front of newer text, kept apart per plugin, route and
 * values, and the chat works when the tab's storage refuses.
 *
 * @module tests/composables/usePluginChatDraft.test
 */

import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h } from 'vue'
import { mount } from '@vue/test-utils'
import { clearPluginChatDrafts, usePluginChatDraft } from '~/composables/usePluginChatDraft'
import type { PluginChatDraft } from '~/composables/usePluginChatDraft'

afterEach(() => {
  window.sessionStorage.clear()
  vi.restoreAllMocks()
})

/**
 * Runs the composable inside a mounted component.
 *
 * @param pluginId - The plugin.
 * @param path - The chat's route path.
 * @param values - The view's values.
 * @returns The draft handle and the wrapper.
 */
const use = (pluginId = 'acme', path = '/chat', values: Record<string, string> = { siteId: '7' }) => {
  let draft!: PluginChatDraft
  const wrapper = mount(defineComponent({
    setup() {
      draft = usePluginChatDraft(pluginId, path, values)
      return () => h('div')
    },
  }))
  return { draft, wrapper }
}

/**
 * The drafts in the tab.
 *
 * @returns The stored texts.
 */
const stored = (): string[] => Object.keys(window.sessionStorage).map(key => window.sessionStorage.getItem(key) ?? '')

describe('usePluginChatDraft', () => {
  it('starts empty and stores what is typed', () => {
    const { draft } = use()

    expect(draft.draft.value).toBe('')
    draft.draft.value = 'Hello'

    expect(stored()).toEqual(['Hello'])
  })

  it('removes the entry when the field is emptied by hand', () => {
    const { draft } = use()
    draft.draft.value = 'Hello'

    draft.draft.value = ''

    expect(stored()).toEqual([])
  })

  it('restores the stored draft when it is mounted again', () => {
    use().draft.draft.value = 'Hello'

    expect(use().draft.draft.value).toBe('Hello')
  })

  it('keeps one draft per plugin, route and values', () => {
    use().draft.draft.value = 'Hello'

    expect(use('other').draft.draft.value).toBe('')
    expect(use('acme', '/other').draft.draft.value).toBe('')
    expect(use('acme', '/chat', { siteId: '8' }).draft.draft.value).toBe('')
  })

  it('takes the trimmed text out of the field but keeps the stored copy until it is forgotten', () => {
    const { draft } = use()
    draft.draft.value = '  Hello  '

    expect(draft.take()).toBe('Hello')
    expect(draft.draft.value).toBe('')
    expect(stored()).toEqual(['  Hello  '])

    draft.forget()
    expect(stored()).toEqual([])
  })

  it('forgets into whatever the field holds by then', () => {
    const { draft } = use()
    draft.draft.value = 'Sent'
    draft.take()
    draft.draft.value = 'Next'

    draft.forget()

    expect(stored()).toEqual(['Next'])
  })

  it('gives text back to an empty field, and in front of newer text otherwise', () => {
    const { draft } = use()
    draft.giveBack('Sent')
    expect(draft.draft.value).toBe('Sent')

    draft.draft.value = 'Newer'
    draft.giveBack('Older')
    expect(draft.draft.value).toBe('Older\nNewer')
  })

  it('works without a draft kept when the tab\'s storage refuses', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('denied')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('denied')
    })
    const { draft } = use()

    expect(() => {
      draft.draft.value = 'Hello'
    }).not.toThrow()
    expect(draft.draft.value).toBe('Hello')
  })

  it('makes the same key for the same values in any order, sorted by code unit and not by locale', () => {
    use('acme', '/chat', { a: '1', B: '2' }).draft.draft.value = 'Hello'

    expect(Object.keys(window.sessionStorage)).toEqual([
      'hostpanel.plugin-chat-draft:acme:/chat:[["B","2"],["a","1"]]',
    ])
    expect(use('acme', '/chat', { B: '2', a: '1' }).draft.draft.value).toBe('Hello')
  })

  it('clears every chat draft of the tab and nothing else', () => {
    window.sessionStorage.setItem('hostpanel.plugin-chat-draft:acme:/chat:[]', 'One')
    window.sessionStorage.setItem('hostpanel.plugin-chat-draft:other:/chat:[]', 'Two')
    window.sessionStorage.setItem('unrelated', 'Kept')

    clearPluginChatDrafts()

    expect(Object.keys(window.sessionStorage)).toEqual(['unrelated'])
  })
})
