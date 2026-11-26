/**
 * Skeleton schema of the open ticket screen of a client: a header over one card of the ticket's fields.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the client's open ticket screen is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {
    title: 'shared.openNewTicket',
    crumbs: true,
  },
  columns: [
    [
      {
        key: 'ticket',
        fields: [
          { key: 'subject', label: 'shared.subject', span: SkeletonSpan.Full },
          { key: 'departmentId', label: 'shared.department' },
          { key: 'priority', label: 'shared.priority' },
          { key: 'message', label: 'shared.message', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

export default schema
