/**
 * Skeleton schema of the announcement form: a header over one card of fields.
 *
 * The creation date is read-only and exists only for a stored announcement, so the screen leaves
 * {@link ANNOUNCEMENT_DATE_FIELD_KEY} out of a new one. The title and subtitle follow the mode (create or edit), so the
 * skeleton draws a bar for each.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** Key of the read-only creation date field, which a new announcement does not have. */
export const ANNOUNCEMENT_DATE_FIELD_KEY = 'createdOn'

/** What the announcement form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: { subtitleFromData: true },
  columns: [
    [
      {
        key: 'announcement',
        fields: [
          { key: 'title', label: 'shared.title' },
          { key: ANNOUNCEMENT_DATE_FIELD_KEY, label: 'shared.date' },
          { key: 'content', label: 'support.announcementForm.announcement', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
          { key: 'published', label: 'shared.published', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

export default schema
