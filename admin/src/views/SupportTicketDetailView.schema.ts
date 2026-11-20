/**
 * Skeleton schema of a support ticket: a header (a bar for the subject, the close and delete buttons) over the
 * conversation with its reply composer, beside the card of the ticket's details.
 *
 * The screen is a conversation with a side card, so it draws them itself and imports the card constants for their titles;
 * the schema lists the same cards for the skeleton.
 */
import { UiSplitRatio } from '@/utils/ui/layout'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** The conversation card: the messages are data, so it has a title only. */
export const CONVERSATION_CARD = {
  key: 'conversation',
  title: 'support.supportTicketDetail.conversation',
} as const

/** The reply composer under the conversation. */
export const COMPOSER_CARD = {
  key: 'composer',
  rows: ['shared.addReply'],
} as const

/** The side card with the ticket's details: department, priority, status, assignee, creation and the last reply. */
export const DETAILS_CARD = {
  key: 'details',
  title: 'support.supportTicketDetail.ticketSettings',
  rows: [
    'shared.department',
    'shared.priority',
    'shared.status',
    'support.supportTicketDetail.assignee',
    'support.supportTicketDetail.created',
    'shared.lastReply',
  ],
} as const

/** A support ticket: two actions (close or reopen, delete), the conversation column and the details column, three quarters to one quarter. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: { actions: 2 },
  ratio: UiSplitRatio.ThreeQuarters,
  columns: [[CONVERSATION_CARD, COMPOSER_CARD], [DETAILS_CARD]],
}

export default schema
