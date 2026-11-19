/**
 * Skeleton schema of the project form: three cards of fields, the general ones, the gallery and the translated text.
 *
 * The header carries no text of its own: its title is the create or edit wording of the mode, so the skeleton draws a bar for it. The gallery is one field whose control is the list of
 * image slots, and the language tabs are a field of their own, so the skeleton keeps their place above the text.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the project form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {},
  columns: [
    [
      {
        key: 'general',
        title: 'storefront.form.general',
        fields: [
          { key: 'slug', label: 'storefront.projects.slug', span: SkeletonSpan.Full },
          { key: 'url', label: 'storefront.projects.liveUrl' },
          { key: 'sortOrder', label: 'storefront.form.sortOrder' },
          { key: 'isActive', label: 'shared.active', kind: SkeletonFieldKind.Toggle, span: SkeletonSpan.Full },
        ],
      },
      {
        key: 'gallery',
        title: 'storefront.projects.gallery',
        fields: [{ key: 'images', span: SkeletonSpan.Full }],
      },
      {
        key: 'translations',
        title: 'storefront.form.translations',
        fields: [
          { key: 'locales', span: SkeletonSpan.Full },
          { key: 'title', label: 'storefront.projects.projectTitle', span: SkeletonSpan.Full },
          { key: 'summary', label: 'storefront.projects.summary', span: SkeletonSpan.Full },
          { key: 'description', label: 'shared.description', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

export default schema
