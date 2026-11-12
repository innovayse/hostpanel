/**
 * Unit tests for PluginChatEmpty: the introduction, the placeholder as the sentence when there is none, and the chips, a
 * press giving the message to send (the text, else the label).
 */
import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PluginChatEmpty from '@/components/plugins/view/PluginChatEmpty.vue'

/**
 * Mounts the empty state.
 *
 * @param props - Props over the defaults.
 * @returns The wrapper.
 */
const mountEmpty = (props: Record<string, unknown> = {}) =>
  mount(PluginChatEmpty, { props: { placeholder: 'Describe a change', suggestions: [], disabled: false, ...props } })

describe('PluginChatEmpty', () => {
  it('shows the introduction as text', () => {
    const wrapper = mountEmpty({ intro: { title: 'What should change?', text: 'Say it <b>plainly</b>.' } })

    expect(wrapper.get('[data-testid="plugin-chat-intro-title"]').text()).toBe('What should change?')
    expect(wrapper.get('[data-testid="plugin-chat-intro-text"]').text()).toBe('Say it <b>plainly</b>.')
    expect(wrapper.find('b').exists()).toBe(false)
  })

  it('falls back to the placeholder as the sentence, with no title', () => {
    const wrapper = mountEmpty()

    expect(wrapper.get('[data-testid="plugin-chat-intro-text"]').text()).toBe('Describe a change')
    expect(wrapper.find('[data-testid="plugin-chat-intro-title"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-suggestions"]').exists()).toBe(false)
  })

  it('offers each chip and hands over its text, or its label without one', async () => {
    const wrapper = mountEmpty({ suggestions: [{ label: 'Gallery', text: 'Add a photo gallery.' }, { label: 'Blog' }] })

    await wrapper.get('[data-testid="plugin-chat-suggestion-0"]').trigger('click')
    await wrapper.get('[data-testid="plugin-chat-suggestion-1"]').trigger('click')

    expect(wrapper.emitted('pick')).toEqual([['Add a photo gallery.'], ['Blog']])
  })

  it('disables the chips while a reply streams', () => {
    const wrapper = mountEmpty({ suggestions: [{ label: 'Blog' }], disabled: true })

    expect(wrapper.get('[data-testid="plugin-chat-suggestion-0"]').attributes('disabled')).toBeDefined()
  })
})
