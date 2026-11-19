/**
 * Skeleton schema of the migration tools screen: a header over the history card (a third of the width) and the selected job's progress card (two thirds).
 *
 * It is a detail screen: the left card is the history of the jobs, whose rows are data, and the right card is the
 * progress of the selected job, whose heading is the job's own label and whose figures are the four counters. The
 * help link beside the primary button only appears when the documentation address is configured, so the header counts
 * the button that is always there.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { UiSplitRatio } from '@/utils/ui/layout'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { DetailSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the migration tools screen is made of. */
const schema: DetailSkeletonSchema = {
  kind: SkeletonKind.Detail,
  header: {
    title: navTitleKey(AppRoute.Migration),
    subtitle: 'migration.migration.importExistingClientDataInto',
    actions: 1,
  },
  ratio: UiSplitRatio.OneThird,
  columns: [
    [{ key: 'history', title: 'migration.migration.history' }],
    [
      {
        key: 'progress',
        rows: [
          'migration.migrationProgressCard.migrated',
          'migration.migrationProgressCard.skipped',
          'migration.migrationProgressCard.failed',
          'shared.total',
        ],
      },
    ],
  ],
}

export default schema
