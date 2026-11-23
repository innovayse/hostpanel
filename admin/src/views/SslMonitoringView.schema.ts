/**
 * Skeleton schema of the SSL monitoring report: the two header controls of its own (the inactive switch and the
 * re-validation), the chips that count the domains of each group and the table of domains with their certificate,
 * grouped by expiry. The chips and the heading rows come with the data, so the schema holds their place.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { UiColumnKind, UiColumnSize } from '@/utils/ui/table'

/** What the screen is made of. */
const schema: ReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.sslMonitoring.sslCertificateMonitoring', subtitle: 'reports.sslMonitoring.displaysAListOfDomains', actions: 2 },
  summary: true,
  table: {
    columns: [
      { key: 'domainName', label: 'shared.domainName', size: UiColumnSize.Grow },
      { key: 'hasSsl', label: 'shared.status', kind: UiColumnKind.Status },
      { key: 'issuer', label: 'reports.sslMonitoring.issuer' },
      { key: 'expiresAt', label: 'reports.sslMonitoring.expiry', kind: UiColumnKind.Date },
      { key: 'lastUpdate', label: 'reports.sslMonitoring.lastUpdate', kind: UiColumnKind.Date },
    ],
    grouped: true,
  },
  emptyText: 'reports.sslMonitoring.noDomains',
}

export default schema
