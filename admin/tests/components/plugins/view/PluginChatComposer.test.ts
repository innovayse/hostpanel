/**
 * Unit tests for PluginChatComposer: the field is the chat's v-model, Enter and the Send button ask to send (Send disabled
 * when nothing can be sent), Send becomes Stop while a reply streams, and the hint and length cap are there.
 */
import { describe, expect, it } from 'vitest'
import PluginChatComposer from '@/components/plugins/view/PluginChatComposer.vue'
import { mountWithApp } from '../../../helpers/mount'

/**
 * Mounts the composer.
 *
 * @param props - Props over the defaults.
 * @returns The wrapper.
 */
const mountComposer = (props: Record<string, unknown> = {}) =>
  mountWithApp(PluginChatComposer, { props: { modelValue: 'Hello', placeholder: 'Say it', streaming: false, canSend: true, ...props } })

describe('PluginChatComposer', () => {
  it('shows the placeholder, the hint and the text', () => {
    const wrapper = mountComposer()

    expect(wrapper.get('[data-testid="plugin-chat-input"]').attributes('placeholder')).toBe('Say it')
    expect((wrapper.get('[data-testid="plugin-chat-input"]').element as HTMLTextAreaElement).value).toBe('Hello')
    expect(wrapper.get('[data-testid="plugin-chat-hint"]').text()).toBe('Enter to send, Shift+Enter for a new line')
  })

  it('passes typing on as the model', async () => {
    const wrapper = mountComposer()

    await wrapper.get('[data-testid="plugin-chat-input"]').setValue('Hi there')

    expect(wrapper.emitted('update:modelValue')).toEqual([['Hi there']])
  })

  it('asks to send from the button and from Enter, and has the button off when nothing can be sent', async () => {
    const wrapper = mountComposer()
    await wrapper.get('[data-testid="plugin-chat-send"]').trigger('click')
    await wrapper.get('[data-testid="plugin-chat-input"]').trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('send')).toHaveLength(2)

    await wrapper.setProps({ canSend: false })
    expect(wrapper.get('[data-testid="plugin-chat-send"]').attributes('disabled')).toBeDefined()
  })

  it('turns Send into a labelled Stop while a reply streams', async () => {
    const wrapper = mountComposer({ streaming: true })

    expect(wrapper.find('[data-testid="plugin-chat-send"]').exists()).toBe(false)
    const stop = wrapper.get('[data-testid="plugin-chat-stop"]')
    expect(stop.attributes('aria-label')).toBe('Stop')

    await stop.trigger('click')
    expect(wrapper.emitted('stop')).toHaveLength(1)
  })

  it('keeps Send and Stop as one button element, so the focus is not lost when a reply starts', async () => {
    const wrapper = mountComposer()
    const button = wrapper.get('[data-testid="plugin-chat-send"]').element

    await wrapper.setProps({ streaming: true })

    expect(wrapper.get('[data-testid="plugin-chat-stop"]').element).toBe(button)
    expect(wrapper.find('[data-testid="plugin-chat-send"]').exists()).toBe(false)
    expect(button.getAttribute('disabled')).toBeNull()
  })

  it('caps the field at the length it is given', () => {
    const wrapper = mountComposer({ maxLength: 80 })

    expect(wrapper.get('[data-testid="plugin-chat-input"]').attributes('maxlength')).toBe('80')
  })
})
