/**
 * Skeleton schema of the client profile tab: two columns of cards, with no header of its own (the client layout owns it).
 *
 * The left column holds the profile, the billing address and the billing preferences; the right column holds the
 * switches and the internal notes shared with the add client screen. Under the form sits the card of the contacts table.
 * `ClientProfileView` draws both blocks from this data and `UiSkeletonOf` draws the same blocks while its chunk is downloading.
 */
import { CLIENT_ADMIN_NOTES_SECTION, CLIENT_NOTIFICATIONS_SECTION, CLIENT_SETTINGS_SECTION } from '@/lib/clientFormSections'
import { SkeletonFieldKind, SkeletonKind, SkeletonPartKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { CardsSkeletonPart, CompositeSkeletonSchema, FormSkeletonPart, SkeletonTable } from '@/utils/ui/skeletonSchema'

/** The form of the tab: two columns of cards. */
export const PROFILE_FORM_PART: FormSkeletonPart = {
  kind: SkeletonPartKind.Form,
  columns: [
    [
      {
        key: 'profile',
        title: 'clients.clientProfile.profile',
        fields: [
          { key: 'identity', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
          { key: 'email', label: 'shared.email', span: SkeletonSpan.Full },
          { key: 'firstName', label: 'shared.firstName', required: true },
          { key: 'lastName', label: 'shared.lastName', required: true },
          { key: 'companyName', label: 'shared.companyName' },
          { key: 'phone', label: 'shared.phone' },
          { key: 'status', label: 'shared.status', span: SkeletonSpan.Full },
          { key: 'clientGroupId', label: 'clients.clientGroups.group', span: SkeletonSpan.Full },
        ],
      },
      {
        key: 'address',
        title: 'clients.clientProfile.billingAddress',
        fields: [
          { key: 'street', label: 'shared.address1' },
          { key: 'address2', label: 'shared.address2' },
          { key: 'city', label: 'shared.city' },
          { key: 'state', label: 'shared.stateRegion' },
          { key: 'postCode', label: 'shared.postcode' },
          { key: 'country', label: 'shared.country' },
        ],
      },
      {
        key: 'preferences',
        title: 'clients.clientProfile.billingPreferences',
        fields: [
          { key: 'currency', label: 'shared.currency' },
          { key: 'paymentMethod', label: 'shared.paymentMethod' },
          { key: 'billingContact', label: 'shared.billingContact', span: SkeletonSpan.Full },
        ],
      },
    ],
    [CLIENT_NOTIFICATIONS_SECTION, CLIENT_SETTINGS_SECTION, CLIENT_ADMIN_NOTES_SECTION],
  ],
}

/** The table of the client's contacts: name, email, phone and the kind of contact. */
export const PROFILE_CONTACTS_TABLE: SkeletonTable = {
  columns: [
    { key: 'name', label: 'shared.name' },
    { key: 'email', label: 'shared.email' },
    { key: 'phone', label: 'shared.phone' },
    { key: 'type', label: 'shared.type' },
  ],
}

/** The card of the contacts table under the form; its heading carries the contact count, which is data, so the card names no title. */
export const PROFILE_CONTACTS_PART: CardsSkeletonPart = {
  kind: SkeletonPartKind.Cards,
  columns: [[{ key: 'contacts', table: PROFILE_CONTACTS_TABLE }]],
}

/** What the client profile tab is made of: the form, then the contacts card. */
const schema: CompositeSkeletonSchema = {
  kind: SkeletonKind.Composite,
  parts: [PROFILE_FORM_PART, PROFILE_CONTACTS_PART],
}

export default schema
