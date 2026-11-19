/**
 * Skeleton schema of the clients by country report: the world map shaded by country, the bar chart of the top
 * countries and the table of every country. The map is drawn from the country rows once they arrive, so the schema
 * holds its place and the skeleton draws a placeholder of it.
 */
import { SkeletonKind } from '@/utils/ui/skeletonSchema'
import type { ChartReportSkeletonSchema } from '@/utils/ui/skeletonSchema'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import { UiAlign, UiColumnSize } from '@/utils/ui/table'

/** What the clients by country report is made of. */
const schema: ChartReportSkeletonSchema = {
  kind: SkeletonKind.Report,
  header: { title: 'reports.clientsByCountry.clientsByCountry', subtitle: 'reports.clientsByCountry.thisReportShowsTheTotal' },
  map: true,
  chart: { kind: ChartKind.HorizontalBar, height: UiChartHeight.Lg },
  table: {
    columns: [
      { key: 'country', label: 'shared.country', size: UiColumnSize.Grow },
      { key: 'clientCount', label: 'reports.clientsByCountry.activeClients', align: UiAlign.Right },
    ],
  },
}

export default schema
