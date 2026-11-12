/**
 * Tests for `usePluginChatScroll`: the log follows what is added, stops following once the reader scrolled up, and
 * follows again after a jump to the latest.
 */
import { describe, expect, it } from 'vitest'
import { defineComponent, h, nextTick, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { usePluginChatScroll } from '@/composables/usePluginChatScroll'
import type { PluginChatScroll } from '@/composables/usePluginChatScroll'

/**
 * Mounts a log of a fixed size around the composable.
 *
 * @returns The handle, the element and the content the log watches.
 */
const mountLog = () => {
  const content = ref<string[]>([])
  let scroll: PluginChatScroll | null = null
  const wrapper = mount(defineComponent({
    setup() {
      const handle = usePluginChatScroll(content)
      scroll = handle
      return () => h('div', { ref: handle.log, onScroll: handle.onScroll }, content.value.map((line) => h('p', line)))
    },
  }))
  if (scroll === null) {
    throw new Error('The composable did not run.')
  }
  const element = wrapper.element as HTMLElement
  Object.defineProperty(element, 'scrollHeight', { configurable: true, value: 1000 })
  Object.defineProperty(element, 'clientHeight', { configurable: true, value: 200 })
  return { scroll: scroll as PluginChatScroll, element, content, wrapper }
}

describe('usePluginChatScroll', () => {
  it('follows new content to the end', async () => {
    const { element, content } = mountLog()

    content.value.push('one')
    await nextTick()
    await nextTick()

    expect(element.scrollTop).toBe(1000)
  })

  it('stops following once the reader scrolled up, and follows again after a jump', async () => {
    const { scroll, element, content, wrapper } = mountLog()
    element.scrollTop = 100
    await wrapper.trigger('scroll')
    expect(scroll.following.value).toBe(false)

    content.value.push('one')
    await nextTick()
    await nextTick()
    expect(element.scrollTop).toBe(100)

    scroll.jumpToLatest()
    expect(scroll.following.value).toBe(true)
    expect(element.scrollTop).toBe(1000)
  })

  it('counts the last 40 pixels as the end', async () => {
    const { scroll, element, wrapper } = mountLog()
    element.scrollTop = 770
    await wrapper.trigger('scroll')

    expect(scroll.following.value).toBe(true)
  })
})
