/**
 * Skeleton schema of the client layout: the header card's key figures and the section tab strip.
 *
 * The figures' keys and captions are what the layout draws its card from, and the tabs come from the list the section
 * tab strip is made of, so the skeleton shown while the layout downloads carries the real captions.
 */
import { CLIENT_NAV } from '@/lib/clientSections'
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { TabsSkeletonSchema } from '@/utils/ui/skeletonSchema'

/** Keys of the figures of the header card; also the suffix of each one's `data-testid`. */
export const ClientStatKey = {
  Id: 'id',
  Email: 'email',
  Joined: 'joined',
  TwoFactor: 'two-factor',
  Credit: 'credit',
} as const

/** One of the keys of the header figures. */
export type ClientStatKey = (typeof ClientStatKey)[keyof typeof ClientStatKey]

/** The client layout: the header card (identity, the export button, five key figures in a row of six) over the section tabs. */
const schema: TabsSkeletonSchema = {
  kind: SkeletonKind.Tabs,
  actions: 1,
  statColumns: 6,
  stats: [
    { key: ClientStatKey.Id, label: 'shared.id' },
    { key: ClientStatKey.Email, label: 'shared.email' },
    { key: ClientStatKey.Joined, label: 'clients.clientsList.joined' },
    { key: ClientStatKey.TwoFactor, label: 'clients.clientsList.2fa' },
    { key: ClientStatKey.Credit, label: 'clients.clientCredit.credit' },
  ],
  tabs: CLIENT_NAV.map(entry => ({ key: entry.key, label: entry.label })),
}

export default schema
