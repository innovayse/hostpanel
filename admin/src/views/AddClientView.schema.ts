/**
 * Skeleton schema of the add client screen: a header over two columns of cards.
 *
 * The left column holds the account owner, the profile and the address and billing details; the right column holds the
 * switches and the internal notes shared with the client profile screen. The owner card is drawn for a new user;
 * {@link EXISTING_OWNER_SECTION} stands in for it while an existing user is being associated.
 */
import { CLIENT_ADMIN_NOTES_SECTION, CLIENT_NOTIFICATIONS_SECTION, CLIENT_SETTINGS_SECTION } from '@/lib/clientFormSections'
import { SkeletonFieldKind, SkeletonKind, SkeletonSpan } from '@/utils/ui/skeletonSchema'
import type { FormSkeletonSchema, SkeletonSection } from '@/utils/ui/skeletonSchema'

/** Key of the account owner section, the one the screen swaps for {@link EXISTING_OWNER_SECTION}. */
export const OWNER_SECTION_KEY = 'owner'

/** The two choices of how the account owner is made, both radios on a line of their own. */
const OWNER_CHOICES = [
  { key: 'ownerNew', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
  { key: 'ownerExisting', kind: SkeletonFieldKind.Check, span: SkeletonSpan.Full },
] as const

/** The account owner card while a new user is created: the two choices, then the credentials. */
const NEW_OWNER_SECTION: SkeletonSection = {
  key: OWNER_SECTION_KEY,
  title: 'clients.addClient.accountOwner',
  fields: [
    ...OWNER_CHOICES,
    { key: 'email', label: 'shared.email', required: true },
    { key: 'password', label: 'shared.password', required: true },
  ],
}

/** The account owner card while an existing user is associated: the two choices, then the user search. */
export const EXISTING_OWNER_SECTION: SkeletonSection = {
  key: OWNER_SECTION_KEY,
  title: 'clients.addClient.accountOwner',
  fields: [
    ...OWNER_CHOICES,
    { key: 'existingUserId', label: 'clients.addClient.findExistingUser', required: true, span: SkeletonSpan.Full },
  ],
}

/** What the add client screen is made of. */
const schema: FormSkeletonSchema = {
  kind: SkeletonKind.Form,
  header: {
    title: 'clients.addClient.addNewClient',
    subtitle: 'clients.addClient.createANewClientAccount',
    actions: 2,
    primaryAction: 1,
  },
  columns: [
    [
      NEW_OWNER_SECTION,
      {
        key: 'profile',
        title: 'clients.addClient.profileDetails',
        fields: [
          { key: 'firstName', label: 'shared.firstName', required: true },
          { key: 'lastName', label: 'shared.lastName', required: true },
          { key: 'companyName', label: 'shared.companyName' },
          { key: 'phone', label: 'shared.phone' },
          { key: 'language', label: 'shared.language' },
          { key: 'status', label: 'shared.status' },
          { key: 'clientGroupId', label: 'clients.clientGroups.group' },
        ],
      },
      {
        key: 'address',
        title: 'clients.addClient.addressBilling',
        fields: [
          { key: 'street', label: 'shared.address1' },
          { key: 'address2', label: 'shared.address2' },
          { key: 'city', label: 'shared.city' },
          { key: 'state', label: 'shared.stateRegion' },
          { key: 'postCode', label: 'shared.postcode' },
          { key: 'country', label: 'shared.country' },
          { key: 'currency', label: 'shared.currency' },
          { key: 'paymentMethod', label: 'shared.paymentMethod' },
          { key: 'billingContact', label: 'shared.billingContact', span: SkeletonSpan.Full },
        ],
      },
    ],
    [CLIENT_NOTIFICATIONS_SECTION, CLIENT_SETTINGS_SECTION, CLIENT_ADMIN_NOTES_SECTION],
  ],
}

export default schema
