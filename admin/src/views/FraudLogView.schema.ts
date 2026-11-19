/**
 * What the fraud log is made of: a header, a search box with the kind, decision and reason filters, and a paged table of
 * the recorded checks. `FraudLogView` draws its table head from these columns and `UiSkeletonOf` draws the whole list
 * from the same data while the view's chunk is downloading.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnSize } from '@/utils/ui/table'

/** The fraud log. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: navTitleKey(AppRoute.FraudLog), subtitle: 'fraud.log.subtitle' },
  toolbar: { search: true, filters: ['fraud.log.kind', 'fraud.log.decision', 'fraud.log.reason'] },
  table: {
    pagination: true,
    columns: [
      { key: 'date', label: 'shared.date' },
      { key: 'kind', label: 'fraud.log.kind' },
      { key: 'decision', label: 'fraud.log.decision', size: UiColumnSize.Compact },
      { key: 'score', label: 'fraud.log.score', numeric: true, size: UiColumnSize.Compact },
      { key: 'reasons', label: 'fraud.log.reasons' },
      { key: 'client', label: 'shared.client' },
      { key: 'order', label: 'fraud.log.order' },
      { key: 'ipAddress', label: 'shared.ipAddress' },
      { key: 'email', label: 'shared.email' },
    ],
  },
}

export default schema
