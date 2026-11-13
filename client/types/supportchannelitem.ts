/**
 * Support channel item type: one way to reach support, as the support section shows it.
 *
 * @module types/supportchannelitem
 */

import type { SupportChannel } from '~/constants/supportChannel'

/** A support channel that is available. */
export interface SupportChannelItem {
  /** Which channel it is. */
  channel: SupportChannel
  /** The address or number to show, when the channel has one. */
  value?: string
  /** Where the card links to, when it links anywhere. */
  href?: string
  /** How fast the operator answers on this channel, when set. */
  responseTime?: string
}
