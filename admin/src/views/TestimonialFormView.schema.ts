/**
 * Skeleton schema of the testimonial form: two cards of fields, the general ones and the translated role and quote.
 *
 * The header carries no text of its own: its title is the create or edit wording of the mode, so the skeleton draws a bar for it. The language tabs are a field of their own, so the
 * skeleton keeps their place above the role.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the testimonial form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {},
  columns: [
    [
      {
        key: 'general',
        title: 'storefront.form.general',
        fields: [
          { key: 'name', label: 'storefront.testimonials.name' },
          { key: 'company', label: 'storefront.testimonials.company' },
          { key: 'rating', label: 'storefront.testimonials.rating' },
          { key: 'avatarUrl', label: 'storefront.testimonials.avatar', span: SkeletonSpan.Full },
          { key: 'publishedAt', label: 'storefront.testimonials.publishedAt' },
          { key: 'sortOrder', label: 'storefront.form.sortOrder' },
          { key: 'isActive', label: 'shared.active', kind: SkeletonFieldKind.Toggle, span: SkeletonSpan.Full },
        ],
      },
      {
        key: 'translations',
        title: 'storefront.form.translations',
        fields: [
          { key: 'locales', span: SkeletonSpan.Full },
          { key: 'role', label: 'storefront.testimonials.role', span: SkeletonSpan.Full },
          { key: 'quote', label: 'storefront.testimonials.quote', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
        ],
      },
    ],
  ],
}

export default schema
