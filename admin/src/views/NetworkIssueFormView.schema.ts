/**
 * Skeleton schema of the network issue form: a header over one card of fields.
 *
 * Create and edit share the screen, so the header title and subtitle are not fixed text. The status field exists for
 * an existing issue only; the screen leaves it out of a new one.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** Key of the status field, which only the edit form has. */
export const NETWORK_ISSUE_STATUS_FIELD = 'status'

/** What the network issue form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: { subtitleFromData: true },
  columns: [
    [
      {
        key: 'issue',
        fields: [
          { key: 'title', label: 'shared.title', span: SkeletonSpan.Full },
          { key: 'type', label: 'shared.type' },
          { key: 'server', label: 'support.networkIssueForm.server' },
          { key: 'priority', label: 'shared.priority' },
          { key: NETWORK_ISSUE_STATUS_FIELD, label: 'shared.status' },
          { key: 'startDate', label: 'support.networkIssueForm.startDate' },
          { key: 'endDate', label: 'support.networkIssueForm.endDate' },
          { key: 'description', label: 'shared.description', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

export default schema
