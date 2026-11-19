/**
 * Skeleton schema of the TLD pricing form: two columns of cards.
 *
 * The left column holds the general settings of the extension; the right column holds the registration, transfer and
 * renewal pricing tables, each a card with one full width field that carries its table. The header's title is the create or edit
 * wording of the mode, so the skeleton draws a bar for it.
 */
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema, SkeletonSection } from '@/utils/ui/skeletonSchema'

/**
 * One pricing card: a title over one full width field holding the period table.
 *
 * @param key - Key of the card and of its field.
 * @param title - Locale key of the card title.
 * @returns The section.
 */
const pricingSection = (key: string, title: string): SkeletonSection => ({
  key,
  title,
  columns: 1,
  fields: [{ key, span: SkeletonSpan.Full }],
})

/** What the TLD pricing form is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {},
  columns: [
    [
      {
        key: 'general',
        title: 'settings.tldConfigForm.generalSettings',
        columns: 1,
        fields: [
          { key: 'tld', label: 'settings.tldConfigForm.tld' },
          { key: 'registrarModule', label: 'settings.tldConfigForm.registrarProvider' },
          { key: 'costCurrency', label: 'settings.tldConfigForm.costCurrency' },
          { key: 'sellCurrency', label: 'settings.tldConfigForm.sellCurrency' },
          { key: 'enabled', label: 'shared.enabled', kind: SkeletonFieldKind.Toggle },
          { key: 'sortOrder', label: 'settings.tldConfigForm.sortOrder' },
          { key: 'categories', label: 'shared.categories' },
        ],
      },
    ],
    [
      pricingSection('register', 'settings.tldConfigForm.registrationPricing'),
      pricingSection('transfer', 'settings.tldConfigForm.transferPricing'),
      pricingSection('renew', 'settings.tldConfigForm.renewalPricing'),
    ],
  ],
}

export default schema
