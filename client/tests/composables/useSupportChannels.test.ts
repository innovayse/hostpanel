/**
 * Tests for `useSupportChannels`: the ticket form is always offered, the other channels only
 * when the operator configured them, and each card carries its response time when set.
 *
 * @module tests/composables/useSupportChannels.test
 */

import { describe, expect, it } from 'vitest'
import { CLIENT_TICKET_NEW_PATH } from '~/constants/clientRoutes'
import { PortalSettingKey } from '~/constants/portalSettingKey'
import { SupportChannel } from '~/constants/supportChannel'
import { useContactDetails } from '~/composables/useContactDetails'
import { useSupportChannels } from '~/composables/useSupportChannels'
import { loadPortalSettings, usePortalSettingsServer } from '~/tests/helpers/portalSettings'

/** What the stub answers, by route; the hooks start the stub before each test and close it after. */
const { replies } = usePortalSettingsServer({ useContactDetails })

describe('useSupportChannels', () => {
  it('offers only the ticket form when nothing is configured', async () => {
    await loadPortalSettings(replies)

    expect(useSupportChannels().value).toEqual([{ channel: SupportChannel.Ticket, href: CLIENT_TICKET_NEW_PATH }])
  })

  it('offers every channel with links and response times when configured', async () => {
    await loadPortalSettings(replies, {
      [PortalSettingKey.ChatProvider]: 'widget',
      [PortalSettingKey.ContactEmail]: 'help@acme.test',
      [PortalSettingKey.ContactPhone]: '+1 (555) 010-200',
      [PortalSettingKey.SupportResponseChat]: '1 min',
      [PortalSettingKey.SupportResponseTicket]: '1 hour',
      [PortalSettingKey.SupportResponseEmail]: '4 hours',
      [PortalSettingKey.SupportResponsePhone]: '5 min',
    })

    expect(useSupportChannels().value).toEqual([
      { channel: SupportChannel.Chat, responseTime: '1 min' },
      { channel: SupportChannel.Ticket, href: CLIENT_TICKET_NEW_PATH, responseTime: '1 hour' },
      { channel: SupportChannel.Email, value: 'help@acme.test', href: 'mailto:help@acme.test', responseTime: '4 hours' },
      { channel: SupportChannel.Phone, value: '+1 (555) 010-200', href: 'tel:+1555010200', responseTime: '5 min' },
    ])
  })
})
