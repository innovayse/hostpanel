/**
 * Skeleton schema of the email campaign form: a header over one card of subject, body and audience.
 *
 * The title and subtitle follow the mode (new, draft or read-only), so the skeleton draws a bar for each. The action
 * buttons under the fields (the section slot) and the delivery card of a read-only campaign are real content only,
 * since which of them exist depends on the campaign.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the campaign form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: { subtitleFromData: true },
  columns: [
    [
      {
        key: 'campaign',
        columns: 1,
        fields: [
          { key: 'subject', label: 'shared.subject', span: SkeletonSpan.Full },
          { key: 'body', label: 'emailCampaigns.form.body', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
          { key: 'segments', label: 'emailCampaigns.form.audience', span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

export default schema
