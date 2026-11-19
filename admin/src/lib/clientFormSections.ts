/**
 * The form sections the client create form and the client profile form have in common, as skeleton schema data.
 *
 * Both screens ask for the same email notification switches, account setting switches and internal notes, so the
 * sections live here once and each screen's schema places them in its own columns. Pure data: every text is a locale
 * key and nothing is imported but the schema types.
 */
import { SkeletonFieldKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { SkeletonField, SkeletonSection } from '@/utils/ui/skeletonSchema'

/**
 * Builds the field of one switch.
 *
 * @param key - Key of the field, the suffix of its `field-<key>` slot.
 * @param label - Locale key of the caption.
 * @returns A toggle field.
 */
const toggleField = (key: string, label: string): SkeletonField => ({ key, label, kind: SkeletonFieldKind.Toggle })

/** The email notification switches, one per kind of email, in display order. */
export const CLIENT_NOTIFICATIONS_SECTION: SkeletonSection = {
  key: 'notifications',
  title: 'shared.emailNotifications',
  columns: 1,
  fields: [
    toggleField('notifyGeneral', 'shared.generalEmails'),
    toggleField('notifyInvoice', 'shared.invoiceEmails'),
    toggleField('notifySupport', 'shared.supportEmails'),
    toggleField('notifyProduct', 'shared.productEmails'),
    toggleField('notifyDomain', 'shared.domainEmails'),
    toggleField('notifyAffiliate', 'shared.affiliateEmails'),
  ],
}

/** The account setting switches followed by the tax identity fields. */
export const CLIENT_SETTINGS_SECTION: SkeletonSection = {
  key: 'settings',
  title: 'shared.settings',
  columns: 1,
  fields: [
    toggleField('overdueNotices', 'shared.overdueNotices'),
    toggleField('taxExempt', 'shared.taxExempt'),
    toggleField('separateInvoices', 'shared.separateInvoices'),
    toggleField('disableCcProcessing', 'shared.disableCcProcessing'),
    toggleField('marketingOptIn', 'shared.marketingEmailsOptIn'),
    toggleField('statusUpdate', 'shared.statusUpdate'),
    toggleField('allowSso', 'shared.allowSingleSignOn'),
    { key: 'taxId', label: 'shared.taxId' },
    { key: 'taxExemptionReason', label: 'shared.taxExemptionReason' },
  ],
}

/** The internal notes only staff read: one text area under the card title. */
export const CLIENT_ADMIN_NOTES_SECTION: SkeletonSection = {
  key: 'adminNotes',
  title: 'shared.adminNotes',
  columns: 1,
  fields: [{ key: 'adminNotes', kind: SkeletonFieldKind.Textarea, span: SkeletonSpan.Full }],
}
