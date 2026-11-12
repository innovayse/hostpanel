/**
 * Unit tests for PluginChatMessage: the operator's message as a bubble with its time, the reply as plain text beside an
 * avatar and an optional name, the Markdown and caret while it is written, the typing dots before its first words, the
 * status with a spinner, the copy button once it is finished, and the time only for a message of this session.
 */
import { describe, expect, it, vi } from 'vitest'
import PluginChatMessage from '@/components/plugins/view/PluginChatMessage.vue'
import { PluginChatRole } from '@/types/pluginview'
import type { PluginChatMessage as Message } from '@/types/pluginview'
import { mountWithApp } from '../../../helpers/mount'

/**
 * Builds a message.
 *
 * @param over - What differs from an empty reply.
 * @returns The message.
 */
const message = (over: Partial<Message> = {}): Message =>
  ({ key: 1, role: PluginChatRole.Assistant, text: '', links: [], steps: [], results: [], issues: [], status: '', ...over })

/**
 * Mounts a message.
 *
 * @param over - What differs from an empty reply.
 * @param props - Further props.
 * @returns The wrapper.
 */
const mountMessage = (over: Partial<Message> = {}, props: Record<string, unknown> = {}) =>
  mountWithApp(PluginChatMessage, { props: { message: message(over), refresh: [], disabled: false, writing: false, ...props } })

describe('PluginChatMessage', () => {
  it('shows the operator\'s message as typed in a bubble, with the time of this session', () => {
    const wrapper = mountMessage({ role: PluginChatRole.User, text: '**Keep** <b>this</b>', sentAt: new Date(2026, 8, 8, 15, 7).getTime() })

    expect(wrapper.get('[data-testid="plugin-chat-user"]').text()).toBe('**Keep** <b>this</b>')
    expect(wrapper.find('strong').exists()).toBe(false)
    expect(wrapper.get('[data-testid="plugin-chat-time"]').text()).toMatch(/3:07|15:07/)
    expect(wrapper.get('[data-testid="plugin-chat-time"]').classes()).not.toContain('opacity-0')
  })

  it('shows no time for a message read from the history', () => {
    expect(mountMessage({ role: PluginChatRole.User, text: 'Earlier' }).find('[data-testid="plugin-chat-time"]').exists()).toBe(false)
  })

  it('shows a reply as plain text beside the avatar, signed with the name when there is one', () => {
    const wrapper = mountMessage({ text: 'Done **now**' }, { assistantLabel: 'Site assistant' })

    expect(wrapper.find('[data-testid="ui-chat-avatar"]').exists()).toBe(true)
    expect(wrapper.get('[data-testid="plugin-chat-assistant-label"]').text()).toBe('Site assistant')
    expect(wrapper.get('[data-testid="plugin-chat-assistant"] strong').text()).toBe('now')
    expect(wrapper.get('[data-testid="plugin-chat-assistant"]').element.parentElement?.className).toContain('max-w-[75ch]')
  })

  it('shows no name line without a name', () => {
    expect(mountMessage({ text: 'Hi' }).find('[data-testid="plugin-chat-assistant-label"]').exists()).toBe(false)
  })

  it('shows the typing dots while a reply has shown nothing, then the text with a caret', async () => {
    const wrapper = mountMessage({}, { writing: true })
    expect(wrapper.find('[data-testid="ui-typing-dots"]').exists()).toBe(true)

    await wrapper.setProps({ message: message({ text: 'Working' }) })

    expect(wrapper.find('[data-testid="ui-typing-dots"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="plugin-chat-assistant"] [data-testid="markdown-caret"]').exists()).toBe(true)
  })

  it('shows the status with a spinner only while the reply is written', async () => {
    const wrapper = mountMessage({ status: 'Building pages…' }, { writing: true })

    expect(wrapper.get('[data-testid="plugin-chat-status"]').text()).toBe('Building pages…')
    expect(wrapper.find('.animate-spin').exists()).toBe(true)
    expect(wrapper.find('[data-testid="ui-typing-dots"]').exists()).toBe(false)

    await wrapper.setProps({ writing: false })
    expect(wrapper.find('[data-testid="plugin-chat-status"]').exists()).toBe(false)
  })

  it('offers copy once the reply is finished, copying its text', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    vi.stubGlobal('navigator', { clipboard: { writeText } })
    const wrapper = mountMessage({ text: 'The answer' }, { writing: true })
    expect(wrapper.find('[data-testid="plugin-chat-copy"]').exists()).toBe(false)

    await wrapper.setProps({ writing: false })
    await wrapper.get('[data-testid="plugin-chat-copy"]').trigger('click')

    expect(writeText).toHaveBeenCalledWith('The answer')
    vi.unstubAllGlobals()
  })

  it('gives a code block of the reply its own copy button', () => {
    const wrapper = mountMessage({ text: '```\nnpm run build\n```' })

    expect(wrapper.get('[data-testid="markdown-copy"]').attributes('aria-label')).toBe('Copy code')
  })

  it('shows a link of the reply as text', () => {
    const wrapper = mountMessage({ links: [{ key: 2, text: 'Open <b>preview</b>', href: 'https://blog.sites.example.test/', external: true }] })

    const link = wrapper.get('[data-testid="plugin-chat-link"]')
    expect(link.text()).toBe('Open <b>preview</b>')
    expect(link.attributes('rel')).toContain('noopener')
  })

  it('draws an issue of the reply', () => {
    const wrapper = mountMessage({ issues: [{ key: 3, text: 'Theme missing.' }] })

    expect(wrapper.get('[data-testid="plugin-chat-issue"]').text()).toBe('Theme missing.')
  })
})
