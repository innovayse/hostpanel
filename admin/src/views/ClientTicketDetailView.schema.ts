/**
 * Skeleton schema of a client's ticket: a header (a bar for the subject, the close and delete buttons) over the ticket
 * settings, the reply form and the conversation, stacked in one column.
 *
 * The screen is a conversation, so it draws the cards itself and imports the card constants for their titles; the schema
 * lists the same cards for the skeleton. The settings card holds the four fields the operator reads or changes.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** The ticket settings card: department, priority, status and the last reply. */
export const SETTINGS_CARD = {
  key: 'settings',
  title: 'clients.clientTicketDetail.ticketSettings',
  rows: ['shared.department', 'shared.priority', 'shared.status', 'shared.lastReply'],
} as const

/** The reply form card. */
export const REPLY_CARD = {
  key: 'reply',
  title: 'shared.addReply',
  rows: ['shared.message'],
} as const

/** The conversation card: the bubbles are data, so it has a title only. */
export const CONVERSATION_CARD = {
  key: 'conversation',
  title: 'clients.clientTicketDetail.conversation',
} as const

/** A client's ticket: two actions (close or reopen, delete) and the three cards. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: { actions: 2 },
  columns: [[SETTINGS_CARD, REPLY_CARD, CONVERSATION_CARD]],
}

export default schema
