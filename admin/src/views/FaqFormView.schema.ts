/**
 * Skeleton schema of the storefront FAQ form: two single column cards under the page title.
 *
 * The general card holds the category, the sort order and the active switch; the translations card holds the language
 * tabs, the question and the answer. The question carries the key `translations` because that is the key under which the
 * API reports a refused translation, so the form shows its sentence on the field without a mapping.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** What the FAQ form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {},
  columns: [
    [
      {
        key: 'general',
        title: 'storefront.form.general',
        columns: 1,
        fields: [
          { key: 'category', label: 'storefront.faqs.category' },
          { key: 'sortOrder', label: 'storefront.form.sortOrder' },
          { key: 'isActive', label: 'shared.active', kind: SkeletonFieldKind.Toggle },
        ],
      },
      {
        key: 'translations',
        title: 'storefront.form.translations',
        columns: 1,
        fields: [
          { key: 'locale', span: SkeletonSpan.Full },
          { key: 'translations', label: 'storefront.faqs.question' },
          { key: 'answer', label: 'storefront.faqs.answer', kind: SkeletonFieldKind.Textarea },
        ],
      },
    ],
  ],
}

export default schema
