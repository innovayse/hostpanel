/**
 * What the domains list is made of: a header, a search box with the status and registrar filters, and a paged table with
 * a row action. `DomainsListView` draws its table head from these columns and `UiSkeletonOf` draws the whole list from
 * the same data while the view's chunk is downloading.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ListSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiCardRole, UiColumnKind, UiColumnSize, UiHideBelow } from '@/utils/ui/table'

/** The domains list. */
const schema: ListSkeletonSchema = {
  kind: SkeletonKind.List,
  header: { title: 'shared.domains', subtitle: 'domains.domainsList.manageAllRegisteredDomainNames' },
  toolbar: { search: true, filters: ['shared.status', 'shared.registrar'] },
  table: {
    pagination: true,
    columns: [
      { key: 'name', label: 'shared.domainName', size: UiColumnSize.Grow },
      { key: 'client', label: 'shared.client', cardRole: UiCardRole.Subtitle },
      { key: 'registrar', label: 'shared.registrar', hideBelow: UiHideBelow.Lg },
      { key: 'regPeriod', label: 'domains.domainsList.regPeriod', hideBelow: UiHideBelow.Xl },
      { key: 'status', label: 'shared.status', kind: UiColumnKind.Status, cardRole: UiCardRole.Badge },
      { key: 'nextDueDate', label: 'shared.nextDueDate', kind: UiColumnKind.Date, hideBelow: UiHideBelow.Xl },
      { key: 'expiresAt', label: 'domains.domainsList.expiresAt', kind: UiColumnKind.Date },
      { key: 'renewalPrice', label: 'domains.domainsList.renewalPrice', numeric: true },
      { key: 'autoRenew', label: 'shared.autoRenew', kind: UiColumnKind.Status },
      { key: 'actions', actions: true },
    ],
  },
}

export default schema
