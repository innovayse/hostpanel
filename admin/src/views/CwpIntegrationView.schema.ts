/**
 * Skeleton schema of the CWP integration screen: the brand header over the config card beside the provider status card.
 *
 * The brand header's name, version and state are data, so it is a head with no caption. The config fields come from the
 * integration the API describes, so the schema holds a guess at their number and the two buttons. The status card's figures
 * are the account count and the time of the last test. The cards flow in one column: the brand takes the whole row, the
 * config and the status share it two thirds to one third.
 */
import { UiSplitRatio } from '@/utils/ui/layout'
import { SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the CWP integration screen is made of. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  ratio: UiSplitRatio.TwoThirds,
  columns: [
    [
      { key: 'brand', identity: true },
      { key: 'config', span: SkeletonSpan.Half, identity: true, fields: 4, actions: 2 },
      { key: 'status', span: SkeletonSpan.Half, rows: ['shared.accounts', 'integrations.providerStatus.lastTested'] },
    ],
  ],
}

export default schema
