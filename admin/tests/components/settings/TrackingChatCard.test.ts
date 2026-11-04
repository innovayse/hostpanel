/**
 * Tests for TrackingChatCard: the tag-manager id, the chat provider dropdown and the four chat
 * fields, each saved on its own.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TrackingChatCard from '@/components/settings/TrackingChatCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountScreen } from '../../helpers/mount'
import { pickOption, selectText } from '../../helpers/select'

/** The save callback the card is given; it answers that the value was stored. */
const save = vi.fn<SettingSave>(async () => ({ saved: true, error: null }))

beforeEach(() => {
  save.mockClear()
})

/** Every row the card edits, as the API seeds them. */
const rows: Setting[] = [
  { id: 1, key: 'portal.analytics.gtm_id', value: '' },
  { id: 2, key: 'portal.chat.provider', value: '' },
  { id: 3, key: 'portal.chat.base_url', value: 'https://chat.example.com' },
  { id: 4, key: 'portal.chat.website_token', value: '' },
  { id: 5, key: 'portal.chat.website_token.ru', value: '' },
  { id: 6, key: 'portal.chat.website_token.hy', value: '' },
]

describe('TrackingChatCard', () => {
  it('saves the tag-manager id', async () => {
    const { wrapper } = await mountScreen(TrackingChatCard, { save, settings: rows })

    await wrapper.get('[data-testid="tracking-gtm"]').setValue('GTM-ABC1234')
    await wrapper.get('[data-testid="tracking-gtm-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[1, 'GTM-ABC1234']])
  })

  it('shows chat as off and saves the chosen provider', async () => {
    const { wrapper } = await mountScreen(TrackingChatCard, { save, settings: rows })
    expect(selectText(wrapper, 'tracking-chat-provider')).toBe('Off')

    await pickOption(wrapper, 'tracking-chat-provider', 'Chatwoot')
    await wrapper.get('[data-testid="tracking-chat-provider-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[2, 'chatwoot']])
  })

  it('shows the stored chat address and saves a token for one language', async () => {
    const { wrapper } = await mountScreen(TrackingChatCard, { save, settings: rows })
    expect((wrapper.get('[data-testid="tracking-chat-base-url"]').element as HTMLInputElement).value).toBe('https://chat.example.com')

    await wrapper.get('[data-testid="tracking-chat-token-hy"]').setValue('tok-hy')
    await wrapper.get('[data-testid="tracking-chat-token-hy-save"]').trigger('click')

    expect(save.mock.calls).toEqual([[6, 'tok-hy']])
  })

  it('warns about every unseeded row instead of drawing its field', async () => {
    const { wrapper } = await mountScreen(TrackingChatCard, { save, settings: [rows[0] as Setting] })

    expect(wrapper.find('[data-testid="tracking-chat-provider"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="setting-not-seeded"]')).toHaveLength(4)
  })
})
