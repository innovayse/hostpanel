/**
 * Tests for the panel chart table: every panel has a kind, and the dashboard schema draws the same entry.
 */
import { describe, expect, it } from 'vitest'
import { PANEL_CHARTS, PanelChart } from '@/lib/panelCharts'
import { ChartKind, UiChartHeight } from '@/utils/ui/chart'
import dashboardSchema from '@/views/DashboardView.schema'
import supportOverviewSchema from '@/views/SupportOverviewView.schema'

describe('PANEL_CHARTS', () => {
  it('gives the revenue panel vertical bars and the growth panel a line', () => {
    expect(PANEL_CHARTS[PanelChart.Revenue]).toEqual({ kind: ChartKind.GroupedBar, height: UiChartHeight.Short })
    expect(PANEL_CHARTS[PanelChart.Growth].kind).toBe(ChartKind.Line)
  })

  it('is the entry the dashboard and support overview skeletons draw their chart panels from', () => {
    /**
     * Finds the chart of one dashboard panel.
     *
     * @param key - Key of the panel in the dashboard schema.
     * @returns The chart the schema draws for the panel; undefined when the panel has none.
     */
    const chartOf = (key: string) => dashboardSchema.panels.flatMap((row) => row.columns.flat()).find((panel) => panel.key === key)?.chart
    expect(chartOf('revenue')).toBe(PANEL_CHARTS[PanelChart.Revenue])
    expect(chartOf('growth')).toBe(PANEL_CHARTS[PanelChart.Growth])
    expect(chartOf('invoiceStatus')).toBe(PANEL_CHARTS[PanelChart.InvoiceStatus])
    expect(chartOf('tickets')).toBe(PANEL_CHARTS[PanelChart.TicketPriority])
    expect(supportOverviewSchema.panels.flatMap((row) => row.columns.flat()).find((panel) => panel.key === 'hours')?.chart).toBe(PANEL_CHARTS[PanelChart.TicketsByHour])
  })
})
