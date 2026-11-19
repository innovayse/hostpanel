/**
 * Skeleton schema of the partner form: two cards of fields, the general ones and the translated name.
 *
 * The header carries no text of its own: its title is the create or edit wording of the mode, so the skeleton draws a bar for it. The language tabs are a field of their own, so the
 * skeleton keeps their place above the name.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the partner form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {},
  columns: [
    [
      {
        key: 'general',
        title: 'storefront.form.general',
        fields: [
          { key: 'logoUrl', label: 'storefront.partners.logo', span: SkeletonSpan.Full },
          { key: 'url', label: 'storefront.partners.website' },
          { key: 'sortOrder', label: 'storefront.form.sortOrder' },
          { key: 'isActive', label: 'shared.active', kind: SkeletonFieldKind.Toggle, span: SkeletonSpan.Full },
        ],
      },
      {
        key: 'translations',
        title: 'storefront.form.translations',
        fields: [
          { key: 'locales', span: SkeletonSpan.Full },
          { key: 'name', label: 'storefront.partners.name', span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

export default schema
