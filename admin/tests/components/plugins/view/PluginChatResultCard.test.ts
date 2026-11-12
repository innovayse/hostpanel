/**
 * Unit tests for PluginChatResultCard: title and sentence as text, and the details behind the card's own switch.
 */
import { describe, expect, it } from 'vitest'
import PluginChatResultCard from '@/components/plugins/view/PluginChatResultCard.vue'
import { PluginBlockTone } from '@/types/pluginview'
import type { PluginChatResult } from '@/types/pluginview'
import { mountWithApp } from '../../../helpers/mount'

/** A card with a title, a sentence and two detail lines. */
const RESULT: PluginChatResult = { key: 1, tone: PluginBlockTone.Success, title: 'Published', text: 'Your <b>site</b> is live.', details: ['Built in 3s', 'Pushed'], actions: [] }

/**
 * Mounts the card.
 *
 * @returns The wrapper.
 */
const mountCard = () => mountWithApp(PluginChatResultCard, { props: { result: RESULT, refresh: [], disabled: false } })

describe('PluginChatResultCard', () => {
  it('shows the title and sentence as text', () => {
    const wrapper = mountCard()

    expect(wrapper.get('[data-testid="plugin-chat-result-title"]').text()).toBe('Published')
    expect(wrapper.get('[data-testid="plugin-chat-result-text"]').text()).toBe('Your <b>site</b> is live.')
    expect(wrapper.find('b').exists()).toBe(false)
  })

  it('opens and closes the details by its own switch', async () => {
    const wrapper = mountCard()
    expect(wrapper.find('[data-testid="plugin-chat-result-details"]').exists()).toBe(false)

    await wrapper.get('[data-testid="plugin-chat-result-details-toggle"]').trigger('click')
    expect(wrapper.get('[data-testid="plugin-chat-result-details"]').text()).toContain('Built in 3s')

    await wrapper.get('[data-testid="plugin-chat-result-details-toggle"]').trigger('click')
    expect(wrapper.find('[data-testid="plugin-chat-result-details"]').exists()).toBe(false)
  })

  it('draws no row of actions for a card with neither actions nor details', () => {
    const wrapper = mountWithApp(PluginChatResultCard, { props: { result: { ...RESULT, details: [] }, refresh: [], disabled: false } })

    expect(wrapper.find('[data-testid="plugin-chat-result-actions"]').exists()).toBe(false)
  })
})
