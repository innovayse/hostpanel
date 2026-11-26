/**
 * Tests for TrackingChatCard: the tag-manager id, the chat provider dropdown and the chat address
 * and the token written per language, each a draft of the page save bar.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest'
import TrackingChatCard from '@/components/settings/TrackingChatCard.vue'
import type { Setting, SettingSave } from '@/types/setting'
import { mountSettingsCard } from '../../helpers/settingsPage'
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
  { id: 7, key: 'portal.chat.website_token.de', value: '' },
  { id: 8, key: 'portal.chat.website_token.es', value: '' },
]

describe('TrackingChatCard', () => {
  it('saves the tag-manager id', async () => {
    const { wrapper, registry } = await mountSettingsCard(TrackingChatCard, rows)

    await wrapper.get('[data-testid="tracking-gtm"]').setValue('GTM-ABC1234')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[1, 'GTM-ABC1234']])
  })

  it('shows chat as off and saves the chosen provider', async () => {
    const { wrapper, registry } = await mountSettingsCard(TrackingChatCard, rows)
    expect(selectText(wrapper, 'tracking-chat-provider')).toBe('Off')

    await pickOption(wrapper, 'tracking-chat-provider', 'Chatwoot')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[2, 'chatwoot']])
  })

  it('shows the stored chat address and saves a token for one language', async () => {
    const { wrapper, registry } = await mountSettingsCard(TrackingChatCard, rows)
    expect((wrapper.get('[data-testid="tracking-chat-base-url"]').element as HTMLInputElement).value).toBe('https://chat.example.com')

    await wrapper.get('[data-testid="tracking-chat-token-tab-hy"]').trigger('click')
    await wrapper.get('[data-testid="tracking-chat-token-hy"]').setValue('tok-hy')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[6, 'tok-hy']])
  })

  it('saves a token for German, a language beyond the first three', async () => {
    const { wrapper, registry } = await mountSettingsCard(TrackingChatCard, rows)

    await wrapper.get('[data-testid="tracking-chat-token-tab-de"]').trigger('click')
    await wrapper.get('[data-testid="tracking-chat-token-de"]').setValue('tok-de')
    await registry.saveAll(save)

    expect(save.mock.calls).toEqual([[7, 'tok-de']])
  })

  it('shows one token field with a tab per language and marks the tabs that are empty', async () => {
    const { wrapper } = await mountSettingsCard(TrackingChatCard, [...rows.slice(0, 5), { id: 6, key: 'portal.chat.website_token.hy', value: 'tok-hy' }])

    expect(wrapper.findAll('[data-testid^="tracking-chat-token"][role="tab"]').map((tab) => tab.attributes('data-testid'))).toEqual([
      'tracking-chat-token-tab-en',
      'tracking-chat-token-tab-ru',
      'tracking-chat-token-tab-hy',
      'tracking-chat-token-tab-de',
      'tracking-chat-token-tab-es',
      'tracking-chat-token-tab-fr',
    ])
    expect(wrapper.find('[data-testid="tracking-chat-token-tab-en-empty"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="tracking-chat-token-tab-hy-empty"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="tracking-chat-token-ru"]').exists()).toBe(false)
  })

  it('puts the refusal of a language on its tab and shows it under the field', async () => {
    const { wrapper, registry } = await mountSettingsCard(TrackingChatCard, rows)
    const refuse = vi.fn<SettingSave>(async () => ({ saved: false, error: 'Token is not valid.' }))

    await wrapper.get('[data-testid="tracking-chat-token-tab-ru"]').trigger('click')
    await wrapper.get('[data-testid="tracking-chat-token-ru"]').setValue('bad')
    await registry.saveAll(refuse)

    expect(wrapper.find('[data-testid="tracking-chat-token-tab-ru-invalid"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Token is not valid.')
  })

  it('warns about every unseeded row instead of drawing its field', async () => {
    const { wrapper } = await mountSettingsCard(TrackingChatCard, [rows[0] as Setting])

    expect(wrapper.find('[data-testid="tracking-chat-provider"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-testid="setting-not-seeded"]')).toHaveLength(2)
  })
})
