/**
 * Skeleton schema of the support open ticket screen: a header over one card of the ticket's fields, the client first.
 */
import { navTitleKey } from '@/lib/navigation'
import { AppRoute } from '@/lib/routes'
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the support open ticket screen is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {
    title: navTitleKey(AppRoute.SupportTicketsNew),
    crumbs: true,
  },
  columns: [
    [
      {
        key: 'ticket',
        fields: [
          { key: 'clientId', label: 'shared.client', span: SkeletonSpan.Full },
          { key: 'name', label: 'shared.name' },
          { key: 'email', label: 'shared.emailAddress' },
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
