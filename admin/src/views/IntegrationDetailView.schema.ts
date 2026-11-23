/**
 * Skeleton schema of the integration detail screen: the config card beside the connection status card, two thirds to one third.
 *
 * The config card's logo, name, fields and buttons come from the integration the API describes, so the schema holds a head
 * made of data, a guess at the number of fields (only the API knows it) and the two buttons; the status card's caption is
 * page text. The special slug `cwp` draws its own screen, which has a schema of its own.
 */
import { UiSplitRatio } from '@/utils/ui/layout'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** The config card: the integration's head, its fields and the Test connection and Save buttons. */
export const INTEGRATION_CONFIG_CARD = { key: 'config', identity: true, fields: 4, actions: 2 } as const

/** The status card: the connection state under its caption. */
export const INTEGRATION_STATUS_CARD = { key: 'status', rows: ['integrations.integrationStatusSidebar.connectionStatus'] } as const

/** What the integration detail screen is made of. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  ratio: UiSplitRatio.TwoThirds,
  columns: [[INTEGRATION_CONFIG_CARD], [INTEGRATION_STATUS_CARD]],
}

export default schema
