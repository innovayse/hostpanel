/**
 * Skeleton schema of the slide form: two cards of fields, the general settings and the translated text.
 *
 * The header carries no text of its own: its title is the create or edit wording of the mode, so the skeleton draws a bar for it. The linked product replaces the learn more link, so
 * the screen leaves that field out while a product is linked. The language tabs are a field of their own, so the
 * skeleton keeps their place above the title.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** Key of the learn more link field, which a linked product takes the place of. */
export const SLIDE_LEARN_MORE_FIELD = 'learnMoreUrl'

/** What the slide form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {},
  columns: [
    [
      {
        key: 'general',
        title: 'settings.slideForm.generalSettings',
        fields: [
          { key: 'iconName', label: 'settings.slideForm.icon', span: SkeletonSpan.Full },
          { key: 'brandColor', label: 'settings.slideForm.brandColor', span: SkeletonSpan.Full },
          { key: 'imageUrl', label: 'settings.slideForm.image', span: SkeletonSpan.Full },
          { key: 'demoUrl', label: 'settings.slideForm.demoUrl', span: SkeletonSpan.Full },
          { key: SLIDE_LEARN_MORE_FIELD, label: 'settings.slideForm.learnMoreUrl', span: SkeletonSpan.Full },
          { key: 'productId', label: 'settings.slideForm.linkedProduct' },
          { key: 'sortOrder', label: 'settings.slideForm.sortOrder' },
          { key: 'isActive', label: 'shared.active', kind: SkeletonFieldKind.Toggle, span: SkeletonSpan.Full },
          { key: 'targetAudience', label: 'settings.slideForm.targetAudience', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
          { key: 'visibleFrom', label: 'settings.slideForm.visibleFrom' },
          { key: 'visibleUntil', label: 'settings.slideForm.visibleUntil' },
        ],
      },
      {
        key: 'translations',
        title: 'settings.slideForm.translations',
        fields: [
          { key: 'locales', span: SkeletonSpan.Full },
          { key: 'title', label: 'shared.title', span: SkeletonSpan.Full },
          { key: 'tagline', label: 'settings.slideForm.tagline', span: SkeletonSpan.Full },
          { key: 'description', label: 'shared.description', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full },
          { key: 'features', label: 'settings.slideForm.features', span: SkeletonSpan.Full },
          { key: 'ctaText', label: 'settings.slideForm.ctaText' },
          { key: 'ctaUrl', label: 'settings.slideForm.ctaUrl' },
        ],
      },
    ],
  ],
}

export default schema
